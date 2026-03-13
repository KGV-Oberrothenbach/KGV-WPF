using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using KGV.Core.Impressum;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Core.Security;
using KGV.Wpf.Helpers;

namespace KGV.Wpf.ViewModels;

public sealed class ImpressumViewModel : BaseViewModel, INavigationAware
{
    private readonly ISupabaseService _supabaseService;
    private readonly UserContext _userContext;

    public string Headline => "Impressum";

    public string VerantwortlichHeadline => "Verantwortlich";
    public string VerantwortlichText => "Kleingartenverein Oberrothenbach e.V.";

    public ObservableCollection<ImpressumSlotItem> VorstandSlots { get; } = new();
    public ObservableCollection<ImpressumSlotItem> BauausschussSlots { get; } = new();
    public ObservableCollection<MemberOption> MemberOptions { get; } = new();

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                InvalidateCommands();
        }
    }

    private string? _statusText;
    public string? StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private bool _isEditMode;
    public bool IsEditMode
    {
        get => _isEditMode;
        private set
        {
            if (SetProperty(ref _isEditMode, value))
                InvalidateCommands();
        }
    }

    private bool _hasUnsavedChanges;
    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        private set
        {
            if (SetProperty(ref _hasUnsavedChanges, value))
                InvalidateCommands();
        }
    }

    public bool CanEditImpressum => _userContext.Role == UserRole.Admin || _userContext.Role == UserRole.Vorstand;

    public RelayCommand<object?> RefreshCommand { get; }
    public RelayCommand<object?> ToggleEditCommand { get; }
    public RelayCommand<object?> SaveCommand { get; }
    public RelayCommand<object?> CancelCommand { get; }

    private IReadOnlyDictionary<string, int?> _snapshotBySlotKey = new Dictionary<string, int?>();
    private bool _loaded;

    public ImpressumViewModel(ISupabaseService supabaseService, UserContext userContext)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));

        RefreshCommand = new RelayCommand<object?>(_ => _ = LoadAsync(), _ => !IsBusy);
        ToggleEditCommand = new RelayCommand<object?>(_ => ToggleEdit(), _ => !IsBusy && CanEditImpressum);
        SaveCommand = new RelayCommand<object?>(_ => _ = SaveAsync(), _ => !IsBusy && IsEditMode && HasUnsavedChanges);
        CancelCommand = new RelayCommand<object?>(_ => _ = CancelAsync(), _ => !IsBusy && IsEditMode);
    }

    public async Task OnNavigatedToAsync()
    {
        if (_loaded) return;
        _loaded = true;
        await LoadAsync();
    }

    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusText = null;

        try
        {
            var slotsTask = _supabaseService.GetImpressumFunktionSlotsAsync();
            var membersTask = _supabaseService.GetMitgliederAsync();

            await Task.WhenAll(slotsTask, membersTask);

            var slots = slotsTask.Result ?? new List<ImpressumFunktionSlotRecord>();
            var members = membersTask.Result ?? new List<MitgliedRecord>();

            var byKey = slots
                .Where(x => !string.IsNullOrWhiteSpace(x.SlotKey))
                .GroupBy(x => x.SlotKey!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var membersById = members
                .Where(x => x != null && x.Id > 0)
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First());

            RebuildMemberOptions(members, byKey.Values.Select(x => x.MitgliedId).Where(x => x.HasValue).Select(x => x!.Value));

            VorstandSlots.Clear();
            BauausschussSlots.Clear();

            foreach (var def in ImpressumSlotDefinitions.All.OrderBy(x => x.SortOrder))
            {
                byKey.TryGetValue(def.SlotKey, out var rec);

                var current = rec ?? new ImpressumFunktionSlotRecord
                {
                    Id = 0,
                    SlotKey = def.SlotKey,
                    Funktion = def.FunktionLabel,
                    SortOrder = def.SortOrder,
                    MitgliedId = null
                };

                var item = new ImpressumSlotItem(def, current, membersById, MemberOptions, OnAnySlotChanged);

                if (def.Bereich == ImpressumBereich.Vorstand)
                    VorstandSlots.Add(item);
                else
                    BauausschussSlots.Add(item);
            }

            IsEditMode = false;
            HasUnsavedChanges = false;
            Snapshot();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            VorstandSlots.Clear();
            BauausschussSlots.Clear();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RebuildMemberOptions(IEnumerable<MitgliedRecord> members, IEnumerable<int> ensureIncludedMemberIds)
    {
        MemberOptions.Clear();

        MemberOptions.Add(MemberOption.NotAssigned);

        var ensure = new HashSet<int>(ensureIncludedMemberIds);

        var all = (members ?? Enumerable.Empty<MitgliedRecord>())
            .Where(m => m != null && m.Id > 0)
            .ToList();

        // Empfehlung umgesetzt: Auswahl standardmäßig aus aktiven Mitgliedern.
        // Wenn ein Slot bereits ein inaktives Mitglied zugeordnet hat, wird es dennoch in die Auswahl aufgenommen.
        var active = all.Where(m => m.Aktiv).ToList();
        var inactiveButAssigned = all.Where(m => !m.Aktiv && ensure.Contains(m.Id)).ToList();

        var options = active
            .Concat(inactiveButAssigned)
            .GroupBy(m => m.Id)
            .Select(g => g.First())
            .OrderBy(m => (m.Name ?? string.Empty).Trim(), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(m => (m.Vorname ?? string.Empty).Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Select(MemberOption.FromMember)
            .ToList();

        foreach (var opt in options)
            MemberOptions.Add(opt);
    }

    private void ToggleEdit()
    {
        if (!CanEditImpressum)
            return;

        if (!IsEditMode)
        {
            IsEditMode = true;
            HasUnsavedChanges = false;
            Snapshot();
            return;
        }

        _ = CancelAsync();
    }

    private async Task SaveAsync()
    {
        if (!IsEditMode || !HasUnsavedChanges) return;

        IsBusy = true;
        StatusText = null;

        try
        {
            var all = VorstandSlots.Concat(BauausschussSlots)
                .OrderBy(x => x.SortOrder)
                .Select(x => x.ToRecord())
                .ToList();

            var ok = await _supabaseService.SaveImpressumFunktionSlotsAsync(all);
            if (!ok)
            {
                StatusText = "Speichern fehlgeschlagen.";
                return;
            }

            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CancelAsync()
    {
        if (!IsEditMode) return;

        if (HasUnsavedChanges)
        {
            var decision = System.Windows.MessageBox.Show(
                "Ungespeicherte Änderungen verwerfen?",
                "Abbrechen",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (decision != System.Windows.MessageBoxResult.Yes)
                return;
        }

        IsEditMode = false;
        HasUnsavedChanges = false;
        await LoadAsync();
    }

    private void Snapshot()
    {
        _snapshotBySlotKey = VorstandSlots
            .Concat(BauausschussSlots)
            .GroupBy(x => x.SlotKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().SelectedMemberId, StringComparer.OrdinalIgnoreCase);
    }

    private void OnAnySlotChanged()
    {
        if (!IsEditMode)
            return;

        var current = VorstandSlots
            .Concat(BauausschussSlots)
            .GroupBy(x => x.SlotKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().SelectedMemberId, StringComparer.OrdinalIgnoreCase);

        HasUnsavedChanges = !DictionaryEquals(_snapshotBySlotKey, current);
    }

    private static bool DictionaryEquals(IReadOnlyDictionary<string, int?> a, IReadOnlyDictionary<string, int?> b)
    {
        if (a.Count != b.Count) return false;

        foreach (var kv in a)
        {
            if (!b.TryGetValue(kv.Key, out var other))
                return false;

            if (kv.Value != other)
                return false;
        }

        return true;
    }

    private void InvalidateCommands()
    {
        RefreshCommand.RaiseCanExecuteChanged();
        ToggleEditCommand.RaiseCanExecuteChanged();
        SaveCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
    }

    public sealed class MemberOption
    {
        private MemberOption(int? id, string displayName, string telefon, string handy, bool aktiv)
        {
            Id = id;
            DisplayName = displayName;
            Telefon = telefon;
            Handy = handy;
            Aktiv = aktiv;
        }

        public int? Id { get; }
        public string DisplayName { get; }
        public string Telefon { get; }
        public string Handy { get; }
        public bool Aktiv { get; }

        public string TelefonText => string.IsNullOrWhiteSpace(Telefon) ? string.Empty : $"Telefon: {Telefon}";
        public string HandyText => string.IsNullOrWhiteSpace(Handy) ? string.Empty : $"Handy: {Handy}";

        public bool HasTelefon => !string.IsNullOrWhiteSpace(Telefon);
        public bool HasHandy => !string.IsNullOrWhiteSpace(Handy);

        public static MemberOption NotAssigned { get; } = new(null, "nicht zugeordnet", string.Empty, string.Empty, aktiv: true);

        public static MemberOption FromMember(MitgliedRecord m)
        {
            var vorname = (m.Vorname ?? string.Empty).Trim();
            var nachname = (m.Name ?? string.Empty).Trim();
            var name = string.Join(" ", new[] { vorname, nachname }.Where(x => !string.IsNullOrWhiteSpace(x)));
            if (string.IsNullOrWhiteSpace(name))
                name = $"#{m.Id}";

            if (!m.Aktiv)
                name += " (inaktiv)";

            return new MemberOption(
                m.Id,
                name,
                (m.Telefon ?? string.Empty).Trim(),
                (m.Handy ?? string.Empty).Trim(),
                m.Aktiv);
        }
    }

    public sealed class ImpressumSlotItem : BaseViewModel
    {
        private readonly Action _onChanged;
        private readonly ImpressumSlotDefinition _def;

        public ImpressumSlotItem(
            ImpressumSlotDefinition def,
            ImpressumFunktionSlotRecord record,
            IReadOnlyDictionary<int, MitgliedRecord> membersById,
            ObservableCollection<MemberOption> memberOptions,
            Action onChanged)
        {
            _def = def;
            _onChanged = onChanged;

            SlotId = record.Id;
            SlotKey = def.SlotKey;
            FunktionLabel = def.FunktionLabel;
            SortOrder = def.SortOrder;

            var selected = MemberOption.NotAssigned;

            if (record.MitgliedId.HasValue && membersById.TryGetValue(record.MitgliedId.Value, out var m))
                selected = MemberOption.FromMember(m);

            // Wenn der ausgewählte Eintrag nicht in der globalen Optionsliste ist (z.B. inaktiv), ihn ergänzen.
            if (selected.Id.HasValue && memberOptions.All(x => x.Id != selected.Id))
                memberOptions.Add(selected);

            _selectedMember = memberOptions.FirstOrDefault(x => x.Id == selected.Id) ?? MemberOption.NotAssigned;
        }

        public long SlotId { get; private set; }
        public string SlotKey { get; }
        public string FunktionLabel { get; }
        public int SortOrder { get; }

        private MemberOption _selectedMember;
        public MemberOption SelectedMember
        {
            get => _selectedMember;
            set
            {
                if (value == null) value = MemberOption.NotAssigned;
                if (SetProperty(ref _selectedMember, value))
                {
                    OnPropertyChanged(nameof(SelectedMemberId));
                    OnPropertyChanged(nameof(AssignedName));
                    OnPropertyChanged(nameof(TelefonText));
                    OnPropertyChanged(nameof(HandyText));
                    OnPropertyChanged(nameof(HasTelefon));
                    OnPropertyChanged(nameof(HasHandy));
                    _onChanged();
                }
            }
        }

        public int? SelectedMemberId => SelectedMember.Id;

        public string AssignedName => SelectedMember.Id.HasValue ? SelectedMember.DisplayName : "nicht zugeordnet";
        public string TelefonText => SelectedMember.TelefonText;
        public string HandyText => SelectedMember.HandyText;
        public bool HasTelefon => SelectedMember.HasTelefon;
        public bool HasHandy => SelectedMember.HasHandy;

        public ImpressumFunktionSlotRecord ToRecord()
        {
            return new ImpressumFunktionSlotRecord
            {
                Id = SlotId,
                SlotKey = SlotKey,
                Funktion = FunktionLabel,
                SortOrder = SortOrder,
                MitgliedId = SelectedMemberId
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using KGV.Core.Impressum;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Core.Security;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Shapes;

namespace KGV.Maui.Pages;

public sealed class ImpressumPage : FooterContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly IUserContextAccessor _userContextAccessor;

    private bool _isBusy;
    private Task? _loadTask;

    private bool _isEditMode;
    private bool _hasUnsavedChanges;

    private IReadOnlyDictionary<string, int?> _snapshotBySlotKey = new Dictionary<string, int?>();

    private readonly ObservableCollection<MemberOption> _memberOptions = new();
    private readonly ObservableCollection<ImpressumSlotItem> _vorstandSlots = new();
    private readonly ObservableCollection<ImpressumSlotItem> _bauSlots = new();

    private readonly ActivityIndicator _busy;
    private readonly Label _status;

    private readonly Button _editButton;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;

    public ImpressumPage(ISupabaseService supabaseService, IUserContextAccessor userContextAccessor)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _userContextAccessor = userContextAccessor ?? throw new ArgumentNullException(nameof(userContextAccessor));

        Title = "Impressum";

        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };
        _status = new Label { TextColor = Colors.Red };

        _editButton = new Button { Text = "Bearbeiten" };
        _editButton.Clicked += async (_, __) => await ToggleEditAsync();

        _saveButton = new Button { Text = "Speichern" };
        _saveButton.Clicked += async (_, __) => await SaveAsync();

        _cancelButton = new Button { Text = "Abbrechen" };
        _cancelButton.Clicked += async (_, __) => await CancelAsync();

        object? cardStyleObj = null;
        if (Application.Current?.Resources != null)
            Application.Current.Resources.TryGetValue("Card", out cardStyleObj);
        var cardStyle = cardStyleObj as Style;

        Border WrapCard(View content)
            => cardStyle != null
                ? new Border { Style = cardStyle, Content = content }
                : new Border
                {
                    Stroke = Colors.LightGray,
                    StrokeThickness = 1,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = 12,
                    Content = content
                };

        var verantwortlich = WrapCard(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = "Verantwortlich", FontSize = 18, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Kleingartenverein Oberrothenbach e.V.", LineBreakMode = LineBreakMode.WordWrap }
            }
        });

        var vorstandList = new VerticalStackLayout { Spacing = 12 };
        BindableLayout.SetItemsSource(vorstandList, _vorstandSlots);
        BindableLayout.SetItemTemplate(vorstandList, new DataTemplate(() => CreateSlotView(WrapCard)));

        var bauList = new VerticalStackLayout { Spacing = 12 };
        BindableLayout.SetItemsSource(bauList, _bauSlots);
        BindableLayout.SetItemTemplate(bauList, new DataTemplate(() => CreateSlotView(WrapCard)));

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        header.Add(new Label { Text = "Impressum", FontSize = 24, FontAttributes = FontAttributes.Bold }, 0, 0);
        header.Add(_editButton, 1, 0);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children =
                {
                    header,

                    _busy,
                    _status,

                    verantwortlich,

                    new Label { Text = "Vorstand", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    vorstandList,

                    new Label { Text = "Bauausschuss", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    bauList,

                    // Save/Cancel am Formularende
                    new HorizontalStackLayout
                    {
                        Spacing = 12,
                        HorizontalOptions = LayoutOptions.End,
                        Children = { _saveButton, _cancelButton }
                    }
                }
            }
        };

        Appearing += OnAppearing;
        Disappearing += (_, _) => _status.Text = string.Empty;

        UpdateUiState();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_isEditMode && _hasUnsavedChanges)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                var discard = await DisplayAlert("Impressum", "Ungespeicherte Änderungen verwerfen?", "Ja", "Nein");
                if (!discard)
                    return;

                await CancelAsync();
                await Shell.Current.GoToAsync("..");
            });

            return true;
        }

        return base.OnBackButtonPressed();
    }

    private bool CanEditImpressum
    {
        get
        {
            var role = _userContextAccessor.CurrentUserContext?.Role;
            return role == UserRole.Admin || role == UserRole.Vorstand;
        }
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        await EnsureLoadedAsync();
    }

    private Task EnsureLoadedAsync()
    {
        if (_loadTask != null && !_loadTask.IsCompleted)
            return _loadTask;

        _loadTask = LoadAsync();
        return _loadTask;
    }

    private async Task LoadAsync()
    {
        if (_isBusy)
            return;

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            var slotTask = _supabaseService.GetImpressumFunktionSlotsAsync();
            var memTask = _supabaseService.GetMitgliederAsync();

            await Task.WhenAll(slotTask, memTask);

            var slots = slotTask.Result ?? new List<ImpressumFunktionSlotRecord>();
            var members = memTask.Result ?? new List<MitgliedRecord>();

            var byKey = slots
                .Where(x => !string.IsNullOrWhiteSpace(x.SlotKey))
                .GroupBy(x => x.SlotKey!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var assignedIds = byKey.Values
                .Select(x => x.MitgliedId)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToHashSet();

            RebuildMemberOptions(members, assignedIds);

            _vorstandSlots.Clear();
            _bauSlots.Clear();

            var membersById = members.Where(x => x != null && x.Id > 0)
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First());

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

                var item = new ImpressumSlotItem(def, current, _memberOptions, membersById);
                item.SelectedMemberChanged += (_, __) => OnAnySlotChanged();
                item.IsEditMode = _isEditMode;

                if (def.Bereich == ImpressumBereich.Vorstand)
                    _vorstandSlots.Add(item);
                else
                    _bauSlots.Add(item);
            }

            Snapshot();
            _hasUnsavedChanges = false;
            UpdateUiState();
        }
        catch (Exception ex)
        {
            _vorstandSlots.Clear();
            _bauSlots.Clear();
            _status.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RebuildMemberOptions(IEnumerable<MitgliedRecord> members, HashSet<int> assignedIds)
    {
        _memberOptions.Clear();
        _memberOptions.Add(MemberOption.NotAssigned);

        var all = (members ?? Enumerable.Empty<MitgliedRecord>())
            .Where(m => m != null && m.Id > 0)
            .ToList();

        var active = all.Where(m => m.Aktiv).ToList();
        var inactiveButAssigned = all.Where(m => !m.Aktiv && assignedIds.Contains(m.Id)).ToList();

        var options = active
            .Concat(inactiveButAssigned)
            .GroupBy(m => m.Id)
            .Select(g => g.First())
            .OrderBy(m => (m.Name ?? string.Empty).Trim(), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(m => (m.Vorname ?? string.Empty).Trim(), StringComparer.CurrentCultureIgnoreCase)
            .Select(MemberOption.FromMember);

        foreach (var opt in options)
            _memberOptions.Add(opt);
    }

    private View CreateSlotView(Func<View, Border> wrapCard)
    {
        var funktion = new Label { FontAttributes = FontAttributes.Bold };
        funktion.SetBinding(Label.TextProperty, nameof(ImpressumSlotItem.FunktionLabel));

        var name = new Label { FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.WordWrap };
        name.SetBinding(Label.TextProperty, nameof(ImpressumSlotItem.AssignedName));

        var tel = new Label { LineBreakMode = LineBreakMode.WordWrap };
        tel.SetBinding(Label.TextProperty, nameof(ImpressumSlotItem.TelefonText));
        tel.SetBinding(IsVisibleProperty, nameof(ImpressumSlotItem.HasTelefon));

        var handy = new Label { LineBreakMode = LineBreakMode.WordWrap };
        handy.SetBinding(Label.TextProperty, nameof(ImpressumSlotItem.HandyText));
        handy.SetBinding(IsVisibleProperty, nameof(ImpressumSlotItem.HasHandy));

        var display = new VerticalStackLayout { Spacing = 4, Children = { name, tel, handy } };
        display.SetBinding(IsVisibleProperty, nameof(ImpressumSlotItem.ShowDisplay));

        var picker = new Picker { Title = "Person auswählen" };
        picker.ItemsSource = _memberOptions;
        picker.ItemDisplayBinding = new Binding(nameof(MemberOption.DisplayName));
        picker.SetBinding(Picker.SelectedItemProperty, nameof(ImpressumSlotItem.SelectedMember));

        var edit = new VerticalStackLayout { Spacing = 6, Children = { picker, tel, handy } };
        edit.SetBinding(IsVisibleProperty, nameof(ImpressumSlotItem.ShowEdit));

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };

        grid.Add(new VerticalStackLayout { Spacing = 6, Children = { funktion } }, 0, 0);
        grid.Add(new VerticalStackLayout { Spacing = 6, Children = { display, edit } }, 1, 0);

        return wrapCard(grid);
    }

    private async Task ToggleEditAsync()
    {
        if (!CanEditImpressum)
            return;

        if (_isEditMode)
        {
            await CancelAsync();
            return;
        }

        _isEditMode = true;
        foreach (var s in _vorstandSlots.Concat(_bauSlots))
            s.IsEditMode = true;

        _hasUnsavedChanges = false;
        Snapshot();
        UpdateUiState();
    }

    private async Task SaveAsync()
    {
        if (!_isEditMode)
            return;

        if (!_hasUnsavedChanges)
            return;

        if (_isBusy)
            return;

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            var all = _vorstandSlots.Concat(_bauSlots)
                .OrderBy(x => x.SortOrder)
                .Select(x => x.ToRecord())
                .ToList();

            var ok = await _supabaseService.SaveImpressumFunktionSlotsAsync(all);
            if (!ok)
            {
                _status.Text = "Speichern fehlgeschlagen.";
                return;
            }

            _isEditMode = false;
            foreach (var s in _vorstandSlots.Concat(_bauSlots))
                s.IsEditMode = false;

            _hasUnsavedChanges = false;
            _loadTask = null;
            await EnsureLoadedAsync();
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
            UpdateUiState();
        }
    }

    private async Task CancelAsync()
    {
        if (!_isEditMode)
            return;

        if (_hasUnsavedChanges)
        {
            var decision = await DisplayAlert("Abbrechen", "Ungespeicherte Änderungen verwerfen?", "Ja", "Nein");
            if (!decision)
                return;
        }

        _isEditMode = false;
        foreach (var s in _vorstandSlots.Concat(_bauSlots))
            s.IsEditMode = false;

        _hasUnsavedChanges = false;
        _loadTask = null;
        await EnsureLoadedAsync();

        UpdateUiState();
    }

    private void Snapshot()
    {
        _snapshotBySlotKey = _vorstandSlots
            .Concat(_bauSlots)
            .GroupBy(x => x.SlotKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().SelectedMemberId, StringComparer.OrdinalIgnoreCase);
    }

    private void OnAnySlotChanged()
    {
        if (!_isEditMode)
            return;

        var current = _vorstandSlots
            .Concat(_bauSlots)
            .GroupBy(x => x.SlotKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().SelectedMemberId, StringComparer.OrdinalIgnoreCase);

        _hasUnsavedChanges = !DictionaryEquals(_snapshotBySlotKey, current);
        UpdateUiState();
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

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        _busy.IsVisible = busy;
        _busy.IsRunning = busy;
        UpdateUiState();
    }

    private void UpdateUiState()
    {
        _editButton.IsVisible = CanEditImpressum && !_isEditMode;
        _saveButton.IsVisible = _isEditMode;
        _cancelButton.IsVisible = _isEditMode;
        _saveButton.IsEnabled = _isEditMode && _hasUnsavedChanges && !_isBusy;
        _cancelButton.IsEnabled = _isEditMode && !_isBusy;
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

            return new MemberOption(m.Id, name, (m.Telefon ?? string.Empty).Trim(), (m.Handy ?? string.Empty).Trim(), m.Aktiv);
        }
    }

    public sealed class ImpressumSlotItem : BindableObject
    {
        private MemberOption _selectedMember;

        public ImpressumSlotItem(ImpressumSlotDefinition def, ImpressumFunktionSlotRecord record, ObservableCollection<MemberOption> memberOptions, IReadOnlyDictionary<int, MitgliedRecord> membersById)
        {
            SlotId = record.Id;
            SlotKey = def.SlotKey;
            FunktionLabel = def.FunktionLabel;
            SortOrder = def.SortOrder;

            var selected = MemberOption.NotAssigned;
            if (record.MitgliedId.HasValue && membersById.TryGetValue(record.MitgliedId.Value, out var m))
                selected = MemberOption.FromMember(m);

            if (selected.Id.HasValue && memberOptions.All(x => x.Id != selected.Id))
                memberOptions.Add(selected);

            _selectedMember = memberOptions.FirstOrDefault(x => x.Id == selected.Id) ?? MemberOption.NotAssigned;
        }

        public long SlotId { get; private set; }
        public string SlotKey { get; }
        public string FunktionLabel { get; }
        public int SortOrder { get; }

        private bool _isEditMode;
        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                if (_isEditMode == value) return;
                _isEditMode = value;
                OnPropertyChanged(nameof(IsEditMode));
                OnPropertyChanged(nameof(ShowDisplay));
                OnPropertyChanged(nameof(ShowEdit));
            }
        }

        public bool ShowDisplay => !IsEditMode;
        public bool ShowEdit => IsEditMode;

        public event EventHandler? SelectedMemberChanged;

        public MemberOption SelectedMember
        {
            get => _selectedMember;
            set
            {
                if (value == null) value = MemberOption.NotAssigned;
                if (_selectedMember == value) return;
                _selectedMember = value;
                OnPropertyChanged(nameof(SelectedMember));
                OnPropertyChanged(nameof(SelectedMemberId));
                OnPropertyChanged(nameof(AssignedName));
                OnPropertyChanged(nameof(TelefonText));
                OnPropertyChanged(nameof(HandyText));
                OnPropertyChanged(nameof(HasTelefon));
                OnPropertyChanged(nameof(HasHandy));
                SelectedMemberChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public int? SelectedMemberId => SelectedMember.Id;

        public string AssignedName => SelectedMember.Id.HasValue ? SelectedMember.DisplayName : "nicht zugeordnet";

        public string TelefonText => string.IsNullOrWhiteSpace(SelectedMember.Telefon) ? string.Empty : $"Telefon: {SelectedMember.Telefon}";
        public string HandyText => string.IsNullOrWhiteSpace(SelectedMember.Handy) ? string.Empty : $"Handy: {SelectedMember.Handy}";
        public bool HasTelefon => !string.IsNullOrWhiteSpace(SelectedMember.Telefon);
        public bool HasHandy => !string.IsNullOrWhiteSpace(SelectedMember.Handy);

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

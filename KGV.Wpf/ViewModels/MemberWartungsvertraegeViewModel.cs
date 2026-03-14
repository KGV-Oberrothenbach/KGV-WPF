using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Core.Security;
using KGV.Wpf.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace KGV.Wpf.ViewModels;

public sealed class MemberWartungsvertraegeViewModel : BaseViewModel, INavigationAware
{
    private readonly ISupabaseService _supabaseService;
    private readonly UserContext _userContext;
    private readonly MemberDTO _member;
    private readonly SemaphoreSlim _opLock = new(1, 1);

    public string MemberDisplay => _member.DisplayName;

    public bool CanEdit => _userContext.Role == UserRole.Admin || _userContext.Role == UserRole.Vorstand;

    public ObservableCollection<WartungsvertragRecord> AvailableContracts { get; } = new();
    public ObservableCollection<ZuordnungItem> ActiveAssignments { get; } = new();
    public ObservableCollection<ZuordnungItem> EndedAssignments { get; } = new();

    private bool _showEnded;
    public bool ShowEnded
    {
        get => _showEnded;
        set => SetProperty(ref _showEnded, value);
    }

    private WartungsvertragRecord? _selectedContractToAdd;
    public WartungsvertragRecord? SelectedContractToAdd
    {
        get => _selectedContractToAdd;
        set
        {
            if (SetProperty(ref _selectedContractToAdd, value))
                AssignCommand.RaiseCanExecuteChanged();
        }
    }

    private DateTime _gueltigAb = DateTime.Today;
    public DateTime GueltigAb
    {
        get => _gueltigAb;
        set => SetProperty(ref _gueltigAb, value);
    }

    private string _bemerkung = string.Empty;
    public string Bemerkung
    {
        get => _bemerkung;
        set => SetProperty(ref _bemerkung, value);
    }

    private string _endBemerkung = string.Empty;
    public string EndBemerkung
    {
        get => _endBemerkung;
        set => SetProperty(ref _endBemerkung, value);
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
                AssignCommand.RaiseCanExecuteChanged();
                EndAssignmentCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public RelayCommand<object?> RefreshCommand { get; }
    public RelayCommand<object?> AssignCommand { get; }
    public RelayCommand<ZuordnungItem?> EndAssignmentCommand { get; }

    public MemberWartungsvertraegeViewModel(ISupabaseService supabaseService, UserContext userContext, MemberDTO member)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        _member = member ?? throw new ArgumentNullException(nameof(member));

        RefreshCommand = new RelayCommand<object?>(_ => _ = LoadAsync(), _ => !IsBusy);
        AssignCommand = new RelayCommand<object?>(_ => _ = AssignAsync(), _ => CanEdit && !IsBusy && SelectedContractToAdd != null);
        EndAssignmentCommand = new RelayCommand<ZuordnungItem?>(x => _ = EndAsync(x), x => CanEdit && !IsBusy && x != null && x.IsActive);
    }

    public Task OnNavigatedFromAsync() => Task.CompletedTask;

    public Task OnNavigatedToAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        if (!await _opLock.WaitAsync(0))
            return;

        IsBusy = true;
        StatusText = string.Empty;

        try
        {
            var contracts = await _supabaseService.GetWartungsvertraegeAsync();
            var all = (contracts ?? new List<WartungsvertragRecord>()).Where(x => x != null).ToList();

            AvailableContracts.Clear();
            foreach (var c in all.Where(x => x.Aktiv).OrderBy(x => x.Bereich).ThenBy(x => x.Titel))
                AvailableContracts.Add(c);

            var contractById = all.ToDictionary(x => x.Id, x => x);

            var z = await _supabaseService.GetWartungsvertragZuordnungenAsync(_member.Id);

            var items = (z ?? new List<WartungsvertragZuordnungRecord>()).Where(x => x != null)
                .Select(x => new ZuordnungItem(x, contractById.TryGetValue(x.WartungsvertragId, out var c) ? c : null))
                .OrderByDescending(x => x.GueltigAb)
                .ThenByDescending(x => x.Id)
                .ToList();

            ActiveAssignments.Clear();
            EndedAssignments.Clear();

            foreach (var one in items)
            {
                if (one.IsActive) ActiveAssignments.Add(one);
                else EndedAssignments.Add(one);
            }

            SelectedContractToAdd = AvailableContracts.FirstOrDefault();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
            _opLock.Release();
        }
    }

    private async Task AssignAsync()
    {
        if (!CanEdit) return;
        if (SelectedContractToAdd == null) return;

        var contractId = SelectedContractToAdd.Id;
        if (contractId <= 0)
        {
            StatusText = "Bitte einen Vertrag auswählen.";
            return;
        }

        if (ActiveAssignments.Any(x => x.WartungsvertragId == contractId))
        {
            StatusText = "Dieser Vertrag ist bereits aktiv zugeordnet.";
            return;
        }

        if (!await _opLock.WaitAsync(0))
            return;

        IsBusy = true;
        StatusText = string.Empty;

        try
        {
            var rec = new WartungsvertragZuordnungRecord
            {
                WartungsvertragId = contractId,
                HauptmitgliedId = _member.Id,
                GueltigAb = DateTime.SpecifyKind(GueltigAb.Date.AddHours(12), DateTimeKind.Unspecified),
                GueltigBis = null,
                Bemerkung = string.IsNullOrWhiteSpace(Bemerkung) ? null : Bemerkung.Trim()
            };

            var saved = await _supabaseService.SaveWartungsvertragZuordnungAsync(rec);
            if (saved == null)
            {
                StatusText = "Speichern fehlgeschlagen.";
                return;
            }

            Bemerkung = string.Empty;
            await LoadAsync();
            StatusText = "Zugewiesen.";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
            _opLock.Release();
        }
    }

    private async Task EndAsync(ZuordnungItem? item)
    {
        if (!CanEdit) return;
        if (item == null) return;

        var confirm = MessageBox.Show(
            "Zuordnung wirklich beenden?",
            "Wartungsvertrag beenden",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
            return;

        if (!await _opLock.WaitAsync(0))
            return;

        IsBusy = true;
        StatusText = string.Empty;

        try
        {
            var ok = await _supabaseService.EndWartungsvertragZuordnungAsync(item.Id, DateTime.Today, string.IsNullOrWhiteSpace(EndBemerkung) ? null : EndBemerkung.Trim());
            if (!ok)
            {
                StatusText = "Beenden fehlgeschlagen.";
                return;
            }

            EndBemerkung = string.Empty;
            await LoadAsync();
            StatusText = "Beendet.";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
            _opLock.Release();
        }
    }

    public sealed class ZuordnungItem
    {
        private readonly WartungsvertragZuordnungRecord _rec;
        private readonly WartungsvertragRecord? _contract;

        public ZuordnungItem(WartungsvertragZuordnungRecord rec, WartungsvertragRecord? contract)
        {
            _rec = rec;
            _contract = contract;
        }

        public long Id => _rec.Id;
        public long WartungsvertragId => _rec.WartungsvertragId;
        public string Titel => _contract?.Titel ?? $"Vertrag #{_rec.WartungsvertragId}";
        public string Bereich => _contract?.Bereich ?? string.Empty;
        public bool BefreitVonPflichtstunden => _contract?.BefreitVonPflichtstunden ?? false;
        public DateTime GueltigAb => _rec.GueltigAb;
        public DateTime? GueltigBis => _rec.GueltigBis;
        public string? Bemerkung => _rec.Bemerkung;

        public bool IsActive
        {
            get
            {
                // DB schreibt i.d.R. datum-ähnliche Werte; wir behandeln Enddatum >= heute als aktiv.
                if (!_rec.GueltigBis.HasValue) return true;
                return _rec.GueltigBis.Value.Date >= DateTime.Today;
            }
        }

        public string Display
        {
            get
            {
                var range = GueltigBis.HasValue
                    ? $"{GueltigAb:dd.MM.yyyy} – {GueltigBis:dd.MM.yyyy}"
                    : $"ab {GueltigAb:dd.MM.yyyy}";

                var befreit = BefreitVonPflichtstunden ? " (befreit)" : string.Empty;
                return $"{Bereich} – {Titel}{befreit} | {range}";
            }
        }
    }
}

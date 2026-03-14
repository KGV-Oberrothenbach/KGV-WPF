using System.Globalization;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Core.Security;
using KGV.Maui.State;

namespace KGV.Maui.Pages;

public sealed class MemberWartungsvertraegePage : FooterContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly MemberSelectionState _memberSelection;
    private readonly IUserContextAccessor _userContextAccessor;

    private bool _isBusy;
    private Task? _initTask;

    private readonly Label _header;
    private readonly Label _subHeader;
    private readonly Label _status;
    private readonly ActivityIndicator _busy;

    private readonly Picker _contractPicker;
    private readonly DatePicker _gueltigAb;
    private readonly Entry _bemerkung;
    private readonly Button _assignButton;

    private readonly Entry _endBemerkung;

    private readonly Switch _showEnded;

    private readonly CollectionView _activeList;
    private readonly CollectionView _endedList;

    private readonly List<WartungsvertragRecord> _contracts = new();
    private readonly List<ZuordnungVm> _active = new();
    private readonly List<ZuordnungVm> _ended = new();

    public MemberWartungsvertraegePage(
        ISupabaseService supabaseService,
        MemberSelectionState memberSelection,
        IUserContextAccessor userContextAccessor)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _memberSelection = memberSelection ?? throw new ArgumentNullException(nameof(memberSelection));
        _userContextAccessor = userContextAccessor ?? throw new ArgumentNullException(nameof(userContextAccessor));

        Title = "Wartungsverträge";

        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };
        _header = new Label { Text = "Wartungsverträge", FontSize = 24, FontAttributes = FontAttributes.Bold };
        _subHeader = new Label { Text = string.Empty, Opacity = 0.8 };
        _status = new Label { TextColor = Colors.Gray };

        _contractPicker = new Picker { Title = "Vertrag" };
        _contractPicker.ItemDisplayBinding = new Binding(nameof(WartungsvertragRecord.Titel));

        _gueltigAb = new DatePicker { Date = DateTime.Today };
        _bemerkung = new Entry { Placeholder = "Bemerkung (optional)" };

        _assignButton = new Button { Text = "Zuweisen" };
        _assignButton.Clicked += async (_, __) => await AssignAsync();

        _endBemerkung = new Entry { Placeholder = "Bemerkung beim Beenden (optional)" };

        _showEnded = new Switch { IsToggled = false };
        _showEnded.Toggled += (_, __) => UpdateEndedVisibility();

        _activeList = new CollectionView
        {
            ItemsSource = _active,
            ItemTemplate = new DataTemplate(() =>
            {
                var title = new Label { FontAttributes = FontAttributes.Bold };
                title.SetBinding(Label.TextProperty, nameof(ZuordnungVm.Display));

                var remark = new Label { FontSize = 12, TextColor = Colors.Gray };
                remark.SetBinding(Label.TextProperty, nameof(ZuordnungVm.Bemerkung));

                var endButton = new Button { Text = "Beenden", FontSize = 12 };
                endButton.SetBinding(Button.BindingContextProperty, new Binding(path: "."));
                endButton.Clicked += async (s, _) =>
                {
                    if (s is not Button b) return;
                    if (b.BindingContext is not ZuordnungVm vm) return;
                    await EndAsync(vm);
                };

                return new VerticalStackLayout
                {
                    Padding = new Thickness(0, 8),
                    Children =
                    {
                        title,
                        remark,
                        new HorizontalStackLayout { Spacing = 10, Children = { endButton } },
                        new BoxView { HeightRequest = 1, Color = Colors.LightGray }
                    }
                };
            })
        };

        _endedList = new CollectionView
        {
            ItemsSource = _ended,
            IsVisible = false,
            ItemTemplate = new DataTemplate(() =>
            {
                var title = new Label { FontAttributes = FontAttributes.Bold };
                title.SetBinding(Label.TextProperty, nameof(ZuordnungVm.Display));

                var remark = new Label { FontSize = 12, TextColor = Colors.Gray };
                remark.SetBinding(Label.TextProperty, nameof(ZuordnungVm.Bemerkung));

                return new VerticalStackLayout
                {
                    Padding = new Thickness(0, 8),
                    Children = { title, remark, new BoxView { HeightRequest = 1, Color = Colors.LightGray } }
                };
            })
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 12,
                Children =
                {
                    _header,
                    _subHeader,
                    _busy,
                    _status,
                    new Label { Text = "Neue Zuordnung", FontAttributes = FontAttributes.Bold },
                    _contractPicker,
                    new HorizontalStackLayout { Spacing = 10, Children = { new Label { Text = "Gültig ab" }, _gueltigAb } },
                    _bemerkung,
                    _assignButton,
                    new Label { Text = "Aktive Zuordnungen", FontAttributes = FontAttributes.Bold },
                    _endBemerkung,
                    _activeList,
                    new HorizontalStackLayout
                    {
                        Spacing = 10,
                        Children = { new Label { Text = "Beendete anzeigen" }, _showEnded }
                    },
                    _endedList
                }
            }
        };

        Appearing += async (_, __) => await EnsureInitializedAsync();
        Disappearing += (_, _) => _status.Text = string.Empty;

        UpdateUiState();
    }

    private bool CanEdit => (_userContextAccessor.CurrentUserContext?.Role ?? UserRole.User) is UserRole.Admin or UserRole.Vorstand;

    private Task EnsureInitializedAsync()
    {
        if (_initTask != null && !_initTask.IsCompleted)
            return _initTask;

        _initTask = InitializeAsync();
        return _initTask;
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
        var memberSelected = _memberSelection.SelectedMitgliedId.HasValue;
        var canEdit = CanEdit;

        _contractPicker.IsEnabled = !_isBusy && canEdit && memberSelected;
        _gueltigAb.IsEnabled = !_isBusy && canEdit && memberSelected;
        _bemerkung.IsEnabled = !_isBusy && canEdit && memberSelected;
        _assignButton.IsEnabled = !_isBusy && canEdit && memberSelected && _contractPicker.SelectedItem != null;

        _endBemerkung.IsEnabled = !_isBusy && canEdit && memberSelected;
        _activeList.IsEnabled = !_isBusy && memberSelected;
        _endedList.IsEnabled = !_isBusy && memberSelected;
    }

    private void UpdateEndedVisibility()
    {
        _endedList.IsVisible = _showEnded.IsToggled;
    }

    private async Task InitializeAsync()
    {
        if (_isBusy)
            return;

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            var selectedId = _memberSelection.SelectedMitgliedId;
            if (!selectedId.HasValue)
            {
                ClearUi("Bitte erst ein Mitglied wählen (Mitgliedersuche).", clearLists: true);
                return;
            }

            var member = await _supabaseService.GetMitgliedByIdAsync(selectedId.Value);
            var hauptmitgliedId = member?.HauptmitgliedId ?? selectedId.Value;

            _subHeader.Text = member != null
                ? $"{member.Vorname} {member.Name}".Trim()
                : $"Mitglied #{hauptmitgliedId}";

            var contracts = await _supabaseService.GetWartungsvertraegeAsync();
            _contracts.Clear();
            if (contracts != null) _contracts.AddRange(contracts.Where(x => x != null && x.Aktiv));

            _contracts.Sort((a, b) => string.Compare(a.Bereich + a.Titel, b.Bereich + b.Titel, StringComparison.CurrentCultureIgnoreCase));

            _contractPicker.ItemsSource = _contracts;
            _contractPicker.SelectedItem = _contracts.FirstOrDefault();

            await LoadAssignmentsAsync(hauptmitgliedId);

            if (!CanEdit)
                _status.Text = "Hinweis: Keine Bearbeitungsberechtigung (Admin/Vorstand erforderlich).";
        }
        catch (Exception ex)
        {
            ClearUi(ex.Message, clearLists: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ClearUi(string message, bool clearLists)
    {
        _status.Text = message;
        _subHeader.Text = string.Empty;

        if (clearLists)
        {
            _active.Clear();
            _ended.Clear();
        }
    }

    private async Task LoadAssignmentsAsync(int hauptmitgliedId)
    {
        var z = await _supabaseService.GetWartungsvertragZuordnungenAsync(hauptmitgliedId);

        var contractById = _contracts.ToDictionary(x => x.Id, x => x);

        var items = (z ?? new List<WartungsvertragZuordnungRecord>()).Where(x => x != null)
            .Select(x => new ZuordnungVm(x, contractById.TryGetValue(x.WartungsvertragId, out var c) ? c : null))
            .OrderByDescending(x => x.GueltigAb)
            .ThenByDescending(x => x.Id)
            .ToList();

        _active.Clear();
        _ended.Clear();

        foreach (var one in items)
        {
            if (one.IsActive) _active.Add(one);
            else _ended.Add(one);
        }

        UpdateEndedVisibility();
    }

    private async Task AssignAsync()
    {
        if (!CanEdit) return;

        if (_contractPicker.SelectedItem is not WartungsvertragRecord contract)
        {
            _status.Text = "Bitte Vertrag auswählen.";
            return;
        }

        var selectedId = _memberSelection.SelectedMitgliedId;
        if (!selectedId.HasValue)
        {
            _status.Text = "Kein Mitglied gewählt.";
            return;
        }

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            var member = await _supabaseService.GetMitgliedByIdAsync(selectedId.Value);
            var hauptmitgliedId = member?.HauptmitgliedId ?? selectedId.Value;

            if (_active.Any(x => x.WartungsvertragId == contract.Id))
            {
                _status.Text = "Dieser Vertrag ist bereits aktiv zugeordnet.";
                return;
            }

            var rec = new WartungsvertragZuordnungRecord
            {
                WartungsvertragId = contract.Id,
                HauptmitgliedId = hauptmitgliedId,
                GueltigAb = DateTime.SpecifyKind(_gueltigAb.Date.Date.AddHours(12), DateTimeKind.Unspecified),
                GueltigBis = null,
                Bemerkung = string.IsNullOrWhiteSpace(_bemerkung.Text) ? null : _bemerkung.Text.Trim()
            };

            var saved = await _supabaseService.SaveWartungsvertragZuordnungAsync(rec);
            if (saved == null)
            {
                _status.Text = "Speichern fehlgeschlagen.";
                return;
            }

            _bemerkung.Text = string.Empty;
            await LoadAssignmentsAsync(hauptmitgliedId);
            _status.Text = "Zugewiesen.";
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task EndAsync(ZuordnungVm vm)
    {
        if (!CanEdit) return;

        var ok = await DisplayAlert(
            "Wartungsvertrag beenden",
            "Zuordnung wirklich beenden?",
            "Beenden",
            "Abbrechen");

        if (!ok)
            return;

        var selectedId = _memberSelection.SelectedMitgliedId;
        if (!selectedId.HasValue)
        {
            _status.Text = "Kein Mitglied gewählt.";
            return;
        }

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            var member = await _supabaseService.GetMitgliedByIdAsync(selectedId.Value);
            var hauptmitgliedId = member?.HauptmitgliedId ?? selectedId.Value;

            var success = await _supabaseService.EndWartungsvertragZuordnungAsync(vm.Id, DateTime.Today, string.IsNullOrWhiteSpace(_endBemerkung.Text) ? null : _endBemerkung.Text.Trim());
            if (!success)
            {
                _status.Text = "Beenden fehlgeschlagen.";
                return;
            }

            _endBemerkung.Text = string.Empty;
            await LoadAssignmentsAsync(hauptmitgliedId);
            _status.Text = "Beendet.";
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private sealed class ZuordnungVm
    {
        private readonly WartungsvertragZuordnungRecord _rec;
        private readonly WartungsvertragRecord? _contract;

        public ZuordnungVm(WartungsvertragZuordnungRecord rec, WartungsvertragRecord? contract)
        {
            _rec = rec;
            _contract = contract;
        }

        public long Id => _rec.Id;
        public long WartungsvertragId => _rec.WartungsvertragId;
        public DateTime GueltigAb => _rec.GueltigAb;
        public DateTime? GueltigBis => _rec.GueltigBis;
        public string Bemerkung => _rec.Bemerkung ?? string.Empty;

        public bool IsActive
        {
            get
            {
                if (!_rec.GueltigBis.HasValue) return true;
                return _rec.GueltigBis.Value.Date >= DateTime.Today;
            }
        }

        public string Display
        {
            get
            {
                var title = _contract?.Titel ?? $"Vertrag #{_rec.WartungsvertragId}";
                var bereich = _contract?.Bereich ?? string.Empty;
                var befreit = (_contract?.BefreitVonPflichtstunden ?? false) ? " (befreit)" : string.Empty;

                var range = GueltigBis.HasValue
                    ? $"{GueltigAb:dd.MM.yyyy} – {GueltigBis:dd.MM.yyyy}"
                    : $"ab {GueltigAb:dd.MM.yyyy}";

                return $"{bereich} – {title}{befreit} | {range}";
            }
        }
    }
}

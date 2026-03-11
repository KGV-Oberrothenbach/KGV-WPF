using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Maui.State;
using Microsoft.Maui.Layouts;

namespace KGV.Maui.Pages;

public sealed class GartenStromPage : ContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly MemberSelectionState _memberSelection;
    private readonly ParzelleSelectionState _parzelleSelection;

    private int? _loadedMemberId;
    private int? _loadedParzelleId;

    private bool _isBusy;
    private Task? _initTask;

    private readonly ActivityIndicator _busy;
    private readonly Label _header;
    private readonly Label _subHeader;
    private readonly Label _status;

    private readonly Button _addAblesungButton;
    private readonly Button _swapZaehlerButton;

    private readonly FlexLayout _parzellenButtons;
    private readonly List<Button> _parzellenButtonsCreated = new();
    private readonly CollectionView _list;

    private readonly List<ParzelleOption> _parzellen = new();

    private const short ZaehlerTypStrom = 1;

    public GartenStromPage(
        ISupabaseService supabaseService,
        MemberSelectionState memberSelection,
        ParzelleSelectionState parzelleSelection)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _memberSelection = memberSelection ?? throw new ArgumentNullException(nameof(memberSelection));
        _parzelleSelection = parzelleSelection ?? throw new ArgumentNullException(nameof(parzelleSelection));

        Title = "Strom";

        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };
        _header = new Label { Text = "Strom", FontSize = 24, FontAttributes = FontAttributes.Bold };
        _subHeader = new Label { Text = string.Empty, Opacity = 0.8 };
        _status = new Label { TextColor = Colors.Red };

        _addAblesungButton = new Button { Text = "Ablesung erfassen" };
        _addAblesungButton.Clicked += OnAddAblesungClicked;

        _swapZaehlerButton = new Button { Text = "Zähler tauschen" };
        _swapZaehlerButton.Clicked += OnSwapZaehlerClicked;

        _parzellenButtons = new FlexLayout
        {
            Direction = FlexDirection.Row,
            Wrap = FlexWrap.Wrap,
            JustifyContent = FlexJustify.Start,
            AlignItems = FlexAlignItems.Start,
            AlignContent = FlexAlignContent.Start
        };

        _list = new CollectionView
        {
            ItemTemplate = new DataTemplate(() =>
            {
                var date = new Label { FontAttributes = FontAttributes.Bold };
                date.SetBinding(Label.TextProperty, new Binding(nameof(ZaehlerAblesungDTO.Ablesedatum), stringFormat: "{0:d}"));

                var stand = new Label();
                stand.SetBinding(Label.TextProperty, new Binding(nameof(ZaehlerAblesungDTO.Stand), stringFormat: "Stand: {0}"));

                var nr = new Label { Opacity = 0.8, FontSize = 12 };
                nr.SetBinding(Label.TextProperty, new Binding(nameof(ZaehlerAblesungDTO.Zaehlernummer), stringFormat: "Zähler: {0}"));

                return new VerticalStackLayout
                {
                    Padding = 12,
                    Spacing = 2,
                    Children = { date, stand, nr }
                };
            })
        };

        Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 12,
            Children =
            {
                _header,
                _subHeader,
                new HorizontalStackLayout { Spacing = 12, Children = { _addAblesungButton, _swapZaehlerButton } },
                _parzellenButtons,
                _busy,
                _status,
                _list
            }
        };

        Appearing += OnAppearing;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        await EnsureInitializedAsync();
    }

    private Task EnsureInitializedAsync()
    {
        // Guard gegen parallele Initialisierung (schnelles Navigieren / mehrfaches Appearing)
        if (_initTask != null && !_initTask.IsCompleted)
            return _initTask;

        _initTask = EnsureInitializedCoreAsync();
        return _initTask;
    }

    private async Task EnsureInitializedCoreAsync()
    {
        _status.Text = string.Empty;

        var selectedMemberId = _memberSelection.SelectedMitgliedId;
        if (selectedMemberId == null)
        {
            ClearUi("Bitte erst ein Mitglied wählen (Mitgliedersuche).", clearContext: true);
            return;
        }

        var memberId = selectedMemberId.Value;
        if (_loadedMemberId != memberId)
        {
            _loadedMemberId = memberId;
            _loadedParzelleId = null;

            // defensiv: Parzellenkontext beim Member-Wechsel zurücksetzen
            _parzelleSelection.SelectedParzelleId = null;
            _parzelleSelection.GartenNr = null;

            await LoadParzellenAsync(memberId);
        }

        // defensiv: SelectedParzelleId muss zu den geladenen Parzellen passen
        if (_parzelleSelection.SelectedParzelleId == null || !_parzellen.Any(p => p.ParzelleId == _parzelleSelection.SelectedParzelleId.Value))
        {
            var first = _parzellen.FirstOrDefault();
            _parzelleSelection.SelectedParzelleId = first?.ParzelleId;
            _parzelleSelection.GartenNr = first?.GartenNr;
        }

        if (_parzelleSelection.SelectedParzelleId == null)
        {
            _subHeader.Text = "Bitte eine Parzelle wählen.";
            _list.ItemsSource = null;
            return;
        }

        // Beim erneuten Öffnen defensiv neu laden (stale Daten vermeiden)
        _loadedParzelleId = _parzelleSelection.SelectedParzelleId;
        await LoadAblesungenAsync(_loadedParzelleId.Value);
    }

    private async Task LoadParzellenAsync(int mitgliedId)
    {
        SetBusy(true);
        try
        {
            _parzellen.Clear();
            ClearParzellenButtons();

            var belegungen = await _supabaseService.GetBelegungenForMitgliedAsync(mitgliedId);
            if (belegungen == null || belegungen.Count == 0)
            {
                _parzelleSelection.SelectedParzelleId = null;
                _parzelleSelection.GartenNr = null;
                _status.Text = "Keine Parzellen für dieses Mitglied gefunden.";
                return;
            }

            var allParzellen = await _supabaseService.GetAllParzellenAsync();
            var map = allParzellen.ToDictionary(p => p.Id, p => p);

            foreach (var b in belegungen.OrderByDescending(x => x.BisDatum == null))
            {
                if (!map.TryGetValue(b.ParzelleId, out var p))
                    continue;

                _parzellen.Add(new ParzelleOption(p.Id, p.GartenNr ?? "?", p.Anlage));
            }

            _parzellen.Sort((a, b) => string.Compare(a.GartenNr, b.GartenNr, StringComparison.CurrentCultureIgnoreCase));

            if (_parzelleSelection.SelectedParzelleId == null)
            {
                var first = _parzellen.FirstOrDefault();
                _parzelleSelection.SelectedParzelleId = first?.ParzelleId;
                _parzelleSelection.GartenNr = first?.GartenNr;
            }

            RenderParzellenButtons();
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

    private void RenderParzellenButtons()
    {
        ClearParzellenButtons();

        var selectedId = _parzelleSelection.SelectedParzelleId;
        var primary = (Color?)Application.Current?.Resources?["KgvPrimary"] ?? Colors.DarkOliveGreen;
        var surface = (Color?)Application.Current?.Resources?["KgvSurface"] ?? Colors.LightGray;
        var text = (Color?)Application.Current?.Resources?["KgvText"] ?? Colors.Black;

        foreach (var p in _parzellen)
        {
            var isSelected = selectedId.HasValue && p.ParzelleId == selectedId.Value;

            var btn = new Button
            {
                Text = p.GartenNr,
                CornerRadius = 16,
                HeightRequest = 42,
                FontSize = 13,
                Padding = new Thickness(14, 10),
                Margin = new Thickness(0, 0, 8, 8),
                BackgroundColor = isSelected ? primary : surface,
                TextColor = isSelected ? Colors.White : text
            };

            btn.Clicked += async (_, __) =>
            {
                if (_isBusy)
                    return;

                _parzelleSelection.SelectedParzelleId = p.ParzelleId;
                _parzelleSelection.GartenNr = p.GartenNr;
                RenderParzellenButtons();
                await EnsureInitializedAsync();
            };

            _parzellenButtonsCreated.Add(btn);
            _parzellenButtons.Children.Add(btn);
        }
    }

    private async Task LoadAblesungenAsync(int parzelleId)
    {
        _status.Text = string.Empty;
        _subHeader.Text = $"Garten: {_parzelleSelection.GartenNr ?? "?"}";

        SetBusy(true);
        try
        {
            var list = await _supabaseService.GetStromAblesungenAsync(parzelleId);
            var items = (list ?? new List<ZaehlerAblesungDTO>())
                .OrderByDescending(x => x.Ablesedatum)
                .ToList();

            _list.ItemsSource = items;
            if (items.Count == 0)
                _status.Text = "Keine Ablesungen vorhanden.";
        }
        catch (Exception ex)
        {
            _status.Text = ex.Message;
            _list.ItemsSource = null;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ClearParzellenButtons()
    {
        _parzellenButtons.Children.Clear();
        _parzellenButtonsCreated.Clear();
    }

    private void ClearUi(string message, bool clearContext)
    {
        _subHeader.Text = string.Empty;
        _status.Text = message;
        _list.ItemsSource = null;
        ClearParzellenButtons();

        _parzellen.Clear();
        _loadedMemberId = null;
        _loadedParzelleId = null;

        if (clearContext)
        {
            _parzelleSelection.SelectedParzelleId = null;
            _parzelleSelection.GartenNr = null;
        }
    }

    private void SetBusy(bool value)
    {
        _isBusy = value;
        _busy.IsVisible = value;
        _busy.IsRunning = value;

        // UI defensiv sperren
        _list.IsEnabled = !value;
        foreach (var btn in _parzellenButtonsCreated)
            btn.IsEnabled = !value;

        _addAblesungButton.IsEnabled = !value;
        _swapZaehlerButton.IsEnabled = !value;
    }

    private async void OnAddAblesungClicked(object? sender, EventArgs e)
    {
        if (_isBusy)
            return;

        if (_parzelleSelection.SelectedParzelleId == null)
        {
            await DisplayAlert("Hinweis", "Bitte zuerst eine Parzelle wählen.", "OK");
            return;
        }

        var parzelleId = _parzelleSelection.SelectedParzelleId.Value;

        SetBusy(true);
        try
        {
            var zaehler = await _supabaseService.GetActiveStromzaehlerAsync(parzelleId, DateTime.Today);
            if (zaehler == null)
            {
                await DisplayAlert("Hinweis", "Kein aktiver Stromzähler vorhanden. Bitte ggf. zuerst einen Zähler einbauen/tauschen.", "OK");
                return;
            }

            var standText = await DisplayPromptAsync("Ablesung", "Stand eingeben", "OK", "Abbrechen", keyboard: Keyboard.Numeric);
            if (standText == null)
                return;

            if (!decimal.TryParse(standText.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var stand) || stand < 0)
            {
                await DisplayAlert("Fehler", "Stand ist ungültig.", "OK");
                return;
            }

            var ok = await _supabaseService.AddAblesungAsync(ZaehlerTypStrom, zaehler.Id, DateTime.Today, stand, fotoPfad: null);
            if (!ok)
            {
                await DisplayAlert("Fehler", "Speichern fehlgeschlagen.", "OK");
                return;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", ex.Message, "OK");
        }
        finally
        {
            SetBusy(false);
        }

        await EnsureInitializedAsync();
    }

    private async void OnSwapZaehlerClicked(object? sender, EventArgs e)
    {
        if (_isBusy)
            return;

        if (_parzelleSelection.SelectedParzelleId == null)
        {
            await DisplayAlert("Hinweis", "Bitte zuerst eine Parzelle wählen.", "OK");
            return;
        }

        var parzelleId = _parzelleSelection.SelectedParzelleId.Value;

        var newNr = await DisplayPromptAsync("Zähler tauschen", "Neue Zählernummer", "OK", "Abbrechen");
        if (newNr == null)
            return;

        newNr = newNr.Trim();
        if (string.IsNullOrWhiteSpace(newNr))
        {
            await DisplayAlert("Fehler", "Zählernummer fehlt.", "OK");
            return;
        }

        var eichText = await DisplayPromptAsync("Zähler tauschen", "Eichdatum (z.B. 01.01.2024)", "OK", "Abbrechen", initialValue: DateTime.Today.ToString("d"));
        if (eichText == null)
            return;

        if (!DateTime.TryParse(eichText.Trim(), out var eichDatum))
        {
            await DisplayAlert("Fehler", "Eichdatum ist ungültig.", "OK");
            return;
        }

        SetBusy(true);
        try
        {
            var existing = await _supabaseService.GetActiveStromzaehlerAsync(parzelleId, DateTime.Today);
            if (existing != null)
            {
                var outOk = await _supabaseService.SetStromzaehlerAusgebautAmAsync(existing.Id, DateTime.Today);
                if (!outOk)
                {
                    await DisplayAlert("Fehler", "Alter Zähler konnte nicht ausgebaut werden.", "OK");
                    return;
                }
            }

            var addOk = await _supabaseService.AddStromzaehlerAsync(parzelleId, newNr, eichDatum.Date, DateTime.Today);
            if (!addOk)
            {
                await DisplayAlert("Fehler", "Neuer Zähler konnte nicht angelegt werden.", "OK");
                return;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", ex.Message, "OK");
        }
        finally
        {
            SetBusy(false);
        }

        await EnsureInitializedAsync();
    }

    private sealed record ParzelleOption(int ParzelleId, string GartenNr, string? Anlage)
    {
        public string Display => string.IsNullOrWhiteSpace(Anlage) ? GartenNr : $"{GartenNr} ({Anlage})";
    }
}

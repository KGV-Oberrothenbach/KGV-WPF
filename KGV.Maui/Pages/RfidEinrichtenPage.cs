using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Maui.Services;

namespace KGV.Maui.Pages;

public sealed class RfidEinrichtenPage : ContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly IRfidScanService _rfidScanService;

    private bool _isBusy;

    private readonly Entry _search;
    private readonly Switch _showAll;
    private readonly CollectionView _parzellenList;
    private readonly Picker _medium;
    private readonly Entry _uid;
    private readonly Button _scan;
    private readonly Button _check;
    private readonly Button _save;
    private readonly ActivityIndicator _busy;
    private readonly Label _status;
    private readonly Label _nfcStatus;

    private List<ParzelleRecord> _parzellenOhneTag = new();
    private List<ParzelleRecord> _parzellenAlle = new();
    private ParzelleRecord? _selectedParzelle;

    private bool _checkOk;
    private bool _replaceExisting;

    public RfidEinrichtenPage(ISupabaseService supabaseService, IRfidScanService rfidScanService)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _rfidScanService = rfidScanService ?? throw new ArgumentNullException(nameof(rfidScanService));

        Title = "RFID einrichten";

        _search = new Entry { Placeholder = "Parzelle suchen (Garten-Nr./Anlage)" };
        _search.TextChanged += (_, _) => ApplyFilter();

        _showAll = new Switch { IsToggled = false };
        _showAll.Toggled += OnShowAllToggled;

        _parzellenList = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            HeightRequest = 320,
            ItemTemplate = new DataTemplate(() =>
            {
                var title = new Label { FontAttributes = FontAttributes.Bold };
                title.SetBinding(Label.TextProperty, nameof(ParzelleRecord.GartenNr), stringFormat: "Garten {0}");

                var sub = new Label { Opacity = 0.8, FontSize = 12 };
                sub.SetBinding(Label.TextProperty, nameof(ParzelleRecord.Anlage));

                var rfid = new Label { Opacity = 0.8, FontSize = 12 };
                rfid.SetBinding(Label.TextProperty, new MultiBinding
                {
                    StringFormat = "Strom: {0} | Wasser: {1}",
                    Bindings =
                    {
                        new Binding(nameof(ParzelleRecord.RfidStrom)),
                        new Binding(nameof(ParzelleRecord.RfidWasser))
                    }
                });

                return new Border
                {
                    Stroke = Colors.LightGray,
                    StrokeThickness = 1,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                    Padding = 12,
                    Content = new VerticalStackLayout
                    {
                        Spacing = 2,
                        Children = { title, sub, rfid }
                    }
                };
            })
        };
        _parzellenList.SelectionChanged += OnParzelleSelected;

        _medium = new Picker { Title = "Medium" };
        _medium.Items.Add("Wasser");
        _medium.Items.Add("Strom");
        _medium.SelectedIndex = 0;
        _medium.SelectedIndexChanged += (_, _) => ResetCheckState();

        _uid = new Entry { Placeholder = "RFID-UID" };
        _uid.HorizontalOptions = LayoutOptions.FillAndExpand;
        _uid.TextChanged += (_, _) => ResetCheckState();

        _scan = new Button { Text = "NFC scannen" };
        _scan.Clicked += OnScanClicked;

        _check = new Button { Text = "Prüfen" };
        _check.Clicked += OnCheckClicked;

        _save = new Button { Text = "Speichern" };
        _save.Clicked += OnSaveClicked;

        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };
        _status = new Label { TextColor = Colors.Red };
        _nfcStatus = new Label { Opacity = 0.8, FontSize = 12, TextColor = Colors.Gray };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 12,
                Children =
                {
                    new Label { Text = "RFID einrichten", FontSize = 24, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "Parzelle wählen, Medium wählen, UID scannen/eingeben, prüfen und speichern.", Opacity = 0.8 },

                    new Label { Text = "1) Parzelle" , FontAttributes = FontAttributes.Bold },
                    new HorizontalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            new Label { Text = "Nur Gärten ohne Tag", VerticalOptions = LayoutOptions.Center },
                            _showAll,
                            new Label { Text = "Alle Gärten", VerticalOptions = LayoutOptions.Center }
                        }
                    },
                    _search,
                    _parzellenList,

                    new Label { Text = "2) Medium", FontAttributes = FontAttributes.Bold },
                    _medium,

                    new Label { Text = "3) UID", FontAttributes = FontAttributes.Bold },
                    new HorizontalStackLayout
                    {
                        Spacing = 12,
                        Children = { _uid, _scan }
                    },
                    _nfcStatus,

                    new HorizontalStackLayout
                    {
                        Spacing = 12,
                        Children = { _check, _save }
                    },

                    _busy,
                    _status
                }
            }
        };

        Appearing += OnAppearing;
        Disappearing += OnDisappearing;

        UpdateUiState();
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        _rfidScanService.TagScanned += OnTagScanned;
        UpdateNfcUi();

        if (_parzellenOhneTag.Count > 0 || _parzellenAlle.Count > 0)
        {
            ApplyFilter();
            return;
        }

        await LoadParzellenAsync(force: false);
    }

    private void OnDisappearing(object? sender, EventArgs e)
    {
        _rfidScanService.TagScanned -= OnTagScanned;
        _rfidScanService.StopListening();
        _nfcStatus.Text = string.Empty;
    }

    private async Task LoadParzellenAsync(bool force)
    {
        if (_isBusy) return;

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            if (force || _parzellenOhneTag.Count == 0)
                _parzellenOhneTag = await _supabaseService.GetParzellenForRfidSetupAsync();

            if (_showAll.IsToggled && (force || _parzellenAlle.Count == 0))
                _parzellenAlle = await _supabaseService.GetAllParzellenAsync();

            ApplyFilter();
        }
        catch (Exception ex)
        {
            _status.Text = $"Fehler: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ApplyFilter()
    {
        var q = (_search.Text ?? string.Empty).Trim();

        IEnumerable<ParzelleRecord> filtered = _showAll.IsToggled ? _parzellenAlle : _parzellenOhneTag;
        if (!string.IsNullOrWhiteSpace(q))
        {
            filtered = filtered.Where(p =>
                (p.GartenNr ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase)
                || (p.Anlage ?? string.Empty).Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        _parzellenList.ItemsSource = filtered.ToList();
    }

    private void OnParzelleSelected(object? sender, SelectionChangedEventArgs e)
    {
        _selectedParzelle = e.CurrentSelection?.FirstOrDefault() as ParzelleRecord;
        ResetCheckState();
        UpdateUiState();
    }

    private void ResetCheckState()
    {
        _checkOk = false;
        _replaceExisting = false;
        _status.Text = string.Empty;
    }

    private async void OnShowAllToggled(object? sender, ToggledEventArgs e)
    {
        ResetCheckState();
        _selectedParzelle = null;
        _parzellenList.SelectedItem = null;

        if (e.Value && _parzellenAlle.Count == 0)
            await LoadParzellenAsync(force: false);
        else
            ApplyFilter();

        UpdateUiState();
    }

    private async void OnScanClicked(object? sender, EventArgs e)
    {
        if (_isBusy)
            return;

        if (!_rfidScanService.IsSupported)
        {
            await DisplayAlert("NFC", "Dieses Gerät unterstützt kein NFC.", "OK");
            UpdateNfcUi();
            return;
        }

        if (!_rfidScanService.IsEnabled)
        {
            var open = await DisplayAlert("NFC deaktiviert", "NFC ist deaktiviert. Bitte NFC aktivieren und dann erneut scannen.", "Einstellungen", "Abbrechen");
            if (open)
                _rfidScanService.OpenNfcSettings();
            UpdateNfcUi();
            return;
        }

        _rfidScanService.StartListening();
        _nfcStatus.Text = "Scan aktiv – Tag an das Gerät halten.";
    }

    private void OnTagScanned(object? sender, string uid)
    {
        _uid.Text = uid;
        _nfcStatus.Text = "Tag erkannt.";
        _rfidScanService.StopListening();
        ResetCheckState();
    }

    private void UpdateNfcUi()
    {
        if (!_rfidScanService.IsSupported)
        {
            _scan.IsEnabled = false;
            _nfcStatus.Text = "NFC wird von diesem Gerät nicht unterstützt.";
            return;
        }

        _scan.IsEnabled = !_isBusy;
        if (!_rfidScanService.IsEnabled)
            _nfcStatus.Text = "NFC ist deaktiviert.";
    }

    private void UpdateUiState()
    {
        var hasParzelle = _selectedParzelle != null;
        _medium.IsEnabled = !_isBusy && hasParzelle;
        _uid.IsEnabled = !_isBusy && hasParzelle;
        _scan.IsEnabled = !_isBusy && hasParzelle && _rfidScanService.IsSupported;
        _check.IsEnabled = !_isBusy && hasParzelle;
        _save.IsEnabled = !_isBusy && hasParzelle;

        if (!_rfidScanService.IsSupported)
            _nfcStatus.Text = "NFC wird von diesem Gerät nicht unterstützt.";
        else if (!_rfidScanService.IsEnabled)
            _nfcStatus.Text = "NFC ist deaktiviert.";
    }

    private static bool IsUidValid(string uid)
    {
        uid = (uid ?? string.Empty).Trim();
        if (uid.Length < 4 || uid.Length > 64) return false;

        foreach (var ch in uid)
        {
            if (!Uri.IsHexDigit(ch))
                return false;
        }

        return true;
    }

    private bool TryResolveZaehlerTyp(out short typ)
    {
        typ = 0;
        var sel = _medium.SelectedItem?.ToString() ?? string.Empty;
        if (sel.Equals("strom", StringComparison.OrdinalIgnoreCase)) { typ = 1; return true; }
        if (sel.Equals("wasser", StringComparison.OrdinalIgnoreCase)) { typ = 2; return true; }
        return false;
    }

    private string GetExistingRfid(short typ)
    {
        if (_selectedParzelle == null) return string.Empty;

        return typ switch
        {
            1 => (_selectedParzelle.RfidStrom ?? string.Empty).Trim(),
            2 => (_selectedParzelle.RfidWasser ?? string.Empty).Trim(),
            _ => string.Empty
        };
    }

    private static string MediumText(short typ) => typ == 1 ? "Strom" : "Wasser";

    private bool IsSelfAssignment(RfidUidAssignmentInfo info, short typ)
        => _selectedParzelle != null && info.ParzelleId == _selectedParzelle.Id && info.ZaehlerTyp == typ;

    private async void OnCheckClicked(object? sender, EventArgs e)
    {
        if (_isBusy) return;

        if (_selectedParzelle == null)
        {
            _status.Text = "Bitte eine Parzelle auswählen.";
            return;
        }

        if (!TryResolveZaehlerTyp(out var typ))
        {
            _status.Text = "Bitte Medium auswählen.";
            return;
        }

        var uid = (_uid.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(uid))
        {
            _status.Text = "Bitte eine UID eingeben.";
            return;
        }

        if (!IsUidValid(uid))
        {
            _status.Text = "UID ist ungültig.";
            return;
        }

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            var conflict = await _supabaseService.FindRfidUidAssignmentAsync(uid);
            if (conflict != null && !IsSelfAssignment(conflict, typ))
            {
                var medium = conflict.ZaehlerTyp == 1 ? "Strom" : "Wasser";
                _status.Text = $"UID ist bereits vergeben: Anlage '{conflict.Anlage}', Garten '{conflict.GartenNr}', Medium {medium}.";
                _checkOk = false;
                return;
            }

            var existing = GetExistingRfid(typ);
            if (!string.IsNullOrWhiteSpace(existing))
            {
                if (!_showAll.IsToggled)
                {
                    _status.Text = $"Diese Parzelle hat für {MediumText(typ)} bereits eine RFID. In der Ansicht 'nur ohne Tag' ist Speichern blockiert.";
                    _checkOk = false;
                    return;
                }

                var replace = await DisplayAlert(
                    "RFID ersetzen?",
                    $"Für {MediumText(typ)} ist bereits eine RFID hinterlegt ({existing}).\n\nSoll die RFID bewusst ersetzt werden?",
                    "Ersetzen",
                    "Abbrechen");

                if (!replace)
                {
                    _status.Text = "Ersetzen abgebrochen.";
                    _checkOk = false;
                    return;
                }

                _replaceExisting = true;
            }

            _status.Text = _replaceExisting
                ? "OK – UID ist frei und die bestehende RFID wird ersetzt."
                : "OK – UID ist frei und kann gespeichert werden.";
            _checkOk = true;
        }
        catch (Exception ex)
        {
            _status.Text = $"Fehler: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_isBusy) return;
        if (!_checkOk)
        {
            _status.Text = "Bitte zuerst prüfen.";
            return;
        }

        if (_selectedParzelle == null)
        {
            _status.Text = "Bitte eine Parzelle auswählen.";
            return;
        }

        if (!TryResolveZaehlerTyp(out var typ))
        {
            _status.Text = "Bitte Medium auswählen.";
            return;
        }

        var uid = (_uid.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(uid))
        {
            _status.Text = "Bitte eine UID eingeben.";
            return;
        }

        if (!IsUidValid(uid))
        {
            _status.Text = "UID ist ungültig.";
            return;
        }

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            var conflict = await _supabaseService.FindRfidUidAssignmentAsync(uid);
            if (conflict != null && !IsSelfAssignment(conflict, typ))
            {
                var medium = conflict.ZaehlerTyp == 1 ? "Strom" : "Wasser";
                _status.Text = $"UID ist bereits vergeben: Anlage '{conflict.Anlage}', Garten '{conflict.GartenNr}', Medium {medium}.";
                _checkOk = false;
                return;
            }

            var existing = GetExistingRfid(typ);
            if (!string.IsNullOrWhiteSpace(existing))
            {
                if (!_showAll.IsToggled)
                {
                    _status.Text = $"Diese Parzelle hat für {MediumText(typ)} bereits eine RFID. In der Ansicht 'nur ohne Tag' ist Speichern blockiert.";
                    _checkOk = false;
                    return;
                }

                if (!_replaceExisting)
                {
                    _status.Text = $"Für {MediumText(typ)} ist bereits eine RFID hinterlegt. Bitte erneut prüfen und Ersetzen bestätigen.";
                    _checkOk = false;
                    return;
                }
            }

            var ok = await _supabaseService.SetParzelleRfidAsync(_selectedParzelle.Id, typ, uid);
            if (!ok)
            {
                _status.Text = "Speichern fehlgeschlagen.";
                _checkOk = false;
                return;
            }

            await DisplayAlert(
                "Erfolg",
                $"RFID gespeichert: Garten '{_selectedParzelle.GartenNr}' ({_selectedParzelle.Anlage}), Medium {MediumText(typ)}.",
                "OK");

            _uid.Text = string.Empty;
            _checkOk = false;
            _replaceExisting = false;

            await LoadParzellenAsync(force: true);
        }
        catch (Exception ex)
        {
            _status.Text = $"Fehler: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        _busy.IsVisible = busy;
        _busy.IsRunning = busy;

        _search.IsEnabled = !busy;
        _parzellenList.IsEnabled = !busy;
        _showAll.IsEnabled = !busy;
        UpdateUiState();
        UpdateNfcUi();
    }
}

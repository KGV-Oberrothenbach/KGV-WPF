using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Maui.Services;

namespace KGV.Maui.Pages;

public sealed class AblesungErfassenPage : FooterContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly IRfidScanService _rfidScanService;

    private bool _isBusy;

    private readonly Entry _rfid;
    private readonly Button _scan;
    private readonly Button _check;
    private readonly ActivityIndicator _busy;
    private readonly Label _status;
    private readonly Label _nfcStatus;

    public AblesungErfassenPage(ISupabaseService supabaseService, IRfidScanService rfidScanService)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _rfidScanService = rfidScanService ?? throw new ArgumentNullException(nameof(rfidScanService));

        Title = "Ablesung erfassen";

        _rfid = new Entry { Placeholder = "RFID-UID" };
        _rfid.HorizontalOptions = LayoutOptions.FillAndExpand;
        _scan = new Button { Text = "NFC scannen" };
        _scan.Clicked += OnScanClicked;
        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };
        _status = new Label { TextColor = Colors.Red };
        _nfcStatus = new Label { Opacity = 0.8, FontSize = 12, TextColor = Colors.Gray };

        _check = new Button { Text = "Prüfen" };
        _check.Clicked += OnCheckClicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Ablesung erfassen", FontSize = 24, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "RFID-Tag scannen oder UID eingeben und prüfen.", Opacity = 0.8 },
                    new HorizontalStackLayout
                    {
                        Spacing = 12,
                        Children = { _rfid, _scan }
                    },
                    _nfcStatus,
                    _check,
                    _busy,
                    _status
                }
            }
        };

        Appearing += OnAppearing;
        Disappearing += OnDisappearing;
        UpdateNfcUi();
    }

    private void OnAppearing(object? sender, EventArgs e)
    {
        _rfidScanService.TagScanned += OnTagScanned;
        UpdateNfcUi();
    }

    private void OnDisappearing(object? sender, EventArgs e)
    {
        _rfidScanService.TagScanned -= OnTagScanned;
        _rfidScanService.StopListening();
        _nfcStatus.Text = string.Empty;
    }

    private async void OnScanClicked(object? sender, EventArgs e)
    {
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
        _rfid.Text = uid;
        _nfcStatus.Text = "Tag erkannt.";
        _rfidScanService.StopListening();
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

    private async void OnCheckClicked(object? sender, EventArgs e)
    {
        if (_isBusy) return;

        var uid = (_rfid.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(uid))
        {
            _status.Text = "Bitte eine UID eingeben.";
            return;
        }

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            var ctx = await _supabaseService.GetRfidScanContextAsync(uid);
            if (ctx == null)
            {
                _status.Text = "UID ist keiner Parzelle zugeordnet.";
                return;
            }

            await Shell.Current.Navigation.PushAsync(new RfidScanContextPage(_supabaseService, ctx));
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

        _rfid.IsEnabled = !busy;
        _scan.IsEnabled = !busy;
        _check.IsEnabled = !busy;
    }
}

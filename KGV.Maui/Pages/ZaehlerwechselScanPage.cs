using KGV.Core.Interfaces;
using KGV.Core.Models;

namespace KGV.Maui.Pages;

public sealed class ZaehlerwechselScanPage : ContentPage
{
    private readonly ISupabaseService _supabaseService;

    private bool _isBusy;

    private readonly Entry _rfid;
    private readonly Button _check;
    private readonly ActivityIndicator _busy;
    private readonly Label _status;

    public ZaehlerwechselScanPage(ISupabaseService supabaseService)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));

        Title = "Zählerwechsel";

        _rfid = new Entry { Placeholder = "RFID-UID" };
        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };
        _status = new Label { TextColor = Colors.Red };

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
                    new Label { Text = "Zählerwechsel", FontSize = 24, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "RFID-Tag scannen oder UID eingeben und prüfen.", Opacity = 0.8 },
                    _rfid,
                    _check,
                    _busy,
                    _status
                }
            }
        };
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

            if (ctx.AktiverZaehlerId.HasValue)
                await Shell.Current.Navigation.PushAsync(new ZaehlerwechselAusbauPage(_supabaseService, ctx));
            else
                await Shell.Current.Navigation.PushAsync(new ZaehlerwechselEinbauPage(_supabaseService, ctx));
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
        _check.IsEnabled = !busy;
    }
}

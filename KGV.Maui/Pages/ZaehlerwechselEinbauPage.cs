using KGV.Core.Interfaces;
using KGV.Core.Models;

namespace KGV.Maui.Pages;

public sealed class ZaehlerwechselEinbauPage : ContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly RfidScanContextRecord _ctx;

    private bool _isBusy;

    private readonly Entry _zaehlernummer;
    private readonly DatePicker _eichdatum;
    private readonly DatePicker _eingebautAm;
    private readonly Button _save;

    private readonly ActivityIndicator _busy;
    private readonly Label _status;

    public ZaehlerwechselEinbauPage(ISupabaseService supabaseService, RfidScanContextRecord ctx)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));

        Title = "Zählerwechsel – Einbau";

        _zaehlernummer = new Entry { Placeholder = "Zählernummer" };
        _eichdatum = new DatePicker { Date = DateTime.Today };
        _eingebautAm = new DatePicker { Date = DateTime.Today };
        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };
        _status = new Label { TextColor = Colors.Red };

        _save = new Button { Text = "Einbau speichern" };
        _save.Clicked += OnSaveClicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Zählerwechsel – Einbau", FontSize = 24, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "Kein aktiver Zähler vorhanden. Bitte neuen Zähler erfassen.", Opacity = 0.8 },

                    BuildCard(
                        ("Anlage", _ctx.Anlage),
                        ("Garten-Nr.", _ctx.GartenNr?.ToString()),
                        ("Medium", _ctx.Medium),
                        ("RFID", _ctx.RfidTagUid)),

                    new Border
                    {
                        Stroke = Colors.LightGray,
                        StrokeThickness = 1,
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                        Padding = 14,
                        Content = new VerticalStackLayout
                        {
                            Spacing = 10,
                            Children =
                            {
                                new Label { Text = "Einbau", FontAttributes = FontAttributes.Bold, FontSize = 18 },
                                _zaehlernummer,
                                new Label { Text = "Eichdatum", FontAttributes = FontAttributes.Bold, FontSize = 12, Opacity = 0.8 },
                                _eichdatum,
                                new Label { Text = "Einbaudatum", FontAttributes = FontAttributes.Bold, FontSize = 12, Opacity = 0.8 },
                                _eingebautAm,
                                _save,
                                _busy,
                                _status
                            }
                        }
                    }
                }
            }
        };
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_isBusy) return;

        if (_ctx.AktiverZaehlerId.HasValue)
        {
            _status.Text = "Es ist bereits ein aktiver Zähler vorhanden.";
            return;
        }

        var nummer = (_zaehlernummer.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nummer))
        {
            _status.Text = "Bitte eine Zählernummer eingeben.";
            return;
        }

        if (!TryResolveZaehlerTyp(_ctx.Medium, out var typ))
        {
            _status.Text = "Medium konnte nicht zugeordnet werden.";
            return;
        }

        if (!_ctx.ParzelleId.HasValue || _ctx.ParzelleId.Value <= 0 || _ctx.ParzelleId.Value > int.MaxValue)
        {
            _status.Text = "Ungültige Parzellen-ID.";
            return;
        }

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            var parzelleId = (int)_ctx.ParzelleId.Value;
            var ok = typ switch
            {
                1 => await _supabaseService.AddStromzaehlerAsync(parzelleId, nummer, _eichdatum.Date.Date, _eingebautAm.Date.Date),
                2 => await _supabaseService.AddWasserzaehlerAsync(parzelleId, nummer, _eichdatum.Date.Date, _eingebautAm.Date.Date),
                _ => false
            };

            if (!ok)
            {
                _status.Text = "Einbau konnte nicht gespeichert werden.";
                return;
            }

            await DisplayAlert("Erfolg", "Zähler eingebaut.", "OK");
            await Shell.Current.Navigation.PopAsync();
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

        _zaehlernummer.IsEnabled = !busy;
        _eichdatum.IsEnabled = !busy;
        _eingebautAm.IsEnabled = !busy;
        _save.IsEnabled = !busy;
    }

    private static bool TryResolveZaehlerTyp(string? medium, out short typ)
    {
        typ = 0;
        medium = (medium ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(medium)) return false;

        if (medium.Contains("strom", StringComparison.OrdinalIgnoreCase)) { typ = 1; return true; }
        if (medium.Contains("wasser", StringComparison.OrdinalIgnoreCase)) { typ = 2; return true; }
        return false;
    }

    private static View BuildCard(params (string Label, string? Value)[] rows)
    {
        var stack = new VerticalStackLayout { Spacing = 6 };

        foreach (var (label, value) in rows)
        {
            var text = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                continue;

            stack.Children.Add(new Label { Text = label, FontAttributes = FontAttributes.Bold, FontSize = 12, Opacity = 0.8 });
            stack.Children.Add(new Label { Text = text, LineBreakMode = LineBreakMode.WordWrap });
        }

        return new Border
        {
            Stroke = Colors.LightGray,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Padding = 14,
            Content = stack
        };
    }
}

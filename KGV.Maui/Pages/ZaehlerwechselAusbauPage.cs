using KGV.Core.Interfaces;
using KGV.Core.Models;
using Microsoft.Maui.Storage;
using System.Globalization;

namespace KGV.Maui.Pages;

public sealed class ZaehlerwechselAusbauPage : FooterContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly RfidScanContextRecord _ctx;

    private bool _isBusy;
    private string? _fotoPfad;

    private readonly Entry _endstand;
    private readonly DatePicker _ausbauDatum;
    private readonly Button _pickFoto;
    private readonly Button _save;
    private readonly ActivityIndicator _busy;
    private readonly Label _status;
    private readonly Label _fotoLabel;

    public ZaehlerwechselAusbauPage(ISupabaseService supabaseService, RfidScanContextRecord ctx)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));

        Title = "Zählerwechsel – Ausbau";

        _endstand = new Entry { Placeholder = "Endstand", Keyboard = Keyboard.Numeric };
        _ausbauDatum = new DatePicker { Date = DateTime.Today };
        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };
        _status = new Label { TextColor = Colors.Red };
        _fotoLabel = new Label { Opacity = 0.8 };

        _pickFoto = new Button { Text = "Foto auswählen (lokal)" };
        _pickFoto.Clicked += OnPickFotoClicked;

        _save = new Button { Text = "Ausbau speichern" };
        _save.Clicked += OnSaveClicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Zählerwechsel – Ausbau", FontSize = 24, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "Aktiver Zähler gefunden. Bitte Endstand und Ausbaudatum erfassen.", Opacity = 0.8 },

                    BuildCard(
                        ("Anlage", _ctx.Anlage),
                        ("Garten-Nr.", _ctx.GartenNr?.ToString()),
                        ("Medium", _ctx.Medium),
                        ("RFID", _ctx.RfidTagUid),
                        ("Zählernummer", _ctx.Zaehlernummer),
                        ("Eingebaut am", FormatDate(_ctx.EingebautAm)),
                        ("Eichfällig am", FormatDate(_ctx.EichfaelligAm))),

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
                                new Label { Text = "Ausbau", FontAttributes = FontAttributes.Bold, FontSize = 18 },
                                _endstand,
                                new Label { Text = "Ausbaudatum", FontAttributes = FontAttributes.Bold, FontSize = 12, Opacity = 0.8 },
                                _ausbauDatum,
                                _pickFoto,
                                _fotoLabel,
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

    private async void OnPickFotoClicked(object? sender, EventArgs e)
    {
        if (_isBusy) return;

        try
        {
            var res = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Foto auswählen (lokal)" });
            _fotoPfad = res?.FullPath;
            _fotoLabel.Text = string.IsNullOrWhiteSpace(_fotoPfad) ? string.Empty : _fotoPfad;
        }
        catch
        {
            // optional
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_isBusy) return;

        if (!_ctx.AktiverZaehlerId.HasValue)
        {
            _status.Text = "Kein aktiver Zähler vorhanden.";
            return;
        }

        if (!TryResolveZaehlerTyp(_ctx.Medium, out var typ))
        {
            _status.Text = "Medium konnte nicht zugeordnet werden.";
            return;
        }

        var s = (_endstand.Text ?? string.Empty).Trim();
        if (!decimal.TryParse(s, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"), out var stand) || stand < 0)
        {
            _status.Text = "Bitte einen gültigen, nicht-negativen Endstand eingeben.";
            return;
        }

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            var datum = _ausbauDatum.Date.Date;
            var zaehlerId = _ctx.AktiverZaehlerId.Value;

            // 1) Endstand als Ablesung speichern
            var res = await _supabaseService.AddAblesungResultAsync(typ, zaehlerId, datum, stand, string.IsNullOrWhiteSpace(_fotoPfad) ? null : _fotoPfad);
            if (!res.Ok)
            {
                _status.Text = string.IsNullOrWhiteSpace(res.Message) ? "Endstand konnte nicht gespeichert werden." : res.Message;
                return;
            }

            // 2) aktiven Zähler als ausgebaut markieren
            var okAusbau = typ switch
            {
                1 => await _supabaseService.SetStromzaehlerAusgebautAmAsync(zaehlerId, datum),
                2 => await _supabaseService.SetWasserzaehlerAusgebautAmAsync(zaehlerId, datum),
                _ => false
            };

            if (!okAusbau)
            {
                _status.Text = "Zähler konnte nicht als ausgebaut markiert werden.";
                return;
            }

            await DisplayAlert("Erfolg", "Zähler ausgebaut.", "OK");
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

        _endstand.IsEnabled = !busy;
        _ausbauDatum.IsEnabled = !busy;
        _pickFoto.IsEnabled = !busy;
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

    private static string? FormatDate(DateTime? dt)
        => dt.HasValue ? dt.Value.ToString("dd.MM.yyyy") : null;

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

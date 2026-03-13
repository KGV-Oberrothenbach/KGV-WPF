using KGV.Core.Interfaces;
using KGV.Core.Models;
using Microsoft.Maui.Storage;
using System.Globalization;

namespace KGV.Maui.Pages;

public sealed class RfidScanContextPage : FooterContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly RfidScanContextRecord _ctx;

    private bool _isBusy;
    private string? _fotoPfad;

    private readonly Entry _stand;
    private Button? _pickFoto;
    private Button? _save;
    private readonly Label _status;
    private readonly ActivityIndicator _busy;
    private Label? _fotoLabel;

    public RfidScanContextPage(ISupabaseService supabaseService, RfidScanContextRecord ctx)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));

        var hasAktiverZaehler = _ctx.AktiverZaehlerId.HasValue;

        Title = hasAktiverZaehler ? "Ablesung erfassen" : "Kein aktiver Zähler";

        var garten = _ctx.GartenNr.HasValue ? $"Garten {_ctx.GartenNr.Value}" : "(Garten unbekannt)";
        var medium = string.IsNullOrWhiteSpace(_ctx.Medium) ? "(Medium unbekannt)" : _ctx.Medium;

        var message = hasAktiverZaehler
            ? $"RFID erkannt: {garten} • {medium}. Aktiver Zähler gefunden."
            : $"RFID erkannt: {garten} • {medium}, aber aktuell ist kein aktiver Zähler vorhanden.";

        _stand = new Entry { Placeholder = "Neuer Zählerstand", Keyboard = Keyboard.Numeric };
        _status = new Label { TextColor = Colors.Red };
        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };

        var root = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 12,
            Children =
            {
                new Label { Text = Title, FontSize = 24, FontAttributes = FontAttributes.Bold },
                new Label { Text = message, Opacity = 0.8, LineBreakMode = LineBreakMode.WordWrap },

                BuildCard(
                    ("Anlage", _ctx.Anlage),
                    ("Garten-Nr.", _ctx.GartenNr?.ToString()),
                    ("Medium", _ctx.Medium),
                    ("RFID", _ctx.RfidTagUid),
                    ("Zählernummer", hasAktiverZaehler ? _ctx.Zaehlernummer : null),
                    ("Eingebaut am", FormatDate(_ctx.EingebautAm)),
                    ("Eichfällig am", FormatDate(_ctx.EichfaelligAm)))
            }
        };

        if (hasAktiverZaehler)
        {
            _pickFoto = new Button { Text = "Foto auswählen (lokal)" };
            _pickFoto.Clicked += OnPickFotoClicked;

            _save = new Button { Text = "Speichern" };
            _save.Clicked += OnSaveClicked;

            _fotoLabel = new Label { Opacity = 0.8 };

            root.Children.Add(new Border
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
                        new Label { Text = "Neue Ablesung", FontAttributes = FontAttributes.Bold, FontSize = 18 },
                        _stand,
                        _pickFoto,
                        _fotoLabel,
                        _save,
                        _busy,
                        _status
                    }
                }
            });

            _fotoLabel.Text = string.IsNullOrWhiteSpace(_fotoPfad) ? string.Empty : _fotoPfad;
        }

        Content = new ScrollView { Content = root };
    }

    private async void OnPickFotoClicked(object? sender, EventArgs e)
    {
        if (_isBusy) return;

        try
        {
            var res = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Foto auswählen (lokal)"
            });

            _fotoPfad = res?.FullPath;
            if (_fotoLabel != null)
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
        if (!_ctx.AktiverZaehlerId.HasValue) return;

        if (!TryResolveZaehlerTyp(_ctx.Medium, out var typ))
        {
            _status.Text = "Medium konnte nicht zugeordnet werden.";
            return;
        }

        var s = (_stand.Text ?? string.Empty).Trim();
        if (!decimal.TryParse(s, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"), out var stand) || stand < 0)
        {
            _status.Text = "Bitte einen gültigen, nicht-negativen Zählerstand eingeben.";
            return;
        }

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            var res = await _supabaseService.AddAblesungResultAsync(typ, _ctx.AktiverZaehlerId.Value, DateTime.Now, stand, string.IsNullOrWhiteSpace(_fotoPfad) ? null : _fotoPfad);
            if (res.Ok)
            {
                _stand.Text = string.Empty;
                await DisplayAlert("Erfolg", res.Message, "OK");
            }
            else
            {
                _status.Text = string.IsNullOrWhiteSpace(res.Message) ? "Speichern fehlgeschlagen." : res.Message;
            }
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

        _stand.IsEnabled = !busy;
        if (_pickFoto != null) _pickFoto.IsEnabled = !busy;
        if (_save != null) _save.IsEnabled = !busy;
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

    private static string? FormatDate(DateTime? dt)
        => dt.HasValue ? dt.Value.ToString("dd.MM.yyyy") : null;
}

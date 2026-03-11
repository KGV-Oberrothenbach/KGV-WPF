using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Maui.State;
using System.Linq;
using System.Text.RegularExpressions;

namespace KGV.Maui.Pages;

public sealed class MyProfilePage : ContentPage
{
    private static readonly Regex PlzRegex = new("^\\d{5}$", RegexOptions.Compiled);

    private readonly ISupabaseService _supabaseService;
    private readonly UserContextState _state;

    private MitgliedRecord? _member;
    private bool _isBusy;
    private Task? _loadTask;

    private readonly ActivityIndicator _busyIndicator;
    private readonly Label _nameLabel;
    private readonly Label _emailLabel;

    private readonly Entry _telefonEntry;
    private readonly Entry _handyEntry;
    private readonly Entry _adresseEntry;
    private readonly Entry _plzEntry;
    private readonly Entry _ortEntry;

    private readonly Label _statusLabel;
    private readonly Button _saveButton;
    private readonly Button _checkAddressButton;

    public MyProfilePage(ISupabaseService supabaseService, UserContextState state)
    {
        _supabaseService = supabaseService;
        _state = state;

        Title = "Meine Stammdaten";

        _busyIndicator = new ActivityIndicator { IsRunning = false, IsVisible = false };

        _nameLabel = new Label { FontSize = 22, FontAttributes = FontAttributes.Bold };
        _emailLabel = new Label();

        _telefonEntry = new Entry { Placeholder = "Telefon" };
        _handyEntry = new Entry { Placeholder = "Handy" };

        _adresseEntry = new Entry { Placeholder = "Adresse (Pflicht)" };
        _plzEntry = new Entry { Placeholder = "PLZ (Pflicht)", Keyboard = Keyboard.Numeric };
        _ortEntry = new Entry { Placeholder = "Ort (Pflicht)" };

        _statusLabel = new Label { TextColor = Colors.Red };

        _checkAddressButton = new Button { Text = "Adresse prüfen" };
        _checkAddressButton.Clicked += OnCheckAddressClicked;

        if (Application.Current?.Resources != null && Application.Current.Resources.TryGetValue("AccentButton", out var accentStyle) && accentStyle is Style s1)
            _checkAddressButton.Style = s1;

        _saveButton = new Button { Text = "Speichern" };
        _saveButton.Clicked += OnSaveClicked;

        object? cardStyleObj = null;
        if (Application.Current?.Resources != null)
            Application.Current.Resources.TryGetValue("Card", out cardStyleObj);
        var cardStyle = cardStyleObj as Style;

        object? entryBorderStyleObj = null;
        if (Application.Current?.Resources != null)
            Application.Current.Resources.TryGetValue("EntryBorder", out entryBorderStyleObj);
        var entryBorderStyle = entryBorderStyleObj as Style;

        object? readOnlyStyleObj = null;
        if (Application.Current?.Resources != null)
            Application.Current.Resources.TryGetValue("ReadOnlyField", out readOnlyStyleObj);
        var readOnlyStyle = readOnlyStyleObj as Style;

        Border WrapEntry(Entry entry)
            => entryBorderStyle != null
                ? new Border { Style = entryBorderStyle, Content = entry }
                : new Border { Content = entry };

        Border WrapCard(View content)
            => cardStyle != null
                ? new Border { Style = cardStyle, Content = content }
                : new Border { Content = content };

        var header = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                _nameLabel,
                readOnlyStyle != null ? new Border { Style = readOnlyStyle, Content = _emailLabel } : _emailLabel,
                _statusLabel
            }
        };

        var kontakt = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Kontakt", FontAttributes = FontAttributes.Bold },
                WrapEntry(_telefonEntry),
                WrapEntry(_handyEntry)
            }
        };

        var adresse = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Adresse", FontAttributes = FontAttributes.Bold },
                WrapEntry(_adresseEntry),
                WrapEntry(_plzEntry),
                WrapEntry(_ortEntry)
            }
        };

        var actions = new HorizontalStackLayout
        {
            Spacing = 12,
            Children = { _checkAddressButton, _saveButton }
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 14,
                Children =
                {
                    _busyIndicator,
                    WrapCard(header),
                    WrapCard(kontakt),
                    WrapCard(adresse),
                    actions
                }
            }
        };

        Appearing += OnAppearing;
        Disappearing += (_, _) => _statusLabel.Text = string.Empty;

        UpdateUiState();
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        await EnsureLoadedAsync();
    }

    private Task EnsureLoadedAsync()
    {
        // Verhindert parallele Loads (z.B. schnelles Tab-Wechseln / mehrfaches Appearing)
        if (_loadTask != null && !_loadTask.IsCompleted)
            return _loadTask;

        _loadTask = LoadAsync();
        return _loadTask;
    }

    private async Task LoadAsync()
    {
        SetBusy(true);
        _statusLabel.Text = string.Empty;

        if (_state.CurrentUserId == null)
        {
            _member = null;
            _state.CurrentMitgliedId = null;
            ClearUi("Nicht angemeldet.");
            return;
        }

        try
        {
            var rec = await _supabaseService.GetMitgliedByAuthUserIdAsync(_state.CurrentUserId.Value);
            if (rec == null)
            {
                _member = null;
                _state.CurrentMitgliedId = null;
                ClearUi("Mitglied nicht gefunden.");
                return;
            }

            _member = rec;
            _state.CurrentMitgliedId = rec.Id;

            _nameLabel.Text = $"{rec.Vorname} {rec.Name}".Trim();
            _emailLabel.Text = rec.Email ?? string.Empty;

            _telefonEntry.Text = rec.Telefon ?? string.Empty;
            _handyEntry.Text = rec.Handy ?? string.Empty;

            _adresseEntry.Text = rec.Adresse ?? string.Empty;
            _plzEntry.Text = rec.Plz ?? string.Empty;
            _ortEntry.Text = rec.Ort ?? string.Empty;

            _statusLabel.Text = string.Empty;
        }
        catch (Exception ex)
        {
            _member = null;
            _state.CurrentMitgliedId = null;
            ClearUi(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_isBusy)
            return;

        if (_member == null)
        {
            await DisplayAlert("Fehler", "Mitglied ist nicht geladen.", "OK");
            return;
        }

        if (_state.CurrentUserId == null)
        {
            await DisplayAlert("Fehler", "Nicht angemeldet.", "OK");
            return;
        }

        // Schutz: nur eigene Daten (Kontext muss zu geladenem Datensatz passen)
        if (_state.CurrentMitgliedId.HasValue && _state.CurrentMitgliedId.Value != _member.Id)
        {
            await DisplayAlert("Fehler", "Inkonsistenter Mitgliedskontext. Bitte abmelden und erneut anmelden.", "OK");
            return;
        }

        var telefon = (_telefonEntry.Text ?? string.Empty).Trim();
        var handy = (_handyEntry.Text ?? string.Empty).Trim();
        var adresse = (_adresseEntry.Text ?? string.Empty).Trim();
        var plz = (_plzEntry.Text ?? string.Empty).Trim();
        var ort = (_ortEntry.Text ?? string.Empty).Trim();

        var error = Validate(adresse, plz, ort, telefon, handy);
        if (!string.IsNullOrEmpty(error))
        {
            await DisplayAlert("Ungültige Eingabe", error, "OK");
            return;
        }

        SetBusy(true);
        try
        {
            var ok = await _supabaseService.UpdateOwnContactAsync(_member.Id, EmptyToNull(telefon), EmptyToNull(handy), adresse, plz, ort);
            if (!ok)
            {
                await DisplayAlert("Fehler", "Speichern fehlgeschlagen.", "OK");
                return;
            }

            await DisplayAlert("OK", "Gespeichert.", "OK");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Fehler", ex.Message, "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnCheckAddressClicked(object? sender, EventArgs e)
    {
        // Stub: funktioniert auch ohne API-Key
        var adresse = (_adresseEntry.Text ?? string.Empty).Trim();
        var plz = (_plzEntry.Text ?? string.Empty).Trim();
        var ort = (_ortEntry.Text ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(adresse) || string.IsNullOrWhiteSpace(plz) || string.IsNullOrWhiteSpace(ort))
        {
            await DisplayAlert("Hinweis", "Bitte Adresse, PLZ und Ort ausfüllen.", "OK");
            return;
        }

        var okPlz = PlzRegex.IsMatch(plz);
        await DisplayAlert("Adresse prüfen", okPlz ? "Format wirkt plausibel." : "PLZ ist ungültig.", "OK");
    }

    private static string? Validate(string adresse, string plz, string ort, string telefon, string handy)
    {
        if (string.IsNullOrWhiteSpace(adresse)) return "Adresse ist Pflicht.";
        if (string.IsNullOrWhiteSpace(plz)) return "PLZ ist Pflicht.";
        if (!PlzRegex.IsMatch(plz)) return "PLZ muss 5-stellig sein (Regex ^\\d{5}$).";
        if (string.IsNullOrWhiteSpace(ort)) return "Ort ist Pflicht.";

        if (!IsValidPhone(telefon)) return "Telefon ist nicht plausibel.";
        if (!IsValidPhone(handy)) return "Handy ist nicht plausibel.";

        return null;
    }

    private static bool IsValidPhone(string value)
    {
        value = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(value)) return true; // optional

        // erlaubte Zeichen: Ziffern + Leerzeichen + + / - ( )
        foreach (var ch in value)
        {
            if (char.IsDigit(ch)) continue;
            if (ch is ' ' or '+' or '/' or '-' or '(' or ')') continue;
            return false;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length >= 6;
    }

    private static string? EmptyToNull(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        _busyIndicator.IsVisible = busy;
        _busyIndicator.IsRunning = busy;
        UpdateUiState();
    }

    private void UpdateUiState()
    {
        var hasMember = _member != null;
        var canEdit = !_isBusy && hasMember;

        _telefonEntry.IsEnabled = canEdit;
        _handyEntry.IsEnabled = canEdit;
        _adresseEntry.IsEnabled = canEdit;
        _plzEntry.IsEnabled = canEdit;
        _ortEntry.IsEnabled = canEdit;

        _saveButton.IsEnabled = canEdit;
        _checkAddressButton.IsEnabled = canEdit;
    }

    private void ClearUi(string message)
    {
        _nameLabel.Text = string.Empty;
        _emailLabel.Text = string.Empty;

        _telefonEntry.Text = string.Empty;
        _handyEntry.Text = string.Empty;
        _adresseEntry.Text = string.Empty;
        _plzEntry.Text = string.Empty;
        _ortEntry.Text = string.Empty;

        _statusLabel.Text = message;
        UpdateUiState();
    }
}

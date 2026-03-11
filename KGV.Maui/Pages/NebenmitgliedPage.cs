using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Maui;
using KGV.Maui.State;
using System.Linq;
using System.Text.RegularExpressions;

namespace KGV.Maui.Pages;

public sealed class NebenmitgliedPage : ContentPage
{
    private static readonly Regex PlzRegex = new("^\\d{5}$", RegexOptions.Compiled);

    private readonly ISupabaseService _supabaseService;
    private readonly UserContextState _state;

    private MitgliedRecord? _neben;
    private bool _isBusy;
    private Task? _loadTask;

    private readonly ActivityIndicator _busy;
    private readonly Label _statusLabel;

    private readonly Label _nameLabel;
    private readonly Entry _telefonEntry;
    private readonly Entry _handyEntry;
    private readonly Entry _adresseEntry;
    private readonly Entry _plzEntry;
    private readonly Entry _ortEntry;
    private readonly Button _saveButton;

    public NebenmitgliedPage(ISupabaseService supabaseService, UserContextState state)
    {
        _supabaseService = supabaseService;
        _state = state;

        Title = "Nebenmitglied";

        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };
        _statusLabel = new Label { TextColor = Colors.Red };

        _nameLabel = new Label { FontSize = 22, FontAttributes = FontAttributes.Bold };

        _telefonEntry = new Entry { Placeholder = "Telefon" };
        _handyEntry = new Entry { Placeholder = "Handy" };
        _adresseEntry = new Entry { Placeholder = "Adresse (Pflicht)" };
        _plzEntry = new Entry { Placeholder = "PLZ (Pflicht)", Keyboard = Keyboard.Numeric };
        _ortEntry = new Entry { Placeholder = "Ort (Pflicht)" };

        _saveButton = new Button { Text = "Speichern" };
        _saveButton.Clicked += OnSaveClicked;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 12,
                Children =
                {
                    _busy,
                    _nameLabel,
                    new Label { Text = "Kontakt/Adresse (nur diese Felder sind editierbar)", FontAttributes = FontAttributes.Italic },
                    _statusLabel,
                    _telefonEntry,
                    _handyEntry,
                    _adresseEntry,
                    _plzEntry,
                    _ortEntry,
                    _saveButton
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

        if (_state.CurrentMitgliedId == null || _state.CurrentMitgliedId.Value <= 0 || _state.CurrentMitgliedId.Value > int.MaxValue)
        {
            _neben = null;
            _state.CurrentNebenMitgliedId = null;
            ClearUi("Hauptmitglied ist nicht gesetzt.");
            return;
        }

        try
        {
            var mainId = (int)_state.CurrentMitgliedId.Value;
            var rec = await _supabaseService.GetNebenmitgliedByHauptmitgliedIdAsync(mainId);
            if (rec == null)
            {
                _neben = null;
                _state.CurrentNebenMitgliedId = null;
                ClearUi("Kein Nebenmitglied vorhanden.");

                // Menüeintrag entfernen (ohne kaputten Screen): zurück zu "Meine Stammdaten".
                if (Shell.Current is not null)
                {
                    try
                    {
                        await Shell.Current.GoToAsync("//myprofile");
                    }
                    catch
                    {
                        // ignore
                    }

                    if (Shell.Current is IAppShellInitializer init)
                        init.BuildMenu();
                }

                return;
            }

            _neben = rec;
            _state.CurrentNebenMitgliedId = rec.Id;

            _nameLabel.Text = $"{rec.Vorname} {rec.Name}".Trim();

            _telefonEntry.Text = rec.Telefon ?? string.Empty;
            _handyEntry.Text = rec.Handy ?? string.Empty;
            _adresseEntry.Text = rec.Adresse ?? string.Empty;
            _plzEntry.Text = rec.Plz ?? string.Empty;
            _ortEntry.Text = rec.Ort ?? string.Empty;

            _statusLabel.Text = string.Empty;
        }
        catch (Exception ex)
        {
            _neben = null;
            _state.CurrentNebenMitgliedId = null;
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

        if (_neben == null)
        {
            await DisplayAlert("Fehler", "Nebenmitglied ist nicht geladen.", "OK");
            return;
        }

        // Schutz gegen inkonsistenten State
        if (_state.CurrentNebenMitgliedId.HasValue && _state.CurrentNebenMitgliedId.Value != _neben.Id)
        {
            await DisplayAlert("Fehler", "Inkonsistenter Nebenmitglied-Kontext. Bitte Seite neu laden oder abmelden/anmelden.", "OK");
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
            var ok = await _supabaseService.UpdateOwnContactAsync(_neben.Id, EmptyToNull(telefon), EmptyToNull(handy), adresse, plz, ort);
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
        if (string.IsNullOrEmpty(value)) return true;

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
        _busy.IsVisible = busy;
        _busy.IsRunning = busy;
        UpdateUiState();
    }

    private void UpdateUiState()
    {
        var hasNeben = _neben != null;
        var canEdit = !_isBusy && hasNeben;

        _telefonEntry.IsEnabled = canEdit;
        _handyEntry.IsEnabled = canEdit;
        _adresseEntry.IsEnabled = canEdit;
        _plzEntry.IsEnabled = canEdit;
        _ortEntry.IsEnabled = canEdit;

        _saveButton.IsEnabled = canEdit;
    }

    private void ClearUi(string message)
    {
        _nameLabel.Text = string.Empty;
        _telefonEntry.Text = string.Empty;
        _handyEntry.Text = string.Empty;
        _adresseEntry.Text = string.Empty;
        _plzEntry.Text = string.Empty;
        _ortEntry.Text = string.Empty;

        _statusLabel.Text = message;
        UpdateUiState();
    }
}

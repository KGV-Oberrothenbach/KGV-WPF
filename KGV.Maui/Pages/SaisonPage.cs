using System.Globalization;
using KGV.Core.Interfaces;
using KGV.Core.Models;

namespace KGV.Maui.Pages;

public sealed class SaisonPage : ContentPage
{
    private readonly ISupabaseService _supabaseService;

    private bool _isBusy;
    private Task? _loadTask;

    private bool _isEditMode;
    private bool _isNewDraft;

    private SaisonRecord? _selected;
    private SaisonRecord? _snapshot;

    private readonly ActivityIndicator _busy;
    private readonly Label _status;

    private readonly Picker _picker;

    private readonly Entry _jahr;
    private readonly Entry _soll;
    private readonly Entry _euro;
    private readonly Editor _bemerkung;

    private readonly Button _newButton;
    private readonly Button _editButton;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;

    private readonly List<SaisonRecord> _items = new();

    public SaisonPage(ISupabaseService supabaseService)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));

        Title = "Saison";

        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };
        _status = new Label { TextColor = Colors.Red };

        _picker = new Picker { Title = "Saison auswählen" };
        _picker.ItemDisplayBinding = new Binding(nameof(SaisonRecord.Jahr));
        _picker.SelectedIndexChanged += (_, __) =>
        {
            if (_isBusy || _isEditMode)
                return;

            _selected = _picker.SelectedItem as SaisonRecord;
            ApplyRecordToUi(_selected);
        };

        _jahr = new Entry { Placeholder = "Jahr (z.B. 2026)", Keyboard = Keyboard.Numeric };
        _soll = new Entry { Placeholder = "Pflichtstunden Soll (z.B. 10)", Keyboard = Keyboard.Numeric };
        _euro = new Entry { Placeholder = "€ pro Fehlstunde (z.B. 25)", Keyboard = Keyboard.Numeric };
        _bemerkung = new Editor { Placeholder = "Bemerkung", AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 120 };

        _newButton = new Button { Text = "Neu" };
        _newButton.Clicked += async (_, __) => await NewAsync();

        _editButton = new Button { Text = "Bearbeiten" };
        _editButton.Clicked += async (_, __) => await ToggleEditAsync();

        _saveButton = new Button { Text = "Speichern" };
        _saveButton.Clicked += async (_, __) => await SaveAsync();

        _cancelButton = new Button { Text = "Abbrechen" };
        _cancelButton.Clicked += async (_, __) => await CancelAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 12,
                Children =
                {
                    _busy,
                    new HorizontalStackLayout
                    {
                        Spacing = 12,
                        Children = { _newButton, _editButton, _saveButton, _cancelButton }
                    },
                    _picker,
                    _status,
                    new Label { Text = "Jahr", FontAttributes = FontAttributes.Bold },
                    _jahr,
                    new Label { Text = "Pflichtstunden Soll", FontAttributes = FontAttributes.Bold },
                    _soll,
                    new Label { Text = "€ pro Fehlstunde", FontAttributes = FontAttributes.Bold },
                    _euro,
                    new Label { Text = "Bemerkung", FontAttributes = FontAttributes.Bold },
                    _bemerkung
                }
            }
        };

        Appearing += OnAppearing;
        Disappearing += (_, _) => _status.Text = string.Empty;

        UpdateUiState();
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        await EnsureLoadedAsync();
    }

    private Task EnsureLoadedAsync()
    {
        if (_loadTask != null && !_loadTask.IsCompleted)
            return _loadTask;

        _loadTask = LoadAsync();
        return _loadTask;
    }

    private async Task LoadAsync()
    {
        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            var list = await _supabaseService.GetSaisonRecordsAsync();
            _items.Clear();
            _items.AddRange((list ?? new()).OrderByDescending(x => x.Jahr));

            _picker.ItemsSource = null;
            _picker.ItemsSource = _items;

            if (_items.Count == 0)
            {
                _selected = null;
                _picker.SelectedIndex = -1;
                ApplyRecordToUi(null);
                return;
            }

            _selected = _items.First();
            _picker.SelectedItem = _selected;
            ApplyRecordToUi(_selected);
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

    private void ApplyRecordToUi(SaisonRecord? s)
    {
        if (s == null)
        {
            _jahr.Text = string.Empty;
            _soll.Text = string.Empty;
            _euro.Text = string.Empty;
            _bemerkung.Text = string.Empty;
            return;
        }

        _jahr.Text = s.Jahr.ToString(CultureInfo.InvariantCulture);
        _soll.Text = s.PflichtstundenSoll.ToString(CultureInfo.CurrentCulture);
        _euro.Text = s.EuroProFehlstunde.ToString(CultureInfo.CurrentCulture);
        _bemerkung.Text = s.Bemerkung ?? string.Empty;
    }

    private void Snapshot()
    {
        if (_selected == null)
        {
            _snapshot = null;
            return;
        }

        _snapshot = new SaisonRecord
        {
            Id = _selected.Id,
            Jahr = _selected.Jahr,
            PflichtstundenSoll = _selected.PflichtstundenSoll,
            EuroProFehlstunde = _selected.EuroProFehlstunde,
            Bemerkung = _selected.Bemerkung
        };
    }

    private async Task NewAsync()
    {
        if (_isBusy)
            return;

        _status.Text = string.Empty;

        var latest = _items.OrderByDescending(x => x.Jahr).FirstOrDefault();
        var newYear = (latest?.Jahr ?? DateTime.Today.Year) + 1;

        _selected = null;
        _picker.SelectedIndex = -1;

        _jahr.Text = newYear.ToString(CultureInfo.InvariantCulture);
        _soll.Text = (latest?.PflichtstundenSoll ?? 0m).ToString(CultureInfo.CurrentCulture);
        _euro.Text = (latest?.EuroProFehlstunde ?? 25m).ToString(CultureInfo.CurrentCulture);
        _bemerkung.Text = latest?.Bemerkung ?? string.Empty;

        _isNewDraft = true;
        _snapshot = null;

        await EnterEditModeAsync();
    }

    private Task ToggleEditAsync()
        => _isEditMode ? CancelAsync() : EnterEditModeAsync();

    private Task EnterEditModeAsync()
    {
        if (_isBusy)
            return Task.CompletedTask;

        _status.Text = string.Empty;

        if (!_isNewDraft && _selected == null)
        {
            _status.Text = "Bitte zuerst eine Saison auswählen.";
            return Task.CompletedTask;
        }

        Snapshot();
        _isEditMode = true;
        UpdateUiState();
        return Task.CompletedTask;
    }

    private async Task SaveAsync()
    {
        if (_isBusy)
            return;

        if (!_isEditMode)
            return;

        _status.Text = string.Empty;

        if (!int.TryParse((_jahr.Text ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var jahr) || jahr < 1900 || jahr > 2100)
        {
            _status.Text = "Jahr ist ungültig.";
            return;
        }

        if (!TryParseDecimal(_soll.Text, out var soll))
        {
            _status.Text = "Pflichtstunden Soll ist ungültig.";
            return;
        }

        if (!TryParseDecimal(_euro.Text, out var euro))
        {
            _status.Text = "€ pro Fehlstunde ist ungültig.";
            return;
        }

        var record = new SaisonRecord
        {
            Id = _isNewDraft ? 0 : (_selected?.Id ?? _snapshot?.Id ?? 0),
            Jahr = jahr,
            PflichtstundenSoll = soll,
            EuroProFehlstunde = euro,
            Bemerkung = string.IsNullOrWhiteSpace(_bemerkung.Text) ? null : _bemerkung.Text.Trim()
        };

        SetBusy(true);
        try
        {
            var saved = await _supabaseService.SaveSaisonAsync(record);
            if (saved == null)
            {
                _status.Text = "Speichern fehlgeschlagen.";
                return;
            }

            _isEditMode = false;
            _isNewDraft = false;
            UpdateUiState();

            await LoadAsync();
            _picker.SelectedItem = _items.FirstOrDefault(x => x.Id == saved.Id) ?? _items.FirstOrDefault(x => x.Jahr == saved.Jahr);
            _selected = _picker.SelectedItem as SaisonRecord;
            ApplyRecordToUi(_selected);

            _status.TextColor = Colors.Green;
            _status.Text = "Gespeichert.";
        }
        catch (Exception ex)
        {
            _status.TextColor = Colors.Red;
            _status.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private Task CancelAsync()
    {
        if (!_isEditMode)
            return Task.CompletedTask;

        _status.Text = string.Empty;
        _status.TextColor = Colors.Red;

        if (_isNewDraft)
        {
            _isNewDraft = false;
            _isEditMode = false;

            _picker.SelectedItem = _items.FirstOrDefault();
            _selected = _picker.SelectedItem as SaisonRecord;
            ApplyRecordToUi(_selected);

            UpdateUiState();
            return Task.CompletedTask;
        }

        if (_snapshot != null)
        {
            _jahr.Text = _snapshot.Jahr.ToString(CultureInfo.InvariantCulture);
            _soll.Text = _snapshot.PflichtstundenSoll.ToString(CultureInfo.CurrentCulture);
            _euro.Text = _snapshot.EuroProFehlstunde.ToString(CultureInfo.CurrentCulture);
            _bemerkung.Text = _snapshot.Bemerkung ?? string.Empty;
        }

        _isEditMode = false;
        UpdateUiState();
        return Task.CompletedTask;
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
        var canEdit = !_isBusy && _isEditMode;

        _picker.IsEnabled = !_isBusy && !_isEditMode;

        _jahr.IsReadOnly = !canEdit;
        _soll.IsReadOnly = !canEdit;
        _euro.IsReadOnly = !canEdit;
        _bemerkung.IsReadOnly = !canEdit;

        _newButton.IsEnabled = !_isBusy;
        _editButton.IsEnabled = !_isBusy;
        _saveButton.IsEnabled = !_isBusy && _isEditMode;
        _cancelButton.IsEnabled = !_isBusy && _isEditMode;

        if (_isEditMode)
        {
            _editButton.Text = "Bearbeiten (aktiv)";
            _editButton.BackgroundColor = Colors.DarkOrange;
            _editButton.TextColor = Colors.White;
        }
        else
        {
            _editButton.Text = "Bearbeiten";
            _editButton.BackgroundColor = Colors.Transparent;
            _editButton.TextColor = Colors.Black;
        }
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        text = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            value = 0m;
            return true;
        }

        if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out value))
            return true;

        if (decimal.TryParse(text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            return true;

        return false;
    }
}

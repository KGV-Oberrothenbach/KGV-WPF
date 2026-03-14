using System.Globalization;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Core.Security;

namespace KGV.Maui.Pages;

public sealed class WartungsvertraegeAdminPage : FooterContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly IUserContextAccessor _userContextAccessor;

    private bool _isBusy;
    private bool _isEditMode;
    private bool _hasUnsavedChanges;
    private bool _suppressDirtyTracking;

    private readonly List<WartungsvertragRecord> _items = new();
    private WartungsvertragRecord? _selected;

    private readonly ActivityIndicator _busy;
    private readonly Label _status;
    private readonly CollectionView _list;

    private readonly Button _newButton;
    private readonly Button _editButton;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;

    private readonly Entry _titel;
    private readonly Entry _bereich;
    private readonly Editor _beschreibung;
    private readonly Entry _maxAktive;
    private readonly Switch _befreit;
    private readonly Switch _aktiv;
    private readonly Editor _bemerkung;

    public WartungsvertraegeAdminPage(ISupabaseService supabaseService, IUserContextAccessor userContextAccessor)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _userContextAccessor = userContextAccessor ?? throw new ArgumentNullException(nameof(userContextAccessor));

        Title = "Wartungsverträge";

        _busy = new ActivityIndicator { IsVisible = false, IsRunning = false };
        _status = new Label { TextColor = Colors.DarkRed, LineBreakMode = LineBreakMode.WordWrap };

        _list = new CollectionView
        {
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 8 },
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(() =>
            {
                var title = new Label { FontAttributes = FontAttributes.Bold };
                title.SetBinding(Label.TextProperty, nameof(WartungsvertragRecord.Titel));

                var sub = new Label { FontSize = 12, TextColor = Colors.Gray };
                sub.SetBinding(Label.TextProperty, nameof(WartungsvertragRecord.Bereich));

                return new Border
                {
                    Stroke = Colors.LightGray,
                    StrokeThickness = 1,
                    Padding = 10,
                    Content = new VerticalStackLayout { Spacing = 2, Children = { title, sub } }
                };
            })
        };

        _list.SelectionChanged += (_, __) =>
        {
            if (_suppressDirtyTracking)
                return;

            if (_isEditMode)
                return;

            _selected = _list.SelectedItem as WartungsvertragRecord;
            BindSelectedToEditor();
        };

        _newButton = new Button { Text = "Neu" };
        _newButton.Clicked += async (_, __) => await NewAsync();

        _editButton = new Button { Text = "Bearbeiten" };
        _editButton.Clicked += async (_, __) => await BeginEditAsync();

        _saveButton = new Button { Text = "Speichern" };
        _saveButton.Clicked += async (_, __) => await SaveAsync();

        _cancelButton = new Button { Text = "Abbrechen" };
        _cancelButton.Clicked += async (_, __) => await CancelAsync();

        _titel = new Entry { Placeholder = "Titel" };
        _bereich = new Entry { Placeholder = "Bereich" };
        _beschreibung = new Editor { AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 120, Placeholder = "Beschreibung" };
        _maxAktive = new Entry { Placeholder = "Max. aktive Zuordnungen", Keyboard = Keyboard.Numeric };
        _befreit = new Switch();
        _aktiv = new Switch();
        _bemerkung = new Editor { AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 90, Placeholder = "Bemerkung" };

        _titel.TextChanged += (_, __) => MarkDirty();
        _bereich.TextChanged += (_, __) => MarkDirty();
        _beschreibung.TextChanged += (_, __) => MarkDirty();
        _maxAktive.TextChanged += (_, __) => MarkDirty();
        _befreit.Toggled += (_, __) => MarkDirty();
        _aktiv.Toggled += (_, __) => MarkDirty();
        _bemerkung.TextChanged += (_, __) => MarkDirty();

        var buttons = new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { _newButton, _editButton, _saveButton, _cancelButton }
        };

        var form = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Details", FontSize = 18, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Titel *", FontAttributes = FontAttributes.Bold },
                _titel,
                new Label { Text = "Bereich *", FontAttributes = FontAttributes.Bold },
                _bereich,
                new Label { Text = "Beschreibung", FontAttributes = FontAttributes.Bold },
                _beschreibung,
                new Label { Text = "Max. aktive Zuordnungen", FontAttributes = FontAttributes.Bold },
                _maxAktive,
                new HorizontalStackLayout { Spacing = 10, Children = { new Label { Text = "Befreit von Pflichtstunden" }, _befreit } },
                new HorizontalStackLayout { Spacing = 10, Children = { new Label { Text = "Aktiv" }, _aktiv } },
                new Label { Text = "Bemerkung", FontAttributes = FontAttributes.Bold },
                _bemerkung,
                buttons
            }
        };

        var grid = new Grid
        {
            Padding = 16,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            }
        };

        grid.Add(new HorizontalStackLayout { Spacing = 8, Children = { _busy } }, 0, 0);
        grid.Add(_status, 0, 1);
        grid.Add(_list, 0, 2);

        var formScroll = new ScrollView { Content = form };
        grid.Add(formScroll, 1, 0);
        Grid.SetRowSpan(formScroll, 3);

        Content = grid;

        Appearing += OnAppearing;
    }

    private bool CanEdit => (_userContextAccessor.CurrentUserContext?.Role ?? UserRole.User) is UserRole.Admin or UserRole.Vorstand;

    private async void OnAppearing(object? sender, EventArgs e)
    {
        await LoadAsync();
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
        var canEdit = CanEdit;

        _newButton.IsEnabled = canEdit && !_isBusy && !_isEditMode;
        _editButton.IsEnabled = canEdit && !_isBusy && !_isEditMode && _selected != null;
        _saveButton.IsEnabled = canEdit && !_isBusy && _isEditMode && _hasUnsavedChanges;
        _cancelButton.IsEnabled = !_isBusy && _isEditMode;

        _list.IsEnabled = !_isBusy && !_isEditMode;

        var fieldsEnabled = canEdit && !_isBusy && _isEditMode;
        _titel.IsEnabled = fieldsEnabled;
        _bereich.IsEnabled = fieldsEnabled;
        _beschreibung.IsEnabled = fieldsEnabled;
        _maxAktive.IsEnabled = fieldsEnabled;
        _befreit.IsEnabled = fieldsEnabled;
        _aktiv.IsEnabled = fieldsEnabled;
        _bemerkung.IsEnabled = fieldsEnabled;
    }

    private async Task LoadAsync()
    {
        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            var list = await _supabaseService.GetWartungsvertraegeAsync();
            _items.Clear();
            if (list != null) _items.AddRange(list);

            _list.ItemsSource = _items;

            _selected = _items.FirstOrDefault();
            _suppressDirtyTracking = true;
            try
            {
                _list.SelectedItem = _selected;
            }
            finally
            {
                _suppressDirtyTracking = false;
            }

            BindSelectedToEditor();
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

    private void BindSelectedToEditor()
    {
        _suppressDirtyTracking = true;
        try
        {
            if (_selected == null)
            {
                _titel.Text = string.Empty;
                _bereich.Text = string.Empty;
                _beschreibung.Text = string.Empty;
                _maxAktive.Text = string.Empty;
                _befreit.IsToggled = false;
                _aktiv.IsToggled = true;
                _bemerkung.Text = string.Empty;
                return;
            }

            _titel.Text = _selected.Titel ?? string.Empty;
            _bereich.Text = _selected.Bereich ?? string.Empty;
            _beschreibung.Text = _selected.Beschreibung ?? string.Empty;
            _maxAktive.Text = _selected.MaxAktiveZuordnungen.ToString(CultureInfo.InvariantCulture);
            _befreit.IsToggled = _selected.BefreitVonPflichtstunden;
            _aktiv.IsToggled = _selected.Aktiv;
            _bemerkung.Text = _selected.Bemerkung ?? string.Empty;
        }
        finally
        {
            _suppressDirtyTracking = false;
            _hasUnsavedChanges = false;
            UpdateUiState();
        }
    }

    private void EnterEditMode()
    {
        _isEditMode = true;
        _hasUnsavedChanges = false;
        UpdateUiState();
    }

    private void ExitEditMode()
    {
        _isEditMode = false;
        _hasUnsavedChanges = false;
        UpdateUiState();
    }

    private void MarkDirty()
    {
        if (_suppressDirtyTracking)
            return;

        if (!_isEditMode)
            return;

        _hasUnsavedChanges = true;
        UpdateUiState();
    }

    private async Task NewAsync()
    {
        if (!CanEdit) return;

        _selected = new WartungsvertragRecord
        {
            Titel = string.Empty,
            Bereich = string.Empty,
            Beschreibung = string.Empty,
            Aktiv = true,
            MaxAktiveZuordnungen = 1,
            BefreitVonPflichtstunden = false,
            Bemerkung = string.Empty
        };

        EnterEditMode();
        BindSelectedToEditor();
        _hasUnsavedChanges = true;
        UpdateUiState();

        await Task.CompletedTask;
    }

    private async Task BeginEditAsync()
    {
        if (!CanEdit) return;
        if (_selected == null) return;

        // Clone for edit
        _selected = new WartungsvertragRecord
        {
            Id = _selected.Id,
            Titel = _selected.Titel,
            Bereich = _selected.Bereich,
            Beschreibung = _selected.Beschreibung,
            Aktiv = _selected.Aktiv,
            MaxAktiveZuordnungen = _selected.MaxAktiveZuordnungen,
            BefreitVonPflichtstunden = _selected.BefreitVonPflichtstunden,
            Bemerkung = _selected.Bemerkung
        };

        EnterEditMode();
        BindSelectedToEditor();

        await Task.CompletedTask;
    }

    private async Task CancelAsync()
    {
        ExitEditMode();
        await LoadAsync();
    }

    private static bool TryParseInt(string? text, out int value)
    {
        value = 0;
        var s = (text ?? string.Empty).Trim();
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private bool IsFormValid(out string message)
    {
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(_titel.Text))
        {
            message = "Titel fehlt.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_bereich.Text))
        {
            message = "Bereich fehlt.";
            return false;
        }

        if (!TryParseInt(_maxAktive.Text, out var max) || max < 0)
        {
            message = "Max. aktive Zuordnungen ist ungültig.";
            return false;
        }

        return true;
    }

    private async Task SaveAsync()
    {
        if (!CanEdit) return;
        if (_selected == null) return;

        if (!IsFormValid(out var msg))
        {
            _status.Text = msg;
            return;
        }

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            _selected.Titel = (_titel.Text ?? string.Empty).Trim();
            _selected.Bereich = (_bereich.Text ?? string.Empty).Trim();
            _selected.Beschreibung = _beschreibung.Text ?? string.Empty;
            _selected.Bemerkung = _bemerkung.Text ?? string.Empty;
            _selected.BefreitVonPflichtstunden = _befreit.IsToggled;
            _selected.Aktiv = _aktiv.IsToggled;
            _ = TryParseInt(_maxAktive.Text, out var max);
            _selected.MaxAktiveZuordnungen = max;

            var saved = await _supabaseService.SaveWartungsvertragAsync(_selected);
            if (saved == null)
            {
                _status.Text = "Speichern fehlgeschlagen.";
                return;
            }

            ExitEditMode();
            await LoadAsync();
            _status.Text = "Gespeichert.";
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
}

using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Core.Security;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KGV.Maui.Pages;

public sealed class BekanntmachungenAdminPage : FooterContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly IUserContextAccessor _userContextAccessor;

    private bool _isBusy;

    private readonly ObservableCollection<StartseiteBekanntmachungRecord> _items = new();

    private readonly CollectionView _list;
    private readonly Label _status;

    private readonly Button _saveButton;

    private readonly Entry _titel;
    private readonly Editor _inhaltHtml;
    private readonly DatePicker _sichtbarAb;
    private readonly DatePicker _sichtbarBis;
    private readonly Switch _sichtbarBisEnabled;
    private readonly Entry _sortOrder;

    private readonly VerticalStackLayout _form;
    private readonly Label _formHint;

    private StartseiteBekanntmachungRecord? _selected;

    private bool _isEditMode;
    private bool _hasUnsavedChanges;
    private bool _suppressSelectionChanged;
    private bool _suppressDirtyTracking;

    public BekanntmachungenAdminPage(ISupabaseService supabaseService, IUserContextAccessor userContextAccessor)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _userContextAccessor = userContextAccessor ?? throw new ArgumentNullException(nameof(userContextAccessor));

        Title = "Bekanntmachungen";

        _status = new Label { TextColor = Colors.Red };

        var newButton = new Button { Text = "Neu" };
        newButton.Clicked += async (_, __) => await NewAsync();

        _saveButton = new Button { Text = "Speichern" };
        _saveButton.Clicked += async (_, __) => await SaveAsync();

        var cancelButton = new Button { Text = "Abbrechen" };
        cancelButton.Clicked += async (_, __) => await CancelAsync();

        var deactivateButton = new Button { Text = "Deaktivieren" };
        deactivateButton.Clicked += async (_, __) => await DeactivateAsync();

        var header = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto } },
            RowDefinitions = { new RowDefinition { Height = GridLength.Auto } }
        };

        header.Add(new Label { Text = "Bekanntmachungen", FontSize = 22, FontAttributes = FontAttributes.Bold }, 0, 0);
        header.Add(new HorizontalStackLayout { Spacing = 10, Children = { newButton } }, 1, 0);

        _list = new CollectionView
        {
            ItemsSource = _items,
            SelectionMode = SelectionMode.Single,
            HeightRequest = 260,
            ItemTemplate = new DataTemplate(() =>
            {
                var title = new Label { FontAttributes = FontAttributes.Bold };
                title.SetBinding(Label.TextProperty, nameof(StartseiteBekanntmachungRecord.Titel));

                var subtitle = new Label { Opacity = 0.8, FontSize = 12, TextColor = Colors.Gray };
                subtitle.SetBinding(Label.TextProperty, new Binding(nameof(StartseiteBekanntmachungRecord.SichtbarAb), stringFormat: "ab: {0:dd.MM.yyyy}"));

                return new VerticalStackLayout { Spacing = 2, Padding = new Thickness(8, 6), Children = { title, subtitle } };
            })
        };

        _list.SelectionChanged += async (_, e) =>
        {
            if (_suppressSelectionChanged)
                return;

            var next = e.CurrentSelection?.FirstOrDefault() as StartseiteBekanntmachungRecord;
            if (next == null)
                return;

            await BeginEditExistingAsync(next);
        };

        _titel = new Entry { Placeholder = "Titel" };
        _inhaltHtml = new Editor { AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 200, Placeholder = "Inhalt (HTML)" };
        _sichtbarAb = new DatePicker { Date = DateTime.Today };
        _sichtbarBis = new DatePicker { Date = DateTime.Today };
        _sichtbarBisEnabled = new Switch { IsToggled = false };
        _sortOrder = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "Sortierung" };

        _sichtbarBisEnabled.Toggled += (_, __) =>
        {
            UpdateSichtbarBisVisibility();
            MarkDirty();
        };

        _formHint = new Label
        {
            Text = "Tippe auf einen Eintrag oder klicke 'Neu', um zu bearbeiten.",
            TextColor = Colors.Gray
        };

        _form = new VerticalStackLayout
        {
            Spacing = 12,
            IsVisible = false,
            Children =
            {
                new Label { Text = "Titel *", FontAttributes = FontAttributes.Bold },
                _titel,
                new Label { Text = "Inhalt (HTML) *", FontAttributes = FontAttributes.Bold },
                _inhaltHtml,
                BuildDatesGrid(),
                new Label { Text = "Sortierung", FontAttributes = FontAttributes.Bold },
                _sortOrder,
                new HorizontalStackLayout { Spacing = 10, Children = { _saveButton, cancelButton, deactivateButton } }
            }
        };

        _titel.TextChanged += (_, __) => MarkDirty();
        _inhaltHtml.TextChanged += (_, __) => MarkDirty();
        _sichtbarAb.DateSelected += (_, __) => MarkDirty();
        _sichtbarBis.DateSelected += (_, __) => MarkDirty();
        _sortOrder.TextChanged += (_, __) => MarkDirty();

        UpdateSichtbarBisVisibility();
        UpdateSaveButtonState();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 18,
                Spacing = 12,
                Children =
                {
                    header,
                    _status,
                    _list,
                    _formHint,
                    _form
                }
            }
        };

        Appearing += async (_, __) => await LoadAsync();
    }

    private bool CanEdit
    {
        get
        {
            var role = _userContextAccessor.CurrentUserContext?.Role;
            return role == UserRole.Admin || role == UserRole.Vorstand;
        }
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        UpdateSaveButtonState();
    }

    private bool IsFormValid()
    {
        if (_selected == null) return false;

        var titel = (_titel.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(titel)) return false;

        var inhalt = (_inhaltHtml.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(inhalt)) return false;

        var sortText = (_sortOrder.Text ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(sortText) && !int.TryParse(sortText, out _))
            return false;

        return true;
    }

    private void UpdateSaveButtonState()
    {
        _saveButton.IsEnabled = CanEdit
            && _isEditMode
            && !_isBusy
            && _hasUnsavedChanges
            && IsFormValid();
    }

    private void UpdateSichtbarBisVisibility()
    {
        var enabled = _sichtbarBisEnabled.IsToggled;
        _sichtbarBis.IsVisible = enabled;
        _sichtbarBis.IsEnabled = enabled;
    }

    private async Task LoadAsync()
    {
        if (_isBusy)
            return;

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            if (!CanEdit)
            {
                _items.Clear();
                _status.Text = "Keine Berechtigung (Admin/Vorstand erforderlich).";

                _suppressSelectionChanged = true;
                try
                {
                    _selected = null;
                    _list.SelectedItem = null;
                }
                finally
                {
                    _suppressSelectionChanged = false;
                }

                ExitEditMode();
                return;
            }

            var list = await _supabaseService.GetStartseiteBekanntmachungenAsync();
            _items.Clear();
            foreach (var r in (list ?? new List<StartseiteBekanntmachungRecord>()).Where(x => x != null))
                _items.Add(r);

            _suppressSelectionChanged = true;
            try
            {
                _selected = null;
                _list.SelectedItem = null;
            }
            finally
            {
                _suppressSelectionChanged = false;
            }

            ExitEditMode();
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
        if (!_isEditMode || _selected == null)
        {
            _titel.Text = string.Empty;
            _inhaltHtml.Text = string.Empty;
            _sichtbarAb.Date = DateTime.Today;
            _sichtbarBis.Date = DateTime.Today;

            _suppressDirtyTracking = true;
            try
            {
                _sichtbarBisEnabled.IsToggled = false;
            }
            finally
            {
                _suppressDirtyTracking = false;
            }

            UpdateSichtbarBisVisibility();
            _sortOrder.Text = string.Empty;

            _titel.IsEnabled = false;
            _inhaltHtml.IsEnabled = false;
            _sichtbarAb.IsEnabled = false;
            _sichtbarBisEnabled.IsEnabled = false;
            _sichtbarBis.IsEnabled = false;
            _sortOrder.IsEnabled = false;

            UpdateSaveButtonState();
            return;
        }

        _titel.IsEnabled = true;
        _inhaltHtml.IsEnabled = true;
        _sichtbarAb.IsEnabled = true;
        _sichtbarBisEnabled.IsEnabled = true;
        _sortOrder.IsEnabled = true;

        _titel.Text = _selected.Titel ?? string.Empty;
        _inhaltHtml.Text = _selected.InhaltHtml ?? string.Empty;

        if (_selected.SichtbarAb.HasValue)
            _sichtbarAb.Date = _selected.SichtbarAb.Value.Date;

        _suppressDirtyTracking = true;
        try
        {
            _sichtbarBisEnabled.IsToggled = _selected.SichtbarBis.HasValue;
        }
        finally
        {
            _suppressDirtyTracking = false;
        }

        _sichtbarBis.Date = (_selected.SichtbarBis ?? DateTime.Today).Date;
        UpdateSichtbarBisVisibility();

        _sortOrder.Text = _selected.SortOrder?.ToString() ?? string.Empty;

        UpdateSaveButtonState();
    }

    private async Task NewAsync()
    {
        if (!CanEdit) return;

        if (!await ConfirmDiscardChangesIfNeededAsync())
            return;

        _selected = new StartseiteBekanntmachungRecord
        {
            Titel = string.Empty,
            InhaltHtml = string.Empty,
            SichtbarAb = DateTime.Today,
            SichtbarBis = null,
            SortOrder = 0
        };

        EnterEditMode();
        await Task.CompletedTask;
    }

    private async Task BeginEditExistingAsync(StartseiteBekanntmachungRecord record)
    {
        if (!CanEdit) return;

        if (!await ConfirmDiscardChangesIfNeededAsync())
        {
            _suppressSelectionChanged = true;
            try
            {
                _list.SelectedItem = null;
            }
            finally
            {
                _suppressSelectionChanged = false;
            }

            return;
        }

        _selected = Clone(record);
        EnterEditMode();
    }

    private async Task CancelAsync()
    {
        if (!await ConfirmDiscardChangesIfNeededAsync())
            return;

        ExitEditMode();

        _suppressSelectionChanged = true;
        try
        {
            _list.SelectedItem = null;
        }
        finally
        {
            _suppressSelectionChanged = false;
        }
    }

    private async Task SaveAsync()
    {
        if (!CanEdit) return;
        if (_selected == null) return;
        if (!_isEditMode) return;
        if (_isBusy) return;

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            _selected.Titel = (_titel.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(_selected.Titel))
            {
                _status.Text = "Bitte Titel ausfüllen.";
                return;
            }

            _selected.InhaltHtml = _inhaltHtml.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace((_selected.InhaltHtml ?? string.Empty).Trim()))
            {
                _status.Text = "Bitte Inhalt ausfüllen.";
                return;
            }

            _selected.SichtbarAb = _sichtbarAb.Date;

            var sortText = (_sortOrder.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(sortText))
            {
                _selected.SortOrder = null;
            }
            else if (!int.TryParse(sortText, out var so))
            {
                _status.Text = "Sortierung muss eine ganze Zahl sein.";
                return;
            }
            else
            {
                _selected.SortOrder = so;
            }

            _selected.SichtbarBis = _sichtbarBisEnabled.IsToggled ? _sichtbarBis.Date : null;

            var saved = await _supabaseService.SaveStartseiteBekanntmachungAsync(_selected);
            if (saved == null)
            {
                _status.Text = "Speichern fehlgeschlagen.";
                return;
            }

            _status.Text = "Gespeichert.";
            await LoadAsync();
            ExitEditMode();
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

    private async Task DeactivateAsync()
    {
        if (_selected == null) return;
        if (!_isEditMode) return;

        _suppressDirtyTracking = true;
        try
        {
            _sichtbarBisEnabled.IsToggled = true;
        }
        finally
        {
            _suppressDirtyTracking = false;
        }

        _sichtbarBis.Date = DateTime.Today;
        UpdateSichtbarBisVisibility();
        await SaveAsync();
    }

    private void EnterEditMode()
    {
        _isEditMode = true;
        _hasUnsavedChanges = false;

        _formHint.IsVisible = false;
        _form.IsVisible = true;
        BindSelectedToEditor();
        UpdateSaveButtonState();
    }

    private void ExitEditMode()
    {
        _isEditMode = false;
        _hasUnsavedChanges = false;
        _selected = null;

        _form.IsVisible = false;
        _formHint.IsVisible = true;
        BindSelectedToEditor();
        UpdateSaveButtonState();
    }

    private void MarkDirty()
    {
        if (_suppressDirtyTracking)
            return;

        if (_isEditMode)
            _hasUnsavedChanges = true;

        UpdateSaveButtonState();
    }

    private async Task<bool> ConfirmDiscardChangesIfNeededAsync()
    {
        if (!_isEditMode || !_hasUnsavedChanges)
            return true;

        return await DisplayAlert(
            "Ungespeicherte Änderungen",
            "Es gibt ungespeicherte Änderungen. Änderungen verwerfen?",
            "Verwerfen",
            "Abbrechen");
    }

    private static StartseiteBekanntmachungRecord Clone(StartseiteBekanntmachungRecord record)
    {
        return new StartseiteBekanntmachungRecord
        {
            Id = record.Id,
            Titel = record.Titel,
            InhaltHtml = record.InhaltHtml,
            SichtbarAb = record.SichtbarAb,
            SichtbarBis = record.SichtbarBis,
            SortOrder = record.SortOrder
        };
    }

    private Grid BuildDatesGrid()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };

        var ab = new VerticalStackLayout
        {
            Spacing = 4,
            Children = { new Label { Text = "Sichtbar ab", FontAttributes = FontAttributes.Bold }, _sichtbarAb }
        };

        var bis = new VerticalStackLayout
        {
            Spacing = 4,
            Children = { new Label { Text = "Sichtbar bis", FontAttributes = FontAttributes.Bold }, _sichtbarBis }
        };

        grid.Add(ab, 0, 0);
        grid.Add(bis, 1, 0);
        return grid;
    }
}

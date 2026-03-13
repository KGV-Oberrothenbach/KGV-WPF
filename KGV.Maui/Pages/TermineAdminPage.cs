using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Core.Security;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace KGV.Maui.Pages;

public sealed class TermineAdminPage : FooterContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly IUserContextAccessor _userContextAccessor;

    private bool _isBusy;

    private readonly ObservableCollection<StartseiteTerminRecord> _items = new();

    private readonly CollectionView _list;
    private readonly Label _status;

    private readonly Button _saveButton;

    private readonly Entry _titel;
    private readonly Editor _beschreibung;
    private readonly DatePicker _datum;
    private readonly Entry _start;
    private readonly Entry _ende;
    private readonly DatePicker _sichtbarAb;
    private readonly DatePicker _sichtbarBis;
    private readonly Switch _sichtbarBisEnabled;

    private readonly VerticalStackLayout _form;
    private readonly Label _formHint;

    private StartseiteTerminRecord? _selected;

    private bool _isEditMode;
    private bool _hasUnsavedChanges;
    private bool _suppressSelectionChanged;
    private bool _suppressDirtyTracking;

    public TermineAdminPage(ISupabaseService supabaseService, IUserContextAccessor userContextAccessor)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _userContextAccessor = userContextAccessor ?? throw new ArgumentNullException(nameof(userContextAccessor));

        Title = "Termine";

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
            ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto } }
        };

        header.Add(new Label { Text = "Termine", FontSize = 22, FontAttributes = FontAttributes.Bold }, 0, 0);
        header.Add(new HorizontalStackLayout { Spacing = 10, Children = { newButton } }, 1, 0);

        _list = new CollectionView
        {
            ItemsSource = _items,
            SelectionMode = SelectionMode.Single,
            HeightRequest = 260,
            ItemTemplate = new DataTemplate(() =>
            {
                var title = new Label { FontAttributes = FontAttributes.Bold };
                title.SetBinding(Label.TextProperty, nameof(StartseiteTerminRecord.Titel));

                var subtitle = new Label { Opacity = 0.8, FontSize = 12, TextColor = Colors.Gray };
                subtitle.SetBinding(Label.TextProperty, new MultiBinding
                {
                    StringFormat = "{0:dd.MM.yyyy} {1}–{2}",
                    Bindings =
                    {
                        new Binding(nameof(StartseiteTerminRecord.Datum)),
                        new Binding(nameof(StartseiteTerminRecord.StartUhrzeit)),
                        new Binding(nameof(StartseiteTerminRecord.EndUhrzeit))
                    }
                });

                return new VerticalStackLayout { Spacing = 2, Padding = new Thickness(8, 6), Children = { title, subtitle } };
            })
        };

        _list.SelectionChanged += async (_, e) =>
        {
            if (_suppressSelectionChanged)
                return;

            var next = e.CurrentSelection?.FirstOrDefault() as StartseiteTerminRecord;
            if (next == null)
                return;

            await BeginEditExistingAsync(next);
        };

        _titel = new Entry { Placeholder = "Titel" };
        _beschreibung = new Editor { AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 160, Placeholder = "Beschreibung" };
        _datum = new DatePicker { Date = DateTime.Today };
        _start = new Entry { Placeholder = "Start (HH:mm)", Keyboard = Keyboard.Text };
        _ende = new Entry { Placeholder = "Ende (HH:mm)", Keyboard = Keyboard.Text };
        _sichtbarAb = new DatePicker { Date = DateTime.Today };
        _sichtbarBis = new DatePicker { Date = DateTime.Today };
        _sichtbarBisEnabled = new Switch { IsToggled = false };

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
                new Label { Text = "Beschreibung", FontAttributes = FontAttributes.Bold },
                _beschreibung,
                BuildWhenGrid(),
                BuildVisibleGrid(),
                new HorizontalStackLayout { Spacing = 10, Children = { _saveButton, cancelButton, deactivateButton } }
            }
        };

        _titel.TextChanged += (_, __) => MarkDirty();
        _beschreibung.TextChanged += (_, __) => MarkDirty();
        _datum.DateSelected += (_, __) => MarkDirty();
        _start.TextChanged += (_, __) => MarkDirty();
        _ende.TextChanged += (_, __) => MarkDirty();
        _sichtbarAb.DateSelected += (_, __) => MarkDirty();
        _sichtbarBis.DateSelected += (_, __) => MarkDirty();

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

    private static readonly CultureInfo DeCulture = CultureInfo.GetCultureInfo("de-DE");

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
        return !string.IsNullOrWhiteSpace(titel);
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
        if (_isBusy) return;

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

            var list = await _supabaseService.GetStartseiteTermineAsync();
            _items.Clear();
            foreach (var r in (list ?? new List<StartseiteTerminRecord>()).Where(x => x != null))
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
        var enabled = _isEditMode && _selected != null;

        _titel.IsEnabled = enabled;
        _beschreibung.IsEnabled = enabled;
        _datum.IsEnabled = enabled;
        _start.IsEnabled = enabled;
        _ende.IsEnabled = enabled;
        _sichtbarAb.IsEnabled = enabled;
        _sichtbarBisEnabled.IsEnabled = enabled;

        if (_selected == null)
        {
            _titel.Text = string.Empty;
            _beschreibung.Text = string.Empty;
            _datum.Date = DateTime.Today;
            _start.Text = string.Empty;
            _ende.Text = string.Empty;
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
            UpdateSaveButtonState();
            return;
        }

        _titel.Text = _selected.Titel ?? string.Empty;
        _beschreibung.Text = _selected.Beschreibung ?? string.Empty;
        if (_selected.Datum.HasValue) _datum.Date = _selected.Datum.Value.Date;
        _start.Text = _selected.StartUhrzeit ?? string.Empty;
        _ende.Text = _selected.EndUhrzeit ?? string.Empty;
        if (_selected.SichtbarAb.HasValue) _sichtbarAb.Date = _selected.SichtbarAb.Value.Date;

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
        UpdateSaveButtonState();
    }

    private async Task NewAsync()
    {
        if (!CanEdit) return;

        if (!await ConfirmDiscardChangesIfNeededAsync())
            return;

        _selected = new StartseiteTerminRecord
        {
            Titel = string.Empty,
            Beschreibung = string.Empty,
            Datum = DateTime.Today,
            StartUhrzeit = string.Empty,
            EndUhrzeit = string.Empty,
            SichtbarAb = DateTime.Today,
            SichtbarBis = null
        };

        EnterEditMode();
        await Task.CompletedTask;
    }

    private async Task BeginEditExistingAsync(StartseiteTerminRecord record)
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

            _selected.Beschreibung = _beschreibung.Text ?? string.Empty;
            _selected.Datum = _datum.Date;
            _selected.StartUhrzeit = (_start.Text ?? string.Empty).Trim();
            _selected.EndUhrzeit = (_ende.Text ?? string.Empty).Trim();
            _selected.SichtbarAb = _sichtbarAb.Date;
            _selected.SichtbarBis = _sichtbarBisEnabled.IsToggled ? _sichtbarBis.Date : null;

            var saved = await _supabaseService.SaveStartseiteTerminAsync(_selected);
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

    private static StartseiteTerminRecord Clone(StartseiteTerminRecord record)
    {
        return new StartseiteTerminRecord
        {
            Id = record.Id,
            Titel = record.Titel,
            Beschreibung = record.Beschreibung,
            Datum = record.Datum,
            StartUhrzeit = record.StartUhrzeit,
            EndUhrzeit = record.EndUhrzeit,
            SichtbarAb = record.SichtbarAb,
            SichtbarBis = record.SichtbarBis
        };
    }

    private Grid BuildWhenGrid()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };

        grid.Add(new VerticalStackLayout { Spacing = 4, Children = { new Label { Text = "Datum *", FontAttributes = FontAttributes.Bold }, _datum } }, 0, 0);
        grid.Add(new VerticalStackLayout { Spacing = 4, Children = { new Label { Text = "Start", FontAttributes = FontAttributes.Bold }, _start } }, 1, 0);
        grid.Add(new VerticalStackLayout { Spacing = 4, Children = { new Label { Text = "Ende", FontAttributes = FontAttributes.Bold }, _ende } }, 2, 0);
        return grid;
    }

    private Grid BuildVisibleGrid()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };

        grid.Add(new VerticalStackLayout { Spacing = 4, Children = { new Label { Text = "Sichtbar ab", FontAttributes = FontAttributes.Bold }, _sichtbarAb } }, 0, 0);

        grid.Add(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label { Text = "Sichtbar bis", FontAttributes = FontAttributes.Bold },
                new HorizontalStackLayout { Spacing = 10, Children = { new Label { Text = "Enddatum setzen" }, _sichtbarBisEnabled } },
                _sichtbarBis
            }
        }, 1, 0);
        return grid;
    }
}

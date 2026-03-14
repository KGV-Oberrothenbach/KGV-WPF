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
    private readonly Button _deleteButton;

    private readonly Entry _titel;
    private readonly Editor _inhaltText;
    private readonly Picker _fontSize;
    private readonly Switch _isBold;
    private readonly Switch _isItalic;
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

        _deleteButton = new Button { Text = "Löschen" };
        _deleteButton.Clicked += async (_, __) => await DeleteAsync();

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
        _inhaltText = new Editor { AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 200, Placeholder = "Inhalt" };
        _fontSize = new Picker { Title = "Schriftgröße" };
        _fontSize.ItemsSource = new List<int> { 12, 14, 16, 18, 20 };
        _fontSize.SelectedIndex = 1; // 14

        _isBold = new Switch { IsToggled = false };
        _isItalic = new Switch { IsToggled = false };
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

        var formatGrid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto) }
        };
        formatGrid.Add(new Label { Text = "Schriftgröße", VerticalTextAlignment = TextAlignment.Center }, 0, 0);
        formatGrid.Add(_fontSize, 1, 0);
        formatGrid.Add(
            new HorizontalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new HorizontalStackLayout { Spacing = 6, Children = { new Label { Text = "Fett", VerticalTextAlignment = TextAlignment.Center }, _isBold } },
                    new HorizontalStackLayout { Spacing = 6, Children = { new Label { Text = "Kursiv", VerticalTextAlignment = TextAlignment.Center }, _isItalic } }
                }
            },
            1,
            1);

        _form = new VerticalStackLayout
        {
            Spacing = 12,
            IsVisible = false,
            Children =
            {
                new Label { Text = "Titel *", FontAttributes = FontAttributes.Bold },
                _titel,
                new Label { Text = "Inhalt *", FontAttributes = FontAttributes.Bold },
                formatGrid,
                _inhaltText,
                BuildDatesGrid(),
                new Label { Text = "Sortierung", FontAttributes = FontAttributes.Bold },
                _sortOrder,
                 new HorizontalStackLayout { Spacing = 10, Children = { _saveButton, cancelButton, deactivateButton, _deleteButton } }
            }
        };

        _titel.TextChanged += (_, __) => MarkDirty();
        _inhaltText.TextChanged += (_, __) => MarkDirty();
        _fontSize.SelectedIndexChanged += (_, __) => { ApplyEditorStyle(); MarkDirty(); };
        _isBold.Toggled += (_, __) => { ApplyEditorStyle(); MarkDirty(); };
        _isItalic.Toggled += (_, __) => { ApplyEditorStyle(); MarkDirty(); };
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

        // Inhalt kommt aus normalem Textfeld (HTML wird intern erzeugt)
        var inhaltText = (_inhaltText.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(inhaltText)) return false;

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

        _deleteButton.IsEnabled = CanEdit
            && _isEditMode
            && !_isBusy
            && _selected != null
            && _selected.Id > 0;
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
            _inhaltText.Text = string.Empty;
            _fontSize.SelectedIndex = 1; // 14

            _suppressDirtyTracking = true;
            try
            {
                _isBold.IsToggled = false;
                _isItalic.IsToggled = false;
            }
            finally
            {
                _suppressDirtyTracking = false;
            }
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
            _inhaltText.IsEnabled = false;
            _fontSize.IsEnabled = false;
            _isBold.IsEnabled = false;
            _isItalic.IsEnabled = false;
            _sichtbarAb.IsEnabled = false;
            _sichtbarBisEnabled.IsEnabled = false;
            _sichtbarBis.IsEnabled = false;
            _sortOrder.IsEnabled = false;

            UpdateSaveButtonState();
            return;
        }

        _titel.IsEnabled = true;
        _inhaltText.IsEnabled = true;
        _fontSize.IsEnabled = true;
        _isBold.IsEnabled = true;
        _isItalic.IsEnabled = true;
        _sichtbarAb.IsEnabled = true;
        _sichtbarBisEnabled.IsEnabled = true;
        _sortOrder.IsEnabled = true;

        _titel.Text = _selected.Titel ?? string.Empty;
        var html = _selected.InhaltHtml ?? string.Empty;
        _inhaltText.Text = ExtractPlainText(html);
        TryExtractEditorStyle(html, out var fs, out var bold, out var italic);
        _fontSize.SelectedItem = fs;

        _suppressDirtyTracking = true;
        try
        {
            _isBold.IsToggled = bold;
            _isItalic.IsToggled = italic;
        }
        finally
        {
            _suppressDirtyTracking = false;
        }

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

        ApplyEditorStyle();

        UpdateSaveButtonState();
    }

    private void ApplyEditorStyle()
    {
        var fontSize = _fontSize.SelectedItem is int fs && fs > 0 ? fs : 14;
        _inhaltText.FontSize = fontSize;

        var attrs = FontAttributes.None;
        if (_isBold.IsToggled) attrs |= FontAttributes.Bold;
        if (_isItalic.IsToggled) attrs |= FontAttributes.Italic;
        _inhaltText.FontAttributes = attrs;
    }

    private string BuildHtml()
    {
        var text = (_inhaltText.Text ?? string.Empty).Trim();
        var encoded = System.Net.WebUtility.HtmlEncode(text)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", "<br/>", StringComparison.Ordinal);

        var fontSize = _fontSize.SelectedItem is int fs && fs > 0 ? fs : 14;
        var styles = new List<string> { $"font-size:{fontSize}px" };
        if (_isBold.IsToggled) styles.Add("font-weight:bold");
        if (_isItalic.IsToggled) styles.Add("font-style:italic");

        return $"<p style=\"{string.Join(";", styles)}\">{encoded}</p>";
    }

    private static string ExtractPlainText(string? html)
    {
        html ??= string.Empty;

        var s = html
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase);

        s = System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", string.Empty);
        return System.Net.WebUtility.HtmlDecode(s).Trim();
    }

    private static void TryExtractEditorStyle(string? html, out int fontSize, out bool bold, out bool italic)
    {
        fontSize = 14;
        bold = false;
        italic = false;

        html ??= string.Empty;
        var m = System.Text.RegularExpressions.Regex.Match(html, "style\\s*=\\s*\\\"(?<style>[^\\\"]+)\\\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success) return;

        var style = m.Groups["style"].Value;
        if (style.Contains("font-weight:bold", StringComparison.OrdinalIgnoreCase)) bold = true;
        if (style.Contains("font-style:italic", StringComparison.OrdinalIgnoreCase)) italic = true;

        var m2 = System.Text.RegularExpressions.Regex.Match(style, "font-size\\s*:\\s*(?<n>\\d+)px", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m2.Success && int.TryParse(m2.Groups["n"].Value, out var fs) && fs > 0)
            fontSize = fs;
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

            var inhaltText = (_inhaltText.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(inhaltText))
            {
                _status.Text = "Bitte Inhalt ausfüllen.";
                return;
            }

            _selected.InhaltHtml = BuildHtml();

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

    private async Task DeleteAsync()
    {
        if (!CanEdit) return;
        if (_selected == null) return;
        if (!_isEditMode) return;
        if (_isBusy) return;
        if (_selected.Id <= 0) return;

        var confirm = await DisplayAlert(
            "Löschen bestätigen",
            "Eintrag wirklich löschen? Diese Aktion kann nicht rückgängig gemacht werden.",
            "Löschen",
            "Abbrechen");

        if (!confirm)
            return;

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            var ok = await _supabaseService.DeleteStartseiteBekanntmachungAsync(_selected.Id);
            if (!ok)
            {
                _status.Text = "Löschen fehlgeschlagen.";
                return;
            }

            _status.Text = "Gelöscht.";
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

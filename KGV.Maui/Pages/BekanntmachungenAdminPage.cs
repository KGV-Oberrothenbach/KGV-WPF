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

    private readonly Entry _titel;
    private readonly Editor _inhaltHtml;
    private readonly DatePicker _sichtbarAb;
    private readonly DatePicker _sichtbarBis;
    private readonly Entry _sortOrder;

    private StartseiteBekanntmachungRecord? _selected;

    public BekanntmachungenAdminPage(ISupabaseService supabaseService, IUserContextAccessor userContextAccessor)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _userContextAccessor = userContextAccessor ?? throw new ArgumentNullException(nameof(userContextAccessor));

        Title = "Bekanntmachungen";

        _status = new Label { TextColor = Colors.Red };

        var newButton = new Button { Text = "Neu" };
        newButton.Clicked += async (_, __) => await NewAsync();

        var saveButton = new Button { Text = "Speichern" };
        saveButton.Clicked += async (_, __) => await SaveAsync();

        var deactivateButton = new Button { Text = "Deaktivieren" };
        deactivateButton.Clicked += async (_, __) => await DeactivateAsync();

        var header = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto } },
            RowDefinitions = { new RowDefinition { Height = GridLength.Auto } }
        };

        header.Add(new Label { Text = "Bekanntmachungen", FontSize = 22, FontAttributes = FontAttributes.Bold }, 0, 0);
        header.Add(new HorizontalStackLayout { Spacing = 10, Children = { newButton, saveButton, deactivateButton } }, 1, 0);

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

        _list.SelectionChanged += (_, e) =>
        {
            _selected = e.CurrentSelection?.FirstOrDefault() as StartseiteBekanntmachungRecord;
            BindSelectedToEditor();
        };

        _titel = new Entry { Placeholder = "Titel" };
        _inhaltHtml = new Editor { AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 200, Placeholder = "Inhalt (HTML)" };
        _sichtbarAb = new DatePicker { Date = DateTime.Today };
        _sichtbarBis = new DatePicker { Date = DateTime.Today };
        _sortOrder = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "Sortierung" };

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
                    new Label { Text = "Titel *", FontAttributes = FontAttributes.Bold },
                    _titel,
                    new Label { Text = "Inhalt (HTML) *", FontAttributes = FontAttributes.Bold },
                    _inhaltHtml,
                    BuildDatesGrid(),
                    new Label { Text = "Sortierung *", FontAttributes = FontAttributes.Bold },
                    _sortOrder
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
                _selected = null;
                _status.Text = "Keine Berechtigung (Admin/Vorstand erforderlich).";
                BindSelectedToEditor();
                return;
            }

            var list = await _supabaseService.GetStartseiteBekanntmachungenAsync();
            _items.Clear();
            foreach (var r in (list ?? new List<StartseiteBekanntmachungRecord>()).Where(x => x != null))
                _items.Add(r);

            _selected = _items.FirstOrDefault();
            _list.SelectedItem = _selected;
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
        if (_selected == null)
        {
            _titel.Text = string.Empty;
            _inhaltHtml.Text = string.Empty;
            _sichtbarAb.Date = DateTime.Today;
            _sichtbarBis.Date = DateTime.Today;
            _sortOrder.Text = string.Empty;

            _titel.IsEnabled = false;
            _inhaltHtml.IsEnabled = false;
            _sichtbarAb.IsEnabled = false;
            _sichtbarBis.IsEnabled = false;
            _sortOrder.IsEnabled = false;
            return;
        }

        _titel.IsEnabled = true;
        _inhaltHtml.IsEnabled = true;
        _sichtbarAb.IsEnabled = true;
        _sichtbarBis.IsEnabled = true;
        _sortOrder.IsEnabled = true;

        _titel.Text = _selected.Titel ?? string.Empty;
        _inhaltHtml.Text = _selected.InhaltHtml ?? string.Empty;

        if (_selected.SichtbarAb.HasValue)
            _sichtbarAb.Date = _selected.SichtbarAb.Value.Date;

        if (_selected.SichtbarBis.HasValue)
            _sichtbarBis.Date = _selected.SichtbarBis.Value.Date;

        _sortOrder.Text = _selected.SortOrder?.ToString() ?? string.Empty;
    }

    private async Task NewAsync()
    {
        if (!CanEdit) return;

        _selected = new StartseiteBekanntmachungRecord
        {
            Titel = string.Empty,
            InhaltHtml = string.Empty,
            SichtbarAb = DateTime.Today,
            SichtbarBis = null,
            SortOrder = 0
        };

        _items.Insert(0, _selected);
        _list.SelectedItem = _selected;
        BindSelectedToEditor();
        await Task.CompletedTask;
    }

    private async Task SaveAsync()
    {
        if (!CanEdit) return;
        if (_selected == null) return;
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
            if (string.IsNullOrWhiteSpace(sortText) || !int.TryParse(sortText, out var so))
            {
                _status.Text = "Sortierung muss eine ganze Zahl sein.";
                return;
            }

            _selected.SortOrder = so;

            // Sichtbar bis ist optional: leere Eingabe lässt null
            _selected.SichtbarBis = _sichtbarBis.Date;

            var saved = await _supabaseService.SaveStartseiteBekanntmachungAsync(_selected);
            if (saved == null)
            {
                _status.Text = "Speichern fehlgeschlagen.";
                return;
            }

            _status.Text = "Gespeichert.";
            await LoadAsync();
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

        _sichtbarBis.Date = DateTime.Today;
        await SaveAsync();
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
            Children = { new Label { Text = "Sichtbar ab *", FontAttributes = FontAttributes.Bold }, _sichtbarAb }
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

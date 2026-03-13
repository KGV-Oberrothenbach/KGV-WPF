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

    private readonly Entry _titel;
    private readonly Editor _beschreibung;
    private readonly DatePicker _datum;
    private readonly Entry _start;
    private readonly Entry _ende;
    private readonly DatePicker _sichtbarAb;
    private readonly DatePicker _sichtbarBis;

    private StartseiteTerminRecord? _selected;

    public TermineAdminPage(ISupabaseService supabaseService, IUserContextAccessor userContextAccessor)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _userContextAccessor = userContextAccessor ?? throw new ArgumentNullException(nameof(userContextAccessor));

        Title = "Termine";

        _status = new Label { TextColor = Colors.Red };

        var newButton = new Button { Text = "Neu" };
        newButton.Clicked += async (_, __) => await NewAsync();

        var saveButton = new Button { Text = "Speichern" };
        saveButton.Clicked += async (_, __) => await SaveAsync();

        var deactivateButton = new Button { Text = "Deaktivieren" };
        deactivateButton.Clicked += async (_, __) => await DeactivateAsync();

        var header = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto } }
        };

        header.Add(new Label { Text = "Termine", FontSize = 22, FontAttributes = FontAttributes.Bold }, 0, 0);
        header.Add(new HorizontalStackLayout { Spacing = 10, Children = { newButton, saveButton, deactivateButton } }, 1, 0);

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

        _list.SelectionChanged += (_, e) =>
        {
            _selected = e.CurrentSelection?.FirstOrDefault() as StartseiteTerminRecord;
            BindSelectedToEditor();
        };

        _titel = new Entry { Placeholder = "Titel" };
        _beschreibung = new Editor { AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 160, Placeholder = "Beschreibung" };
        _datum = new DatePicker { Date = DateTime.Today };
        _start = new Entry { Placeholder = "Start (HH:mm)", Keyboard = Keyboard.Text };
        _ende = new Entry { Placeholder = "Ende (HH:mm)", Keyboard = Keyboard.Text };
        _sichtbarAb = new DatePicker { Date = DateTime.Today };
        _sichtbarBis = new DatePicker { Date = DateTime.Today };

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
                    new Label { Text = "Beschreibung", FontAttributes = FontAttributes.Bold },
                    _beschreibung,
                    BuildWhenGrid(),
                    BuildVisibleGrid()
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

    private void SetBusy(bool busy) => _isBusy = busy;

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
                _selected = null;
                _status.Text = "Keine Berechtigung (Admin/Vorstand erforderlich).";
                BindSelectedToEditor();
                return;
            }

            var list = await _supabaseService.GetStartseiteTermineAsync();
            _items.Clear();
            foreach (var r in (list ?? new List<StartseiteTerminRecord>()).Where(x => x != null))
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
        var enabled = _selected != null;

        _titel.IsEnabled = enabled;
        _beschreibung.IsEnabled = enabled;
        _datum.IsEnabled = enabled;
        _start.IsEnabled = enabled;
        _ende.IsEnabled = enabled;
        _sichtbarAb.IsEnabled = enabled;
        _sichtbarBis.IsEnabled = enabled;

        if (_selected == null)
        {
            _titel.Text = string.Empty;
            _beschreibung.Text = string.Empty;
            _datum.Date = DateTime.Today;
            _start.Text = string.Empty;
            _ende.Text = string.Empty;
            _sichtbarAb.Date = DateTime.Today;
            _sichtbarBis.Date = DateTime.Today;
            return;
        }

        _titel.Text = _selected.Titel ?? string.Empty;
        _beschreibung.Text = _selected.Beschreibung ?? string.Empty;
        if (_selected.Datum.HasValue) _datum.Date = _selected.Datum.Value.Date;
        _start.Text = _selected.StartUhrzeit ?? string.Empty;
        _ende.Text = _selected.EndUhrzeit ?? string.Empty;
        if (_selected.SichtbarAb.HasValue) _sichtbarAb.Date = _selected.SichtbarAb.Value.Date;
        if (_selected.SichtbarBis.HasValue) _sichtbarBis.Date = _selected.SichtbarBis.Value.Date;
    }

    private async Task NewAsync()
    {
        if (!CanEdit) return;

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

            _selected.Beschreibung = _beschreibung.Text ?? string.Empty;
            _selected.Datum = _datum.Date;
            _selected.StartUhrzeit = (_start.Text ?? string.Empty).Trim();
            _selected.EndUhrzeit = (_ende.Text ?? string.Empty).Trim();
            _selected.SichtbarAb = _sichtbarAb.Date;
            _selected.SichtbarBis = _sichtbarBis.Date;

            var saved = await _supabaseService.SaveStartseiteTerminAsync(_selected);
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

        grid.Add(new VerticalStackLayout { Spacing = 4, Children = { new Label { Text = "Sichtbar ab *", FontAttributes = FontAttributes.Bold }, _sichtbarAb } }, 0, 0);
        grid.Add(new VerticalStackLayout { Spacing = 4, Children = { new Label { Text = "Sichtbar bis", FontAttributes = FontAttributes.Bold }, _sichtbarBis } }, 1, 0);
        return grid;
    }
}

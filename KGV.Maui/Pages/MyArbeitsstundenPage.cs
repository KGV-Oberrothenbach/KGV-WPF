using System.Globalization;
using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Maui.State;

namespace KGV.Maui.Pages;

public sealed class MyArbeitsstundenPage : ContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly UserContextState _state;

    private bool _isBusy;
    private Task? _initTask;
    private int? _currentSaisonId;

    private readonly ActivityIndicator _busy;

    private readonly Picker _forWhomPicker;

    private readonly Label _pflichtHeader;
    private readonly Label _pflichtSoll;
    private readonly Label _pflichtIst;
    private readonly Label _pflichtOffen;
    private readonly Label _pflichtFehlbetrag;
    private readonly Label _pflichtGrund;
    private readonly DatePicker _datePicker;
    private readonly Entry _hoursEntry;
    private readonly Entry _descEntry;
    private readonly Button _addButton;

    private readonly CollectionView _list;
    private readonly Label _status;

    private readonly List<MemberOption> _options = new();
    private readonly List<ArbeitsstundeListItem> _items = new();

    public MyArbeitsstundenPage(ISupabaseService supabaseService, UserContextState state)
    {
        _supabaseService = supabaseService;
        _state = state;

        Title = "Meine Arbeitsstunden";

        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };

        _forWhomPicker = new Picker { Title = "Für wen?" };
        _forWhomPicker.ItemDisplayBinding = new Binding(nameof(MemberOption.Display));

        _pflichtHeader = new Label { Text = "Pflichtstunden (Saison)", FontAttributes = FontAttributes.Bold };
        _pflichtSoll = new Label();
        _pflichtIst = new Label();
        _pflichtOffen = new Label();
        _pflichtFehlbetrag = new Label();
        _pflichtGrund = new Label { FontSize = 12, TextColor = Colors.Gray };

        _datePicker = new DatePicker { Date = DateTime.Today };

        _hoursEntry = new Entry { Placeholder = "Stunden (z.B. 2,5)", Keyboard = Keyboard.Numeric };
        _descEntry = new Entry { Placeholder = "Art der Arbeit" };

        _addButton = new Button { Text = "Arbeitsstunde erfassen" };
        _addButton.Clicked += OnAddClicked;

        _status = new Label { TextColor = Colors.Red };

        _list = new CollectionView
        {
            ItemsSource = _items,
            ItemTemplate = new DataTemplate(() =>
            {
                var title = new Label { FontAttributes = FontAttributes.Bold };
                title.SetBinding(Label.TextProperty, new Binding(path: nameof(ArbeitsstundeListItem.Dto), converter: new ArbeitsstundeTitleConverter()));

                var forWhom = new Label { FontSize = 12, TextColor = Colors.Gray };
                forWhom.SetBinding(Label.TextProperty, nameof(ArbeitsstundeListItem.ForWhomDisplay));

                var sub = new Label { FontSize = 12, TextColor = Colors.Gray };
                sub.SetBinding(Label.TextProperty, new Binding(path: nameof(ArbeitsstundeListItem.Dto), converter: new ArbeitsstundeSubConverter()));

                return new VerticalStackLayout
                {
                    Padding = new Thickness(0, 8),
                    Children = { title, forWhom, sub, new BoxView { HeightRequest = 1, Color = Colors.LightGray } }
                };
            })
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 12,
                Children =
                {
                    _busy,
                    _pflichtHeader,
                    BuildPflichtstundenGrid(),
                    _pflichtGrund,
                    _forWhomPicker,
                    _datePicker,
                    _hoursEntry,
                    _descEntry,
                    _addButton,
                    _status,
                    new Label { Text = "Bisher erfasst", FontAttributes = FontAttributes.Bold },
                    _list
                }
            }
        };

        Appearing += OnAppearing;
        Disappearing += (_, _) => _status.Text = string.Empty;

        UpdateUiState();
    }

    private Grid BuildPflichtstundenGrid()
    {
        static T Place<T>(T view, int row, int col) where T : View
        {
            Grid.SetRow(view, row);
            Grid.SetColumn(view, col);
            return view;
        }

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };

        grid.Children.Add(Place(new Label { Text = "Soll" }, 0, 0));
        grid.Children.Add(Place(_pflichtSoll, 0, 1));
        grid.Children.Add(Place(new Label { Text = "Ist" }, 0, 2));
        grid.Children.Add(Place(_pflichtIst, 0, 3));

        grid.Children.Add(Place(new Label { Text = "Offen" }, 1, 0));
        grid.Children.Add(Place(_pflichtOffen, 1, 1));
        grid.Children.Add(Place(new Label { Text = "Fehlbetrag" }, 1, 2));
        grid.Children.Add(Place(_pflichtFehlbetrag, 1, 3));

        return grid;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        await EnsureInitializedAsync();
    }

    private Task EnsureInitializedAsync()
    {
        // Verhindert parallele Loads (z.B. schnelles Tab-Wechseln / mehrfaches Appearing)
        if (_initTask != null && !_initTask.IsCompleted)
            return _initTask;

        _initTask = InitializeAsync();
        return _initTask;
    }

    private async Task InitializeAsync()
    {
        if (_isBusy)
            return;

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            if (_state.CurrentMitgliedId == null || _state.CurrentMitgliedId.Value <= 0 || _state.CurrentMitgliedId.Value > int.MaxValue)
            {
                ClearUi("MitgliedId fehlt.");
                return;
            }

            await EnsureSeasonAsync();
            await LoadPflichtstundenAsync();
            await EnsureOptionsAsync();
            await LoadListAsync();

            if (_items.Count == 0)
                _status.Text = "Noch keine Arbeitsstunden erfasst.";
        }
        catch (Exception ex)
        {
            ClearUi(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task EnsureSeasonAsync()
    {
        if (_currentSaisonId.HasValue)
            return;

        var saisonen = await _supabaseService.GetSaisonRecordsAsync();
        if (saisonen == null || saisonen.Count == 0)
            return;

        var year = DateTime.Today.Year;
        var selected = saisonen.FirstOrDefault(s => s.Jahr == year) ?? saisonen.OrderByDescending(s => s.Jahr).First();
        _currentSaisonId = selected.Id;
    }

    private async Task LoadPflichtstundenAsync()
    {
        try
        {
            if (!_currentSaisonId.HasValue)
                return;

            if (_state.CurrentMitgliedId == null || _state.CurrentMitgliedId.Value <= 0 || _state.CurrentMitgliedId.Value > int.MaxValue)
                return;

            var mainId = (int)_state.CurrentMitgliedId.Value;

            var rec = await _supabaseService.GetPflichtstundenUebersichtAsync(mainId, _currentSaisonId.Value);

            _pflichtSoll.Text = (rec?.Sollstunden).GetValueOrDefault().ToString(CultureInfo.CurrentCulture);
            _pflichtIst.Text = (rec?.Geleistet).GetValueOrDefault().ToString(CultureInfo.CurrentCulture);
            _pflichtOffen.Text = (rec?.Offen).GetValueOrDefault().ToString(CultureInfo.CurrentCulture);
            _pflichtFehlbetrag.Text = (rec?.Fehlbetrag).GetValueOrDefault().ToString(CultureInfo.CurrentCulture);
            _pflichtGrund.Text = string.IsNullOrWhiteSpace(rec?.Befreiungsgrund) ? (rec?.Regelgrund ?? string.Empty) : rec!.Befreiungsgrund!;

            _pflichtHeader.IsVisible = true;
        }
        catch
        {
            _pflichtSoll.Text = string.Empty;
            _pflichtIst.Text = string.Empty;
            _pflichtOffen.Text = string.Empty;
            _pflichtFehlbetrag.Text = string.Empty;
            _pflichtGrund.Text = string.Empty;
        }
    }

    private async Task EnsureOptionsAsync()
    {
        _options.Clear();

        var mainId = (int)_state.CurrentMitgliedId!.Value;
        _options.Add(new MemberOption(mainId, "Hauptmitglied"));

        if (_state.CurrentNebenMitgliedId != null && _state.CurrentNebenMitgliedId.Value > 0 && _state.CurrentNebenMitgliedId.Value <= int.MaxValue)
        {
            var neben = await _supabaseService.GetNebenmitgliedByHauptmitgliedIdAsync(mainId);
            if (neben != null)
            {
                _options.Add(new MemberOption(neben.Id, $"Nebenmitglied: {neben.Name} {neben.Vorname}".Trim()));
            }
            else
            {
                // defensiv: State korrigieren, wenn das Nebenmitglied nicht mehr existiert/zugeordnet ist
                _state.CurrentNebenMitgliedId = null;
            }
        }

        _forWhomPicker.IsVisible = _options.Count > 1;
        _forWhomPicker.ItemsSource = _options;
        _forWhomPicker.SelectedItem = _options.Count > 0 ? _options[0] : null;
    }

    private async Task LoadListAsync()
    {
        _items.Clear();

        if (_options.Count == 0)
            return;

        var ids = _options.Select(o => o.MitgliedId).Distinct().ToArray();
        var list = await _supabaseService.GetArbeitsstundenAsync(ids);

        var memberMap = _options.GroupBy(o => o.MitgliedId).ToDictionary(g => g.Key, g => g.First().Display);

        foreach (var a in list.OrderByDescending(x => x.Datum).ThenByDescending(x => x.Id))
        {
            memberMap.TryGetValue(a.MitgliedId, out var label);
            _items.Add(new ArbeitsstundeListItem(a, label));
        }

        _list.ItemsSource = null;
        _list.ItemsSource = _items;
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        _status.Text = string.Empty;

        if (_isBusy)
            return;

        if (_state.CurrentMitgliedId == null || _state.CurrentMitgliedId.Value <= 0 || _state.CurrentMitgliedId.Value > int.MaxValue)
        {
            await DisplayAlert("Fehler", "MitgliedId fehlt.", "OK");
            return;
        }

        if (!_currentSaisonId.HasValue)
        {
            await DisplayAlert("Fehler", "Saison konnte nicht ermittelt werden.", "OK");
            return;
        }

        var opt = _forWhomPicker.SelectedItem as MemberOption;
        if (opt == null)
        {
            await DisplayAlert("Fehler", "Bitte " + '"' + "Für wen?" + '"' + " wählen.", "OK");
            return;
        }

        var desc = (_descEntry.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(desc))
        {
            await DisplayAlert("Fehler", "Bitte Art der Arbeit angeben.", "OK");
            return;
        }

        var hoursText = (_hoursEntry.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(hoursText))
        {
            await DisplayAlert("Fehler", "Bitte Stunden angeben.", "OK");
            return;
        }

        if (!decimal.TryParse(hoursText.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var hours))
        {
            await DisplayAlert("Fehler", "Stunden sind ungültig.", "OK");
            return;
        }

        if (hours <= 0 || hours > 24)
        {
            await DisplayAlert("Fehler", "Stunden müssen zwischen 0 und 24 liegen.", "OK");
            return;
        }

        SetBusy(true);
        try
        {
            var rec = new ArbeitsstundeRecord
            {
                MitgliedId = opt.MitgliedId,
                SaisonId = _currentSaisonId.Value,
                Datum = _datePicker.Date.Date,
                Stunden = hours,
                ArtDerArbeit = desc,
                Status = "offen",
                Freigegeben = false
            };

            var ok = await _supabaseService.AddArbeitsstundeAsync(rec);
            if (!ok)
            {
                await DisplayAlert("Fehler", "Speichern fehlgeschlagen.", "OK");
                return;
            }

            _hoursEntry.Text = string.Empty;
            _descEntry.Text = string.Empty;
            _datePicker.Date = DateTime.Today;

            await LoadListAsync();
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

    private sealed record MemberOption(int MitgliedId, string Display);

    private sealed record ArbeitsstundeListItem(ArbeitsstundeDTO Dto, string? ForWhomDisplay);

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        _busy.IsVisible = busy;
        _busy.IsRunning = busy;
        UpdateUiState();
    }

    private void UpdateUiState()
    {
        var hasContext = _state.CurrentMitgliedId != null && _state.CurrentMitgliedId.Value > 0 && _state.CurrentMitgliedId.Value <= int.MaxValue;
        var canEdit = !_isBusy && hasContext;

        _forWhomPicker.IsEnabled = canEdit;
        _datePicker.IsEnabled = canEdit;
        _hoursEntry.IsEnabled = canEdit;
        _descEntry.IsEnabled = canEdit;
        _addButton.IsEnabled = canEdit;
    }

    private void ClearUi(string message)
    {
        _options.Clear();
        _items.Clear();

        _forWhomPicker.ItemsSource = null;
        _forWhomPicker.SelectedItem = null;
        _forWhomPicker.IsVisible = false;

        _list.ItemsSource = null;
        _list.ItemsSource = _items;

        _status.Text = message;
        UpdateUiState();
    }

    private sealed class ArbeitsstundeTitleConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is not ArbeitsstundeDTO a) return string.Empty;
            var status = string.IsNullOrWhiteSpace(a.Status)
                ? (a.Freigegeben ? "genehmigt" : "offen")
                : a.Status;

            return $"{a.Datum:dd.MM.yyyy} – {a.Stunden:0.##}h – Status: {status}";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }

    private sealed class ArbeitsstundeSubConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is not ArbeitsstundeDTO a) return string.Empty;
            var who = $"{a.Nachname} {a.Vorname}".Trim();
            return string.IsNullOrWhiteSpace(who) ? a.Beschreibung : $"{who}: {a.Beschreibung}";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
    }
}

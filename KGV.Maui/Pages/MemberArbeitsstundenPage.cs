using System.Globalization;
using KGV.Core.Interfaces;
using KGV.Core.Helpers;
using KGV.Core.Models;
using KGV.Maui.State;

namespace KGV.Maui.Pages;

public sealed class MemberArbeitsstundenPage : FooterContentPage
{
    private readonly ISupabaseService _supabaseService;
    private readonly MemberSelectionState _memberSelection;

    private bool _isBusy;
    private Task? _initTask;
    private int? _currentSaisonId;
    private SaisonRecord? _currentSaison;

    private readonly ActivityIndicator _busy;

    private readonly Label _header;
    private readonly Label _subHeader;

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

    private readonly List<ArbeitsstundeDTO> _items = new();

    public MemberArbeitsstundenPage(ISupabaseService supabaseService, MemberSelectionState memberSelection)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));
        _memberSelection = memberSelection ?? throw new ArgumentNullException(nameof(memberSelection));

        Title = "Arbeitsstunden";

        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };

        _header = new Label { Text = "Arbeitsstunden", FontSize = 24, FontAttributes = FontAttributes.Bold };
        _subHeader = new Label { Text = string.Empty, Opacity = 0.8 };

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

        _status = new Label { TextColor = Colors.Gray };

        _list = new CollectionView
        {
            ItemsSource = _items,
            ItemTemplate = new DataTemplate(() =>
            {
                var title = new Label { FontAttributes = FontAttributes.Bold };
                title.SetBinding(Label.TextProperty, new Binding(path: ".", converter: new ArbeitsstundeTitleConverter()));

                var sub = new Label { FontSize = 12, TextColor = Colors.Gray };
                sub.SetBinding(Label.TextProperty, nameof(ArbeitsstundeDTO.Beschreibung));

                return new VerticalStackLayout
                {
                    Padding = new Thickness(0, 8),
                    Children = { title, sub, new BoxView { HeightRequest = 1, Color = Colors.LightGray } }
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
                    _header,
                    _subHeader,
                    _pflichtHeader,
                    BuildPflichtstundenGrid(),
                    _pflichtGrund,
                    _busy,
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

    private async void OnAppearing(object? sender, EventArgs e)
    {
        await EnsureInitializedAsync();
    }

    private Task EnsureInitializedAsync()
    {
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
        SetStatus(string.Empty, isError: false);

        try
        {
            var memberId = _memberSelection.SelectedMitgliedId;
            if (!memberId.HasValue)
            {
                ClearUi("Bitte erst ein Mitglied wählen (Mitgliedersuche).", clearList: true);
                _subHeader.Text = string.Empty;
                return;
            }

            _subHeader.Text = $"Mitglied #{memberId.Value}";

            await EnsureSeasonAsync();
            await LoadPflichtstundenAsync(memberId.Value);
            await LoadListAsync(memberId.Value);

            if (_items.Count == 0)
                SetStatus("Noch keine Arbeitsstunden erfasst.", isError: false);
        }
        catch (Exception ex)
        {
            ClearUi(ex.Message, clearList: true);
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
        _currentSaison = selected;
    }

    private async Task LoadPflichtstundenAsync(int selectedMitgliedId)
    {
        try
        {
            if (!_currentSaisonId.HasValue)
                return;

            if (selectedMitgliedId <= 0)
                return;

            var member = await _supabaseService.GetMitgliedByIdAsync(selectedMitgliedId);
            var hauptmitgliedId = member?.HauptmitgliedId ?? selectedMitgliedId;

            var eval = await _supabaseService.GetPflichtstundenEvaluationAsync(hauptmitgliedId, _currentSaisonId.Value);
            if (eval == null)
                return;

            _pflichtSoll.Text = eval.Sollstunden.ToString("0.##", CultureInfo.CurrentCulture);
            _pflichtIst.Text = eval.Geleistet.ToString("0.##", CultureInfo.CurrentCulture);
            _pflichtOffen.Text = eval.OffeneStunden.ToString("0.##", CultureInfo.CurrentCulture);
            _pflichtFehlbetrag.Text = MoneyText.FormatEuro(eval.Fehlbetrag);
            _pflichtGrund.Text = eval.Grund;
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

    private async Task LoadListAsync(int mitgliedId)
    {
        _items.Clear();

        var list = await _supabaseService.GetArbeitsstundenAsync(mitgliedId);
        foreach (var a in (list ?? new List<ArbeitsstundeDTO>()).OrderByDescending(x => x.Datum).ThenByDescending(x => x.Id))
            _items.Add(a);

        _list.ItemsSource = null;
        _list.ItemsSource = _items;
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        if (_isBusy)
            return;

        var memberId = _memberSelection.SelectedMitgliedId;
        if (!memberId.HasValue)
        {
            await DisplayAlert("Fehler", "Bitte erst ein Mitglied wählen.", "OK");
            return;
        }

        if (!_currentSaisonId.HasValue)
        {
            await DisplayAlert("Fehler", "Saison konnte nicht ermittelt werden.", "OK");
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
                MitgliedId = memberId.Value,
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

            await InitializeAsync();
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

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        _busy.IsVisible = busy;
        _busy.IsRunning = busy;
        UpdateUiState();
    }

    private void UpdateUiState()
    {
        var hasContext = _memberSelection.SelectedMitgliedId != null;
        var canEdit = !_isBusy && hasContext;

        _datePicker.IsEnabled = canEdit;
        _hoursEntry.IsEnabled = canEdit;
        _descEntry.IsEnabled = canEdit;
        _addButton.IsEnabled = canEdit;
        _list.IsEnabled = !_isBusy;
    }

    private void SetStatus(string message, bool isError)
    {
        _status.Text = message;
        _status.TextColor = isError ? Colors.Red : Colors.Gray;
    }

    private void ClearUi(string message, bool clearList)
    {
        if (clearList)
        {
            _items.Clear();
            _list.ItemsSource = null;
            _list.ItemsSource = _items;
        }

        SetStatus(message, isError: true);
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

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }
}

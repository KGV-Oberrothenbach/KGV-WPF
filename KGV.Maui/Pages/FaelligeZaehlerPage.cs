using KGV.Core.Interfaces;
using KGV.Core.Models;

namespace KGV.Maui.Pages;

public sealed class FaelligeZaehlerPage : ContentPage
{
    private readonly ISupabaseService _supabaseService;

    private bool _isBusy;

    private readonly Picker _filter;
    private readonly Button _reload;
    private readonly ActivityIndicator _busy;
    private readonly Label _status;
    private readonly CollectionView _list;

    private List<ZaehlerEichstatusRecord> _all = new();

    public FaelligeZaehlerPage(ISupabaseService supabaseService)
    {
        _supabaseService = supabaseService ?? throw new ArgumentNullException(nameof(supabaseService));

        Title = "Fällige Zähler";

        _filter = new Picker { Title = "Filter" };
        _filter.Items.Add("Alle");
        _filter.Items.Add("Bereits fällig");
        _filter.Items.Add("Bald fällig");
        _filter.Items.Add("Unkritisch");
        _filter.SelectedIndex = 0;
        _filter.SelectedIndexChanged += (_, _) => ApplyFilter();

        _reload = new Button { Text = "Aktualisieren" };
        _reload.Clicked += OnReloadClicked;

        _busy = new ActivityIndicator { IsRunning = false, IsVisible = false };
        _status = new Label { TextColor = Colors.Red };

        _list = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            ItemTemplate = new DataTemplate(() =>
            {
                var title = new Label { FontAttributes = FontAttributes.Bold, FontSize = 16 };
                title.SetBinding(Label.TextProperty, new MultiBinding
                {
                    StringFormat = "{0} • Garten {1}",
                    Bindings =
                    {
                        new Binding(nameof(ZaehlerEichstatusRecord.Anlage)),
                        new Binding(nameof(ZaehlerEichstatusRecord.GartenNr))
                    }
                });

                var line1 = new Label { Opacity = 0.9 };
                line1.SetBinding(Label.TextProperty, new MultiBinding
                {
                    StringFormat = "{0} • Zähler {1}",
                    Bindings =
                    {
                        new Binding(nameof(ZaehlerEichstatusRecord.Medium)),
                        new Binding(nameof(ZaehlerEichstatusRecord.Zaehlernummer))
                    }
                });

                var line2 = new Label { Opacity = 0.8, FontSize = 12 };
                line2.SetBinding(Label.TextProperty, new MultiBinding
                {
                    StringFormat = "Eichdatum: {0:dd.MM.yyyy} • Fällig: {1:dd.MM.yyyy}",
                    Bindings =
                    {
                        new Binding(nameof(ZaehlerEichstatusRecord.Eichdatum)),
                        new Binding(nameof(ZaehlerEichstatusRecord.EichfaelligAm))
                    }
                });

                var line3 = new Label { Opacity = 0.8, FontSize = 12 };
                line3.SetBinding(Label.TextProperty, new MultiBinding
                {
                    StringFormat = "Status: {0} • Tage: {1}",
                    Bindings =
                    {
                        new Binding(nameof(ZaehlerEichstatusRecord.Status)),
                        new Binding(nameof(ZaehlerEichstatusRecord.TageBisFaellig))
                    }
                });

                // simple visual hint based on days
                var box = new BoxView { HeightRequest = 4 };
                box.SetBinding(VisualElement.BackgroundColorProperty, new Binding(nameof(ZaehlerEichstatusRecord.TageBisFaellig), converter: new DaysToColorConverter()));

                return new Border
                {
                    Stroke = Colors.LightGray,
                    StrokeThickness = 1,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                    Padding = 14,
                    Content = new VerticalStackLayout
                    {
                        Spacing = 4,
                        Children = { title, line1, line2, line3, box }
                    }
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
                    new Label { Text = "Fällige Zähler", FontSize = 24, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "Übersicht basierend auf v_zaehler_eichstatus.", Opacity = 0.8 },
                    new HorizontalStackLayout
                    {
                        Spacing = 12,
                        Children = { _filter, _reload }
                    },
                    _busy,
                    _status,
                    _list
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_all.Count > 0) return;
        await LoadAsync();
    }

    private async void OnReloadClicked(object? sender, EventArgs e)
        => await LoadAsync();

    private async Task LoadAsync()
    {
        if (_isBusy) return;

        SetBusy(true);
        _status.Text = string.Empty;

        try
        {
            _all = await _supabaseService.GetZaehlerEichstatusAsync();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _status.Text = $"Fehler: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ApplyFilter()
    {
        var sel = _filter.SelectedItem?.ToString() ?? "Alle";

        IEnumerable<ZaehlerEichstatusRecord> filtered = _all;

        bool IsDue(ZaehlerEichstatusRecord r) => (r.TageBisFaellig ?? int.MaxValue) <= 0;
        bool IsSoon(ZaehlerEichstatusRecord r) { var d = r.TageBisFaellig ?? int.MaxValue; return d > 0 && d <= 30; }
        bool IsOk(ZaehlerEichstatusRecord r) => (r.TageBisFaellig ?? int.MaxValue) > 30;

        filtered = sel switch
        {
            "Bereits fällig" => filtered.Where(IsDue),
            "Bald fällig" => filtered.Where(IsSoon),
            "Unkritisch" => filtered.Where(IsOk),
            _ => filtered
        };

        _list.ItemsSource = filtered
            .OrderBy(r => r.TageBisFaellig ?? int.MaxValue)
            .ThenBy(r => (r.Anlage ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.GartenNr ?? int.MaxValue)
            .ThenBy(r => (r.Medium ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => (r.Zaehlernummer ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        _busy.IsVisible = busy;
        _busy.IsRunning = busy;

        _filter.IsEnabled = !busy;
        _reload.IsEnabled = !busy;
    }

    private sealed class DaysToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int days)
            {
                if (days <= 0) return Colors.Red;
                if (days <= 30) return Colors.Orange;
                return Colors.Green;
            }

            return Colors.LightGray;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }
}

using Microsoft.Extensions.DependencyInjection;

namespace KGV.Maui.Pages;

public sealed class AblesenPage : FooterContentPage
{
    private readonly IServiceProvider _services;

    public AblesenPage(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));

        Title = "Ablesen";

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 16,
                Children =
                {
                    new Label
                    {
                        Text = "Ablesen",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold
                    },
                    new Label
                    {
                        Text = "Bitte wähle eine Funktion.",
                        Opacity = 0.8
                    },

                    BuildTile(
                        title: "Ablesung erfassen",
                        subtitle: "RFID scannen und Ablesung aufnehmen",
                        tapped: OnAblesungErfassenTapped),

                    BuildTile(
                        title: "Zählerwechsel",
                        subtitle: "Tag scannen und je nach Zustand Ausbau oder Einbau starten",
                        tapped: OnZaehlerwechselTapped),

                    BuildTile(
                        title: "RFID einrichten",
                        subtitle: "Parzelle wählen, Medium wählen und Tag zuordnen",
                        tapped: OnRfidEinrichtenTapped),

                    BuildTile(
                        title: "Fällige Zähler",
                        subtitle: "Zähler mit naher Eichfälligkeit anzeigen",
                        tapped: OnFaelligeZaehlerTapped)
                }
            }
        };
    }

    private static View BuildTile(string title, string subtitle, EventHandler<TappedEventArgs> tapped)
    {
        var titleLabel = new Label { Text = title, FontAttributes = FontAttributes.Bold, FontSize = 16 };
        var subtitleLabel = new Label { Text = subtitle, Opacity = 0.8, LineBreakMode = LineBreakMode.WordWrap };

        var content = new VerticalStackLayout
        {
            Spacing = 4,
            Children = { titleLabel, subtitleLabel }
        };

        var border = new Border
        {
            Stroke = Colors.LightGray,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Padding = 14,
            Content = content
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += tapped;
        border.GestureRecognizers.Add(tap);

        return border;
    }

    private async void OnAblesungErfassenTapped(object? sender, TappedEventArgs e)
    {
        var page = _services.GetRequiredService<AblesungErfassenPage>();
        await Shell.Current.Navigation.PushAsync(page);
    }

    private async void OnZaehlerwechselTapped(object? sender, TappedEventArgs e)
    {
        var page = _services.GetRequiredService<ZaehlerwechselScanPage>();
        await Shell.Current.Navigation.PushAsync(page);
    }

    private async void OnRfidEinrichtenTapped(object? sender, TappedEventArgs e)
    {
        var page = _services.GetRequiredService<RfidEinrichtenPage>();
        await Shell.Current.Navigation.PushAsync(page);
    }

    private async void OnFaelligeZaehlerTapped(object? sender, TappedEventArgs e)
    {
        var page = _services.GetRequiredService<FaelligeZaehlerPage>();
        await Shell.Current.Navigation.PushAsync(page);
    }
}

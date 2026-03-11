namespace KGV.Maui.Pages;

public sealed class ZaehlerEinbauenPlaceholderPage : ContentPage
{
    public ZaehlerEinbauenPlaceholderPage()
    {
        Title = "Zähler einbauen";

        Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 12,
            Children =
            {
                new Label { Text = "Zähler einbauen", FontSize = 24, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Wird im nächsten Block umgesetzt.", Opacity = 0.8 }
            }
        };
    }
}

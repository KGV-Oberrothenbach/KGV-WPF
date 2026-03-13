namespace KGV.Maui.Pages;

public sealed class ZaehlerAusbauenPlaceholderPage : FooterContentPage
{
    public ZaehlerAusbauenPlaceholderPage()
    {
        Title = "Zähler ausbauen";

        Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 12,
            Children =
            {
                new Label { Text = "Zähler ausbauen", FontSize = 24, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Wird im nächsten Block umgesetzt.", Opacity = 0.8 }
            }
        };
    }
}

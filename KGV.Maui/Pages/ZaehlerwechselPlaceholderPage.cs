namespace KGV.Maui.Pages;

public sealed class ZaehlerwechselPlaceholderPage : FooterContentPage
{
    public ZaehlerwechselPlaceholderPage()
    {
        Title = "Zählerwechsel";

        Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 12,
            Children =
            {
                new Label { Text = "Zählerwechsel", FontSize = 24, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Tag scannen und je nach Zustand Ausbau oder Einbau starten. Wird im nächsten Block umgesetzt.", Opacity = 0.8 }
            }
        };
    }
}

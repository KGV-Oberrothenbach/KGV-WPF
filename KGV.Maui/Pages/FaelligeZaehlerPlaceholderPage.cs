namespace KGV.Maui.Pages;

public sealed class FaelligeZaehlerPlaceholderPage : ContentPage
{
    public FaelligeZaehlerPlaceholderPage()
    {
        Title = "Fällige Zähler";

        Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 12,
            Children =
            {
                new Label { Text = "Fällige Zähler", FontSize = 24, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Wird im nächsten Block umgesetzt.", Opacity = 0.8 }
            }
        };
    }
}

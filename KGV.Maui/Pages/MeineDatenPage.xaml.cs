namespace KGV.Maui.Pages;

public class MeineDatenPage : ContentPage
{
    public MeineDatenPage()
    {
        Title = "Meine Daten";

        object? cardStyleObj = null;
        if (Application.Current?.Resources != null)
            Application.Current.Resources.TryGetValue("Card", out cardStyleObj);
        var cardStyle = cardStyleObj as Style;

        Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 12,
            Children =
            {
                new Label { Text = "Meine Daten", FontSize = 24, FontAttributes = FontAttributes.Bold },
                cardStyle != null
                    ? new Border
                    {
                        Style = cardStyle,
                        Content = new Label { Text = "Platzhalter" }
                    }
                    : new Label { Text = "Platzhalter" }
            }
        };
    }
}

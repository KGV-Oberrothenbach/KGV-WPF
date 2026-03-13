using KGV.Maui.Views;

namespace KGV.Maui.Pages;

public sealed class ImpressumPage : FooterContentPage
{
    public ImpressumPage()
    {
        Title = "Impressum";

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 10,
                Children =
                {
                    new Label { Text = "KGV", FontSize = 22, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "Impressum", FontSize = 18, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "Entwickler: Andreas Bräuer" },
                    new Label { Text = "Copyright © Andreas Bräuer" }
                }
            }
        };
    }
}

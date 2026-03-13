namespace KGV.Maui.Pages;

public sealed class RfidEinrichtenPlaceholderPage : FooterContentPage
{
    public RfidEinrichtenPlaceholderPage()
    {
        Title = "RFID einrichten";

        Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 12,
            Children =
            {
                new Label { Text = "RFID einrichten", FontSize = 24, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Parzelle wählen, Medium wählen und Tag zuordnen. Wird im nächsten Block umgesetzt.", Opacity = 0.8 }
            }
        };
    }
}

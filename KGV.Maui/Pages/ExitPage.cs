using KGV.Maui;

namespace KGV.Maui.Pages;

public sealed class ExitPage : ContentPage
{
    private readonly IServiceProvider _services;

    public ExitPage(IServiceProvider services)
    {
        _services = services;

        Title = "Abmelden";

        var logoutButton = new Button { Text = "Abmelden" };
        logoutButton.Clicked += (_, _) => AppFlow.ResetToLogin(_services);

        Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 12,
            Children =
            {
                new Label { Text = "Abmelden", FontSize = 18, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Setzt den App-Zustand zurück und führt zurück zum Login." },
                logoutButton
            }
        };
    }
}

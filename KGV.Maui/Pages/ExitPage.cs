using KGV.Core.Interfaces;
using KGV.Maui;

namespace KGV.Maui.Pages;

public sealed class ExitPage : FooterContentPage
{
    private readonly IServiceProvider _services;
    private readonly IAuthService? _authService;

    public ExitPage(IServiceProvider services)
    {
        _services = services;
        _authService = services.GetService<IAuthService>();

        Title = "Abmelden";

        var logoutButton = new Button { Text = "Abmelden" };
        logoutButton.Clicked += OnLogoutClicked;

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

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_authService != null)
                await _authService.SignOutAsync();
        }
        catch
        {
        }

        AppFlow.ResetToLogin(_services);
    }
}

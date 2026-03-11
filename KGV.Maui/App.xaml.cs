using Microsoft.Extensions.DependencyInjection;
using System;

namespace KGV.Maui;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var loginPage = _services.GetRequiredService<Pages.LoginPage>();
        var root = new NavigationPage(loginPage);
        return new Window(root);
    }
}

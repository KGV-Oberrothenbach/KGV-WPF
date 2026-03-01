using KGV.Core.Interfaces;
using KGV.Infrastructure.Authentication;
using KGV.Infrastructure.Services;
using KGV.Infrastructure.Supabase;
using KGV.ViewModels;
using KGV.Views;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace KGV
{
    public partial class App : Application
    {
        private IAuthService _authService = null!;
        private INavigationService _navigationService = null!;
        private ISupabaseService _supabaseService = null!;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            // Lade benutzerspezifische AppSettings
            AppSettings.Load();

            // Konfiguration laden
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            // Optional: UserSecrets einbinden
            try
            {
                var userSecretsAsm = Assembly.Load("Microsoft.Extensions.Configuration.UserSecrets");
                if (userSecretsAsm != null)
                {
                    var extensionsType = userSecretsAsm.GetType("Microsoft.Extensions.Configuration.UserSecrets.UserSecretsConfigurationExtensions");
                    if (extensionsType != null)
                    {
                        var methods = extensionsType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                        foreach (var m in methods)
                        {
                            if (m.Name == "AddUserSecrets" && m.IsGenericMethodDefinition)
                            {
                                var gen = m.MakeGenericMethod(typeof(App));
                                gen.Invoke(null, new object[] { builder, true });
                                break;
                            }
                        }
                    }
                }
            }
            catch
            {
                // ignorieren, falls UserSecrets-Paket nicht vorhanden
            }

            var config = builder.Build();

            // ⚡ SupabaseClientFactory erstellen (für AuthService & SupabaseService)
            var clientFactory = new SupabaseClientFactory(config);

            // Services initialisieren
            _authService = new AuthService(clientFactory, null); // Logger optional
            _supabaseService = new SupabaseService(clientFactory, null);

            // NavigationService braucht SupabaseService für VM-Erzeugung
            _navigationService = new NavigationService(_supabaseService, _authService);

            // Letzte Email laden
            string lastEmail = AppSettings.LastEmail ?? string.Empty;

            // LoginViewModel erstellen
            var loginViewModel = new LoginViewModel(_authService)
            {
                Email = lastEmail
            };

            var loginWindow = new LoginWindow
            {
                DataContext = loginViewModel
            };

            // Event bei erfolgreichem Login
            loginViewModel.LoginSucceeded += async () =>
            {
                // SupabaseService initialisieren
                await _supabaseService.InitializeAsync();

                // MainWindowViewModel mit allen Services erstellen
                var mainWindowViewModel = new MainWindowViewModel(
                    _authService,
                    _navigationService,
                    _supabaseService
                );

                // MainWindow erstellen und anzeigen
                var mainWindow = new MainWindow(mainWindowViewModel);

                Application.Current.MainWindow = mainWindow;
                mainWindow.Show();
                loginWindow.Close();
            };

            loginWindow.Show();
        }
    }
}
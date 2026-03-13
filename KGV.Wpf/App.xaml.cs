// Datei: KGV.Wpf/App.xaml.cs

using KGV.Core.Interfaces;
using KGV.Core.Security;
using KGV.Infrastructure.Authentication;
using KGV.Infrastructure.Services;
using KGV.Infrastructure.Supabase;
using KGV.Wpf.Infrastructure.Services;
using KGV.Wpf.Infrastructure.Configuration;
using KGV.Wpf.Infrastructure.Updates;
using KGV.Wpf.State;
using KGV.Wpf.ViewModels;
using KGV.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Windows;

namespace KGV.Wpf
{
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Während des Login-Dialogs soll die App NICHT automatisch beenden,
            // nur weil das erste Window (Login) geschlossen wird.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Lade benutzerspezifische AppSettings
            AppSettings.Load();

            // Konfiguration:
            // - beim ersten Start aus eingebetteter (verschlüsselter) Vorlage nach %LocalAppData%\KGV\appsettings.json schreiben
            // - danach ausschließlich aus dieser lokalen Datei laden
            var localConfigPath = ConfigurationInitializer.GetConfigPath();
            ConfigurationInitializer.EnsureConfigExists();

            var builder = new ConfigurationBuilder()
                .AddJsonFile(localConfigPath, optional: false, reloadOnChange: true);

            var config = builder.Build();

            // Fail-fast mit brauchbarer Diagnose, bevor wir tief im Startup eine Exception bekommen.
            if (string.IsNullOrWhiteSpace(config["Supabase:Url"]) || string.IsNullOrWhiteSpace(config["Supabase:Key"]))
            {
                var msg =
                    "Supabase-Konfiguration fehlt.\n\n" +
                    $"Konfigurationsdatei:\n- {localConfigPath} (exists: {File.Exists(localConfigPath)})\n\n" +
                    "Erwartete JSON-Struktur:\n{\n  \"Supabase\": {\n    \"Url\": \"...\",\n    \"Key\": \"...\"\n  }\n}";

                MessageBox.Show(msg, "Konfiguration fehlt", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            // ⚡ SupabaseClientFactory erstellen (für AuthService & SupabaseService)
            var clientFactory = new SupabaseClientFactory(config);

            var permissionService = new PermissionService();
            var userContextService = new UserContextService(clientFactory, permissionService, null);

            // Services initialisieren
            var authService = new AuthService(clientFactory, null); // Logger optional
            var supabaseService = new SupabaseService(clientFactory, null, () => AppState.CurrentUserContext);

            // Letzte Email laden
            string lastEmail = AppSettings.LastEmail ?? string.Empty;

            // LoginViewModel erstellen
            var loginViewModel = new LoginViewModel(authService)
            {
                Email = lastEmail
            };

            var loginWindow = new LoginWindow
            {
                DataContext = loginViewModel
            };

            // Event bei erfolgreichem Login (Dialog schließen)
            loginViewModel.LoginSucceeded += () =>
            {
                // Setting DialogResult schließt das Window automatisch (bei ShowDialog)
                loginWindow.DialogResult = true;
            };

            var loginOk = loginWindow.ShowDialog();
            if (loginOk != true)
            {
                Shutdown();
                return;
            }

            if (string.IsNullOrWhiteSpace(authService.CurrentUserId) || !Guid.TryParse(authService.CurrentUserId, out var userId))
            {
                MessageBox.Show("Login ok, aber UserId ist ungültig.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            var userContext = await userContextService.GetUserContextAsync(userId);
            AppState.CurrentUserContext = userContext;

            if (userContext.Role == UserRole.User && userContext.MitgliedId == null)
            {
                MessageBox.Show(
                    "Dein Account ist keinem Mitglied zugeordnet.\nBitte wende dich an den Vorstand.",
                    "Zugriff eingeschränkt",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Shutdown();
                return;
            }

            // SupabaseService initialisieren
            await supabaseService.InitializeAsync();

            // ===== DI Container (WPF) =====
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);

            services.AddSingleton<ISupabaseClientFactory>(clientFactory);
            services.AddSingleton<IAuthService>(authService);
            services.AddSingleton<ISupabaseService>(supabaseService);

            // Navigation
            services.AddSingleton<INavigationService, NavigationService>();

            // User Context (für VMs wie Export)
            services.AddSingleton(userContext);

            // ✅ WICHTIGER FIX: MainWindowViewModel muss die gleiche UserContext-Instanz bekommen,
            // die wir oben geladen haben. Das passiert über DI bereits, aber nur wenn der Parameter-Typ genau passt.
            // (Ist hier ok: ctor erwartet UserContext.)

            // ViewModels
            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainWindowViewModel>();

            // MemberSearchViewModel / MemberDetailViewModel werden NICHT sinnvoll direkt über DI konstruiert,
            // weil sie spezielle Konstruktorparameter brauchen (MainWindowViewModel / MemberDTO).
            // Daher lassen wir sie NICHT als Transient registriert, um Verwirrung zu vermeiden.
            // Die Erzeugung läuft bewusst über NavigationService.CreateViewModel(...).
            //
            // ❗WICHTIG: Falls du irgendwo _serviceProvider.GetRequiredService<MemberDetailViewModel>() nutzt,
            // dann muss man diese Registrierung wieder aufnehmen. In deinem aktuellen Flow passiert das nicht.

            services.AddTransient<NebenmitgliedDetailViewModel>();
            services.AddTransient<ArbeitsstundenViewModel>();
            services.AddTransient<GartenStromViewModel>();
            services.AddTransient<GartenWasserViewModel>();
            services.AddTransient<GartenDokumenteViewModel>();
            services.AddTransient<AdminRoleViewModel>();
            services.AddTransient<DokumenteViewModel>();
            services.AddTransient<ExportViewModel>();

            // Views
            services.AddTransient<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();

            // MainWindow über DI erzeugen (stellt ctor-injection sicher)
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();

            Current.MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();

            if (mainWindow.DataContext is MainWindowViewModel mainVm)
            {
                UpdateStartupCoordinator.Start(mainVm, mainWindow);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            _serviceProvider?.Dispose();
        }
    }
}
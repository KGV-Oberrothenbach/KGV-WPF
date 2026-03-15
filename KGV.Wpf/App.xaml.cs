// Datei: KGV.Wpf/App.xaml.cs

using KGV.Core.Interfaces;
using KGV.Core.Models;
using KGV.Core.Security;
using KGV.Infrastructure.Authentication;
using KGV.Infrastructure.Services;
using KGV.Infrastructure.Supabase;
using KGV.Wpf.Security;
using KGV.Wpf.Infrastructure.Services;
using KGV.Wpf.Infrastructure.Configuration;
using KGV.Wpf.Infrastructure.Updates;
using KGV.Wpf.State;
using KGV.Wpf.ViewModels;
using KGV.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace KGV.Wpf
{
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        private IConfiguration? _config;
        private ISupabaseClientFactory? _clientFactory;
        private DpapiSupabaseSessionStore? _sessionStore;
        private PermissionService? _permissionService;
        private UserContextService? _userContextService;
        private IAuthService? _authService;
        private ISupabaseService? _supabaseService;

        private DispatcherTimer? _inactivityTimer;
        private DateTime _lastUserActivityUtc = DateTime.UtcNow;
        private static readonly TimeSpan InactivityTimeout = TimeSpan.FromMinutes(15);

        private bool _sessionCheckInProgress;

        private const string GoogleRedirectUri = "http://localhost:54321/auth/callback";

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Während des Login-Dialogs soll die App NICHT automatisch beenden,
            // nur weil das erste Window (Login) geschlossen wird.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Lade benutzerspezifische AppSettings
            AppSettings.Load();

            InitializeInactivityTracking();
            Activated += App_Activated;
            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;

            // Konfiguration:
            // - beim ersten Start aus eingebetteter (verschlüsselter) Vorlage nach %LocalAppData%\KGV\appsettings.json schreiben
            // - danach ausschließlich aus dieser lokalen Datei laden
            var localConfigPath = ConfigurationInitializer.GetConfigPath();
            ConfigurationInitializer.EnsureConfigExists();

            var builder = new ConfigurationBuilder()
                .AddJsonFile(localConfigPath, optional: false, reloadOnChange: true);

            var config = builder.Build();
            _config = config;

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

            // ⚡ Session-Persistenz (DPAPI) + SupabaseClientFactory erstellen (für AuthService & SupabaseService)
            var sessionStore = new DpapiSupabaseSessionStore();
            var clientFactory = new SupabaseClientFactory(config, sessionStore);

            _sessionStore = sessionStore;
            _clientFactory = clientFactory;

            var permissionService = new PermissionService();
            var userContextService = new UserContextService(clientFactory, permissionService, null);

            _permissionService = permissionService;
            _userContextService = userContextService;

            // Services initialisieren
            var authService = new AuthService(clientFactory, null, sessionStore); // Logger optional
            var supabaseService = new SupabaseService(clientFactory, authService, null, () => AppState.CurrentUserContext, config);

            _authService = authService;
            _supabaseService = supabaseService;

            // 1) Beim Start zuerst versuchen, eine vorhandene Session wiederherzustellen.
            var restored = await authService.TryRestoreSessionAsync();

            if (!restored || string.IsNullOrWhiteSpace(authService.CurrentUserId) || !Guid.TryParse(authService.CurrentUserId, out _))
            {
                if (restored)
                    await authService.SignOutAsync();

                var loginOk = await ShowLoginDialogAsync(authService);
                if (!loginOk)
                {
                    Shutdown();
                    return;
                }
            }

            var started = await StartMainWindowAsync();
            if (!started)
            {
                Shutdown();
                return;
            }
        }

        private void InitializeInactivityTracking()
        {
            _lastUserActivityUtc = DateTime.UtcNow;

            try
            {
                InputManager.Current.PreProcessInput += (_, __) => _lastUserActivityUtc = DateTime.UtcNow;
            }
            catch
            {
            }

            _inactivityTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };

            _inactivityTimer.Tick += (_, __) =>
            {
                if (DateTime.UtcNow - _lastUserActivityUtc >= InactivityTimeout)
                {
                    // Wichtig: Kein SignOut – Session-Persistenz bleibt bestehen.
                    Shutdown();
                }
            };

            _inactivityTimer.Start();
        }

        private async void App_Activated(object? sender, EventArgs e)
        {
            _lastUserActivityUtc = DateTime.UtcNow;

            if (_sessionCheckInProgress)
                return;

            if (_authService == null)
                return;

            if (Current?.MainWindow == null || !Current.MainWindow.IsVisible)
                return;

            if (string.IsNullOrWhiteSpace(_authService.CurrentUserId))
                return;

            _sessionCheckInProgress = true;
            try
            {
                var ok = await _authService.EnsureValidSessionAsync(forceRefresh: true);
                if (!ok)
                    await ReturnToLoginAsync();
            }
            finally
            {
                _sessionCheckInProgress = false;
            }
        }

        private async void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode != PowerModes.Resume)
                return;

            _lastUserActivityUtc = DateTime.UtcNow;

            if (_authService == null || string.IsNullOrWhiteSpace(_authService.CurrentUserId))
                return;

            try
            {
                var ok = await _authService.EnsureValidSessionAsync(forceRefresh: true);
                if (!ok)
                    await ReturnToLoginAsync();
            }
            catch
            {
            }
        }

        private async Task ReturnToLoginAsync()
        {
            if (_authService == null)
                return;

            await _authService.SignOutAsync();

            // MainWindow schließen ohne App zu beenden
            var previousShutdownMode = ShutdownMode;
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                try
                {
                    Current.MainWindow?.Close();
                }
                catch
                {
                }

                try
                {
                    _serviceProvider?.Dispose();
                    _serviceProvider = null;
                }
                catch
                {
                }

                var loginOk = await ShowLoginDialogAsync(_authService);
                if (!loginOk)
                {
                    Shutdown();
                    return;
                }

                var started = await StartMainWindowAsync();
                if (!started)
                    Shutdown();
            }
            finally
            {
                ShutdownMode = previousShutdownMode;
            }
        }

        private async Task<bool> ShowLoginDialogAsync(IAuthService authService)
        {
            string lastEmail = AppSettings.LastEmail ?? string.Empty;

            var loginViewModel = new LoginViewModel(authService)
            {
                Email = lastEmail
            };

            var loginWindow = new LoginWindow
            {
                DataContext = loginViewModel
            };

            loginViewModel.LoginSucceeded += () => loginWindow.DialogResult = true;

            loginViewModel.PasswordResetRequired += () =>
            {
                var resetVm = new ResetPasswordViewModel(authService);
                var resetWindow = new ResetPasswordWindow
                {
                    Owner = loginWindow,
                    DataContext = resetVm
                };

                var ok = resetWindow.ShowDialog();
                return Task.FromResult(ok == true);
            };

            loginViewModel.GoogleLoginRequired += () => HandleGoogleLoginAsync(authService);

            var loginOk = loginWindow.ShowDialog();
            return loginOk == true;
        }

        private async Task<bool> StartMainWindowAsync()
        {
            if (_authService == null || _clientFactory == null || _userContextService == null || _supabaseService == null || _config == null)
                return false;

            if (string.IsNullOrWhiteSpace(_authService.CurrentUserId) || !Guid.TryParse(_authService.CurrentUserId, out var userId))
            {
                MessageBox.Show("Login ok, aber UserId ist ungültig.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            var userContext = await _userContextService.GetUserContextAsync(userId);
            AppState.CurrentUserContext = userContext;

            if (userContext.Role == UserRole.User && userContext.MitgliedId == null)
            {
                MessageBox.Show(
                    "Dein Account ist keinem Mitglied zugeordnet.\nBitte wende dich an den Vorstand.",
                    "Zugriff eingeschränkt",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            await _supabaseService.InitializeAsync();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(_config);

            services.AddSingleton<ISupabaseClientFactory>(_clientFactory);
            services.AddSingleton<IAuthService>(_authService);
            services.AddSingleton<ISupabaseService>(_supabaseService);

            services.AddSingleton<INavigationService, NavigationService>();

            services.AddSingleton(userContext);

            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainWindowViewModel>();

            services.AddTransient<NebenmitgliedDetailViewModel>();
            services.AddTransient<ArbeitsstundenViewModel>();
            services.AddTransient<GartenStromViewModel>();
            services.AddTransient<GartenWasserViewModel>();
            services.AddTransient<GartenDokumenteViewModel>();
            services.AddTransient<AdminRoleViewModel>();
            services.AddTransient<DokumenteViewModel>();
            services.AddTransient<ExportViewModel>();

            services.AddTransient<MainWindow>();

            _serviceProvider?.Dispose();
            _serviceProvider = services.BuildServiceProvider();

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            Current.MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();

            if (mainWindow.DataContext is MainWindowViewModel mainVm)
                UpdateStartupCoordinator.Start(mainVm, mainWindow);

            return true;
        }

        private async Task<bool> HandleGoogleLoginAsync(IAuthService authService)
        {
            OAuthSignInStartResult? start;
            try
            {
                start = await authService.StartGoogleSignInAsync(GoogleRedirectUri);
            }
            catch
            {
                return false;
            }

            if (start == null)
                return false;

            try
            {
                var code = await ReceiveOAuthCodeViaLoopbackAsync(start.AuthUri, path: "/auth/callback", port: 54321, timeout: TimeSpan.FromMinutes(5));
                if (string.IsNullOrWhiteSpace(code))
                    return false;

                return await authService.CompleteGoogleSignInAsync(code, start.PkceVerifier);
            }
            catch
            {
                return false;
            }
        }

        private static async Task<string?> ReceiveOAuthCodeViaLoopbackAsync(Uri authUri, string path, int port, TimeSpan timeout)
        {
            // Minimaler Loopback-Callback ohne HttpListener (vermeidet URLACL-Probleme unter Windows).
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            using var cts = new System.Threading.CancellationTokenSource(timeout);

            // Browser öffnen, sobald Listener läuft.
            // (Supabase/Google redirectet nach erfolgreichem Login auf http://localhost:{port}{path}?code=...)
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = authUri.ToString(),
                    UseShellExecute = true
                });
            }
            catch
            {
                // Falls kein Browser gestartet werden kann, wartet der Listener trotzdem.
            }

            var acceptTask = listener.AcceptTcpClientAsync(cts.Token);
            var client = await acceptTask;

            await using var stream = client.GetStream();

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync(cts.Token);
            if (string.IsNullOrWhiteSpace(requestLine))
                return null;

            // Read headers
            while (true)
            {
                var line = await reader.ReadLineAsync(cts.Token);
                if (string.IsNullOrEmpty(line))
                    break;
            }

            // Beispiel: GET /auth/callback?code=XYZ HTTP/1.1
            var parts = requestLine.Split(' ');
            if (parts.Length < 2)
                return null;

            var rawTarget = parts[1];
            if (!rawTarget.StartsWith(path, StringComparison.OrdinalIgnoreCase))
                return null;

            var uri = new Uri($"http://localhost:{port}{rawTarget}");
            string? code = null;
            var query = uri.Query.TrimStart('?');
            if (!string.IsNullOrWhiteSpace(query))
            {
                foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = pair.Split('=', 2);
                    if (kv.Length != 2)
                        continue;

                    var key = WebUtility.UrlDecode(kv[0]);
                    if (!string.Equals(key, "code", StringComparison.OrdinalIgnoreCase))
                        continue;

                    code = WebUtility.UrlDecode(kv[1]);
                    break;
                }
            }

            var body = "<html><body><h3>Login abgeschlossen</h3><p>Du kannst dieses Fenster schließen.</p></body></html>";
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var header = $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\n\r\n";
            var headerBytes = Encoding.UTF8.GetBytes(header);

            await stream.WriteAsync(headerBytes, 0, headerBytes.Length, cts.Token);
            await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length, cts.Token);

            return code;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            _serviceProvider?.Dispose();

            try
            {
                SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
            }
            catch
            {
            }

            try
            {
                Activated -= App_Activated;
            }
            catch
            {
            }
        }
    }
}
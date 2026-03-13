using KGV.Core.Interfaces;
using KGV.Core.Security;
using KGV.Infrastructure.Services;
using KGV.Maui;
using KGV.Maui.State;
using KGV.Maui.Settings;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace KGV.Maui.Pages;

public class LoginPage : FooterContentPage
{
    private readonly IAuthService _authService;
    private readonly ISupabaseService _supabaseService;
    private readonly IUserContextService _userContextService;
    private readonly UserContextState _userContextState;
    private readonly IPermissionService _permissionService;
    private readonly IServiceProvider _services;

    private readonly Entry _emailEntry;
    private readonly Entry _passwordEntry;
    private readonly ImageButton _togglePassword;
    private bool _showPassword;
    private readonly Label _statusLabel;

    public LoginPage(
        IAuthService authService,
        ISupabaseService supabaseService,
        IUserContextService userContextService,
        UserContextState userContextState,
        IPermissionService permissionService,
        IServiceProvider services)
    {
        _authService = authService;
        _supabaseService = supabaseService;
        _userContextService = userContextService;
        _userContextState = userContextState;
        _permissionService = permissionService;
        _services = services;

        Title = "Login";

        _emailEntry = new Entry { Placeholder = "E-Mail", Keyboard = Keyboard.Email, Text = AppSettings.LastEmail ?? string.Empty };
        _passwordEntry = new Entry { Placeholder = "Passwort", IsPassword = true };
		_passwordEntry.HorizontalOptions = LayoutOptions.FillAndExpand;

        _togglePassword = new ImageButton
        {
            Source = "eye.svg",
            BackgroundColor = Colors.LightGray,
            Padding = 6,
            WidthRequest = 44,
            HeightRequest = 44,
            CornerRadius = 22,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center
        };
        _togglePassword.Clicked += (_, _) => TogglePasswordVisibility();

        var passwordRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        passwordRow.Add(_passwordEntry);
        passwordRow.Add(_togglePassword, 1, 0);

        _statusLabel = new Label { TextColor = Colors.Red };

        var loginButton = new Button { Text = "Anmelden" };
        loginButton.Clicked += OnLoginClicked;

        Content = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 12,
            Children =
            {
                new Label { Text = "Login", FontSize = 24, FontAttributes = FontAttributes.Bold },
                _emailEntry,
                passwordRow,
                loginButton,
                _statusLabel
            }
        };
    }

    private void TogglePasswordVisibility()
    {
        _showPassword = !_showPassword;
        _passwordEntry.IsPassword = !_showPassword;
        _togglePassword.Source = _showPassword ? "eye_off.svg" : "eye.svg";
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        _statusLabel.Text = string.Empty;

        var email = (_emailEntry.Text ?? string.Empty).Trim();
        var password = _passwordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _statusLabel.Text = "Bitte E-Mail und Passwort eingeben.";
            return;
        }

        try
        {
            var ok = await _authService.LoginAsync(email, password);
            if (!ok)
            {
                _statusLabel.Text = "Login fehlgeschlagen. Bitte Zugangsdaten und Verbindung prüfen.";
                return;
            }

            AppSettings.LastEmail = email;
            AppSettings.Save();

            if (string.IsNullOrWhiteSpace(_authService.CurrentUserId) || !Guid.TryParse(_authService.CurrentUserId, out var userId))
            {
                _statusLabel.Text = "Login ok, aber UserId ist ungültig.";
                return;
            }

            _userContextState.CurrentUserId = userId;

            var ctx = await _userContextService.GetUserContextAsync(userId);

            if (ctx.Role == UserRole.User && !ctx.MitgliedId.HasValue)
            {
                var mitglied = await _supabaseService.GetMitgliedByAuthUserIdAsync(userId);
                if (mitglied == null)
                {
                    _statusLabel.Text = "Dein Account ist keinem Mitglied zugeordnet.";
                    return;
                }

                ctx = _permissionService.CreateContext(userId, UserRoles.User, mitglied.Id);
            }

            _userContextState.CurrentUserContext = ctx;
            _userContextState.CurrentMitgliedId = ctx.MitgliedId;

            if (_userContextState.CurrentMitgliedId.HasValue && _userContextState.CurrentMitgliedId.Value <= int.MaxValue)
            {
                var neben = await _supabaseService.GetNebenmitgliedByHauptmitgliedIdAsync((int)_userContextState.CurrentMitgliedId.Value);
                _userContextState.CurrentNebenMitgliedId = neben?.Id;
            }
            else
            {
                _userContextState.CurrentNebenMitgliedId = null;
            }

            var isPrivileged = ctx.Role is UserRole.Admin or UserRole.Vorstand || ctx.Has(PermissionFlags.CanSearchMembers);

            // Kein Modus-Dialog mehr: Shell richtet sich nach der echten Berechtigung.
            AppFlow.SwitchToShell(_services, isPrivileged ? AppMode.Admin : AppMode.User);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = ex.Message;
        }
    }
}

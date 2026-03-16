using KGV.Maui.Services;
using KGV.Maui.State;
using KGV.Maui.Settings;
using KGV.Core.Interfaces;
using KGV.Core.Security;
using KGV.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

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

        var logger = _services.GetService<ILogger<App>>();
#if KGV_PLAYSTORE
        // Play Store-Version: Updates werden durch den Store verwaltet (kein eigener Updater).
        var status = _services.GetService<AppStatusState>();
        if (status != null)
            status.UpdateStatusText = "Updates über Google Play Store";

        logger?.LogInformation("Android updates: Play Store managed (custom updater disabled).");
#else
        logger?.LogInformation("Android updates: custom updater enabled (non-PlayStore build).");
        UpdateStartupCoordinator.Start(_services);
#endif

        _ = TryRestoreSessionAndNavigateAsync();
        return new Window(root);
    }

    private async Task TryRestoreSessionAndNavigateAsync()
    {
        try
        {
            var authService = _services.GetService<IAuthService>();
            var userContextService = _services.GetService<IUserContextService>();
            var supabaseService = _services.GetService<ISupabaseService>();
            var permissionService = _services.GetService<IPermissionService>();
            var state = _services.GetService<UserContextState>();

            if (authService == null || userContextService == null || supabaseService == null || permissionService == null || state == null)
                return;

            var ok = await authService.TryRestoreSessionAsync().ConfigureAwait(false);
            if (!ok)
                return;

            if (string.IsNullOrWhiteSpace(authService.CurrentUserId) || !Guid.TryParse(authService.CurrentUserId, out var userId))
            {
                await authService.SignOutAsync().ConfigureAwait(false);
                return;
            }

            state.CurrentUserId = userId;

            var ctx = await userContextService.GetUserContextAsync(userId).ConfigureAwait(false);

            if (ctx.Role == UserRole.User && !ctx.MitgliedId.HasValue)
            {
                var mitglied = await supabaseService.GetMitgliedByAuthUserIdAsync(userId).ConfigureAwait(false);
                if (mitglied == null)
                {
                    await authService.SignOutAsync().ConfigureAwait(false);
                    return;
                }

                ctx = permissionService.CreateContext(userId, UserRoles.User, mitglied.Id);
            }

            state.CurrentUserContext = ctx;
            state.CurrentMitgliedId = ctx.MitgliedId;

            if (state.CurrentMitgliedId.HasValue && state.CurrentMitgliedId.Value <= int.MaxValue)
            {
                var neben = await supabaseService.GetNebenmitgliedByHauptmitgliedIdAsync((int)state.CurrentMitgliedId.Value).ConfigureAwait(false);
                state.CurrentNebenMitgliedId = neben?.Id;
            }
            else
            {
                state.CurrentNebenMitgliedId = null;
            }

            var isPrivileged = ctx.Role is UserRole.Admin or UserRole.Vorstand || ctx.Has(PermissionFlags.CanSearchMembers);
            var mode = isPrivileged ? AppMode.Admin : AppMode.User;

            await MainThread.InvokeOnMainThreadAsync(() => AppFlow.SwitchToShell(_services, mode));
        }
        catch
        {
        }
    }
}

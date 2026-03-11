using KGV.Maui.Pages;
using KGV.Maui.Settings;
using KGV.Maui.State;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace KGV.Maui;

public static class AppFlow
{
    public static void SwitchToShell(IServiceProvider services, AppMode mode)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        var state = services.GetRequiredService<UserContextState>();

        if (state.CurrentUserId == null)
            throw new InvalidOperationException("CurrentUserId fehlt (bitte erneut anmelden).");

        if (state.CurrentUserContext == null)
            throw new InvalidOperationException("CurrentUserContext fehlt (bitte erneut anmelden).");

        if (mode == AppMode.User && state.CurrentMitgliedId == null)
            throw new InvalidOperationException("User-Modus ist nicht möglich: Account ist keinem Mitglied zugeordnet.");

        state.CurrentAppMode = mode;

        AppSettings.AppMode = AppModes.ToStorageValue(mode);
        AppSettings.Save();

        ResetSelectionStates(services);

        var window = Application.Current?.Windows?.FirstOrDefault();
        if (window == null)
            throw new InvalidOperationException("Kein Window verfügbar.");

        Shell shell = mode == AppMode.Admin
            ? services.GetRequiredService<AdminShell>()
            : services.GetRequiredService<UserShell>();

        if (shell is IAppShellInitializer init)
            init.BuildMenu();

        window.Page = shell;
    }

    public static void ResetToLogin(IServiceProvider services)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        ResetState(services);

        var window = Application.Current?.Windows?.FirstOrDefault();
        if (window == null)
            return;

        // Fresh navigation root to avoid stale navigation stacks.
        var loginPage = services.GetRequiredService<LoginPage>();
        window.Page = new NavigationPage(loginPage);
    }

    private static void ResetState(IServiceProvider services)
    {
        var state = services.GetRequiredService<UserContextState>();

        state.CurrentAppMode = null;
        state.CurrentUserContext = null;
        state.CurrentUserId = null;
        state.CurrentMitgliedId = null;
        state.CurrentNebenMitgliedId = null;

        ResetSelectionStates(services);

        // AppMode ist nach Logout nicht mehr vertrauenswürdig.
        AppSettings.AppMode = null;
        AppSettings.Save();
    }

    private static void ResetSelectionStates(IServiceProvider services)
    {
        // These states exist in both modes; clear them when switching shells or logging out.
        var memberSel = services.GetService<MemberSelectionState>();
        if (memberSel != null)
            memberSel.SelectedMitgliedId = null;

        var parzSel = services.GetService<ParzelleSelectionState>();
        if (parzSel != null)
        {
            parzSel.SelectedParzelleId = null;
            parzSel.GartenNr = null;
        }
    }
}

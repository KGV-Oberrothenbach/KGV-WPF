using KGV.Core.Security;
using KGV.Infrastructure.DependencyInjection;
using KGV.Maui.Pages;
using KGV.Maui.Settings;
using KGV.Maui.State;
using KGV.Maui.Services;
using KGV.Maui.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KGV.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Use the same appsettings.json as the WPF app.
        // For Android we bootstrap a local config file on first start and then load from that file only.
        // No plaintext appsettings.json is shipped with the app.
        TryAddAppSettings(builder.Configuration);

        AppSettings.Load();

        builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

        builder.Services.AddSingleton<UserContextState>();
        builder.Services.AddSingleton<IUserContextAccessor>(sp => sp.GetRequiredService<UserContextState>());

        builder.Services.AddSingleton<AppStatusState>();

        builder.Services.AddSingleton<MemberSelectionState>();
        builder.Services.AddSingleton<ParzelleSelectionState>();

        builder.Services.AddSingleton<IAndroidUpdateService, AndroidUpdateService>();

#if ANDROID
        builder.Services.AddSingleton<IRfidScanService, AndroidRfidScanService>();
#endif

        builder.Services.AddKgvServices(builder.Configuration);

        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<AblesenPage>();
        builder.Services.AddTransient<AblesungErfassenPage>();
        builder.Services.AddTransient<ZaehlerwechselScanPage>();
        builder.Services.AddTransient<RfidEinrichtenPage>();
        builder.Services.AddTransient<FaelligeZaehlerPage>();
        builder.Services.AddTransient<MemberSearchViewModel>();
        builder.Services.AddTransient<MemberSearchPage>();

        builder.Services.AddTransient<MemberDetailPage>();
        builder.Services.AddTransient<GartenStromPage>();
        builder.Services.AddTransient<GartenWasserPage>();
		builder.Services.AddTransient<GartenDokumentePage>();
        builder.Services.AddTransient<MyProfilePage>();
		builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<DokumentePage>();
        builder.Services.AddTransient<NebenmitgliedPage>();
        builder.Services.AddTransient<MyArbeitsstundenPage>();
        builder.Services.AddTransient<ArbeitsstundenReviewPage>();
        builder.Services.AddTransient<AdminRolePage>();
        builder.Services.AddTransient<UserManagementPage>();
        builder.Services.AddTransient<BekanntmachungenAdminPage>();
        builder.Services.AddTransient<TermineAdminPage>();
        builder.Services.AddTransient<ArbeitseinsaetzeAdminPage>();
        builder.Services.AddTransient<MemberArbeitsstundenPage>();
        builder.Services.AddTransient<MemberDokumentePage>();
        builder.Services.AddTransient<SaisonPage>();
        builder.Services.AddTransient<ExitPage>();
        builder.Services.AddTransient<ImpressumPage>();

        builder.Services.AddSingleton<AdminShell>();
        builder.Services.AddSingleton<UserShell>();

        return builder.Build();
    }

    private static void TryAddAppSettings(IConfigurationBuilder configuration)
    {
        try
        {
            MauiConfigurationBootstrapper.EnsureConfigExistsAsync()
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            var localPath = MauiConfigurationBootstrapper.GetConfigPath();
            if (!File.Exists(localPath))
                throw new FileNotFoundException($"Local configuration file was not created: '{localPath}'.");

            configuration.AddJsonFile(localPath, optional: false, reloadOnChange: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to bootstrap/load local configuration: {ex}");
        }
    }
}

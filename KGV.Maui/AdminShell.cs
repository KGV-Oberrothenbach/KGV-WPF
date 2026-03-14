using KGV.Maui.Pages;
using KGV.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace KGV.Maui;

public sealed class AdminShell : Shell, IAppShellInitializer
{
    private readonly IServiceProvider _services;

    private static bool _routesRegistered;

    private FlyoutItem? _workhoursReviewFlyout;
    private ShellContent? _workhoursReviewContent;

    public AdminShell(IServiceProvider services)
    {
        _services = services;
        FlyoutBehavior = FlyoutBehavior.Flyout;
    }

    public void BuildMenu()
    {
        Items.Clear();

        if (!_routesRegistered)
        {
            Routing.RegisterRoute("ablesen_rfid_einrichten", typeof(RfidEinrichtenPlaceholderPage));
            Routing.RegisterRoute("ablesen_faellige_zaehler", typeof(FaelligeZaehlerPlaceholderPage));
            Routing.RegisterRoute("bekanntmachungen_admin", typeof(BekanntmachungenAdminPage));
            Routing.RegisterRoute("termine_admin", typeof(TermineAdminPage));
            Routing.RegisterRoute("arbeitseinsaetze_admin", typeof(ArbeitseinsaetzeAdminPage));
            Routing.RegisterRoute("wartungsvertraege_admin", typeof(WartungsvertraegeAdminPage));
            Routing.RegisterRoute("wartungsvertraege_member", typeof(MemberWartungsvertraegePage));
            _routesRegistered = true;
        }

        Items.Add(new FlyoutItem
        {
            Title = "Start",
            Items =
            {
                new ShellContent
                {
                    Title = "Start",
                    Route = "home",
                    ContentTemplate = new DataTemplate(() => _services.GetRequiredService<HomePage>())
                }
            }
        });

        Items.Add(new FlyoutItem
        {
            Title = "Ablesen",
            Items =
            {
                new ShellContent
                {
                    Title = "Ablesen",
                    Route = "ablesen",
                    ContentTemplate = new DataTemplate(() => _services.GetRequiredService<AblesenPage>())
                }
            }
        });

        Items.Add(new FlyoutItem
        {
            Title = "Saison",
            Items =
            {
                new ShellContent
                {
                    Title = "Saison",
                    Route = "saison",
                    ContentTemplate = new DataTemplate(() => _services.GetRequiredService<SaisonPage>())
                }
            }
        });

        Items.Add(new FlyoutItem
        {
            Title = "Wartungsverträge",
            Items =
            {
                new ShellContent
                {
                    Title = "Wartungsverträge",
                    Route = "wartungsvertraege",
                    ContentTemplate = new DataTemplate(() => _services.GetRequiredService<WartungsvertraegeAdminPage>())
                }
            }
        });

        // Globale Admin-Aufgaben
        _workhoursReviewContent = new ShellContent
        {
            Title = "Arbeitsstunden prüfen",
            Route = "workhours_review",
            ContentTemplate = new DataTemplate(() => _services.GetRequiredService<ArbeitsstundenReviewPage>())
        };

        _workhoursReviewFlyout = new FlyoutItem
        {
            Title = "Arbeitsstunden prüfen",
            Items = { _workhoursReviewContent }
        };

        Items.Add(_workhoursReviewFlyout);

        Items.Add(new FlyoutItem
        {
            Title = "Mitgliedersuche",
            Items =
            {
                new ShellContent
                {
                    Title = "Mitgliedersuche",
                    Route = "membersearch",
                    ContentTemplate = new DataTemplate(() => _services.GetRequiredService<MemberSearchPage>())
                }
            }
        });

        Items.Add(new FlyoutItem
        {
            Title = "Stammdaten",
            Items =
            {
                new ShellContent
                {
                    Title = "Stammdaten",
                    Route = "memberdetail",
                    ContentTemplate = new DataTemplate(() => _services.GetRequiredService<MemberDetailPage>())
                }
            }
        });

        Items.Add(new FlyoutItem
        {
            Title = "Arbeitsstunden",
            Items =
            {
                new ShellContent
                {
                    Title = "Arbeitsstunden",
                    Route = "workhours_member",
                    ContentTemplate = new DataTemplate(() => _services.GetRequiredService<MemberArbeitsstundenPage>())
                }
            }
        });

        Items.Add(new FlyoutItem
        {
            Title = "Wartungsverträge",
            Items =
            {
                new ShellContent
                {
                    Title = "Wartungsverträge",
                    Route = "wartungsvertraege_member",
                    ContentTemplate = new DataTemplate(() => _services.GetRequiredService<MemberWartungsvertraegePage>())
                }
            }
        });

        Items.Add(new FlyoutItem
        {
            Title = "Mitgliedsdokumente",
            Items =
            {
                new ShellContent
                {
                    Title = "Mitgliedsdokumente",
                    Route = "memberdocs",
                    ContentTemplate = new DataTemplate(() => _services.GetRequiredService<MemberDokumentePage>())
                }
            }
        });

        Items.Add(new FlyoutItem
        {
            Title = "Strom",
            Items =
            {
                new ShellContent
                {
                    Title = "Strom",
                    Route = "strom",
                    ContentTemplate = new DataTemplate(() => _services.GetRequiredService<GartenStromPage>())
                }
            }
        });

        Items.Add(new FlyoutItem
        {
            Title = "Wasser",
            Items =
            {
                new ShellContent
                {
                    Title = "Wasser",
                    Route = "wasser",
                    ContentTemplate = new DataTemplate(() => _services.GetRequiredService<GartenWasserPage>())
                }
            }
        });

        Items.Add(new FlyoutItem
        {
            Title = "Parzellen-Dokumente",
            Items =
            {
                new ShellContent
                {
                    Title = "Parzellen-Dokumente",
                    Route = "dokumente",
                    ContentTemplate = new DataTemplate(() => _services.GetRequiredService<GartenDokumentePage>())
                }
            }
        });

        Items.Add(new FlyoutItem
        {
            Title = "Info / Impressum",
            Items =
            {
                new ShellContent
                {
                    Title = "Impressum",
                    Route = "impressum",
                    ContentTemplate = new DataTemplate(() => _services.GetRequiredService<ImpressumPage>())
                }
            }
        });

        Items.Add(new FlyoutItem
        {
            Title = "Abmelden",
            Items =
            {
                new ShellContent
                {
                    Title = "Abmelden",
                    Route = "exit",
                    ContentTemplate = new DataTemplate(() => _services.GetRequiredService<ExitPage>())
                }
            }
        });

        // Admin-Menü bewusst ganz unten (unter Abmelden)
        Items.Add(new FlyoutItem
        {
            Title = "Admin-Menü",
            Items =
            {
                new ShellContent
                {
                    Title = "Rollen / Benutzer",
                    Route = "adminrole",
                    ContentTemplate = new DataTemplate(() => _services.GetRequiredService<AdminRolePage>())
                },
                new ShellContent
                {
                    Title = "Benutzerverwaltung",
                    Route = "usermanagement",
                    ContentTemplate = new DataTemplate(() => _services.GetRequiredService<UserManagementPage>())
                }
            }
        });

        // Startseite bleibt neutral (Landing-Page), unabhängig vom Menü
        if (Items.Count > 0)
        {
            CurrentItem = Items[0];
        }

        _ = RefreshWorkhoursBadgeAsync();
    }

    public async Task RefreshWorkhoursBadgeAsync()
    {
        if (_workhoursReviewFlyout == null || _workhoursReviewContent == null)
            return;

        try
        {
            var supabase = _services.GetRequiredService<ISupabaseService>();
            var groups = await supabase.GetUnapprovedArbeitsstundenByMitgliedAsync();
            var openCount = groups?.Sum(x => x.Count) ?? 0;

            var title = openCount > 0 ? $"Arbeitsstunden prüfen ({openCount})" : "Arbeitsstunden prüfen";

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _workhoursReviewFlyout.Title = title;
                _workhoursReviewContent.Title = title;
            });
        }
        catch
        {
            // Badge ist optional; bei Fehlern einfach ohne Zahl.
        }
    }
}

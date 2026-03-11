using KGV.Core.Security;
using KGV.Maui.Pages;

namespace KGV.Maui;

public class AppShell : Shell
{
    public AppShell()
    {
        FlyoutBehavior = FlyoutBehavior.Disabled;
    }

    public void BuildMenu(UserContext userContext)
    {
        Items.Clear();

        FlyoutBehavior = FlyoutBehavior.Flyout;

        if (userContext.Has(PermissionFlags.CanSearchMembers))
        {
            Items.Add(new FlyoutItem
            {
                Title = "Mitgliedersuche",
                Items =
                {
                    new ShellContent
                    {
                        Title = "Mitgliedersuche",
                        Route = "membersearch",
                        ContentTemplate = new DataTemplate(typeof(MemberSearchPage))
                    }
                }
            });
        }

        // Always show Home
        Items.Add(new FlyoutItem
        {
            Title = "Home",
            Items =
            {
                new ShellContent
                {
                    Title = "Home",
                    Route = "home",
                    ContentTemplate = new DataTemplate(typeof(HomePage))
                }
            }
        });

        if (userContext.Has(PermissionFlags.CanSeeOwnDataOnly))
        {
            Items.Add(new FlyoutItem
            {
                Title = "Meine Daten",
                Items =
                {
                    new ShellContent
                    {
                        Title = "Meine Daten",
                        ContentTemplate = new DataTemplate(typeof(MeineDatenPage))
                    }
                }
            });
        }

        if (userContext.Has(PermissionFlags.CanManageDocuments))
        {
            Items.Add(new FlyoutItem
            {
                Title = "Dokumente",
                Items =
                {
                    new ShellContent
                    {
                        Title = "Dokumente",
                        ContentTemplate = new DataTemplate(typeof(DokumentePage))
                    }
                }
            });
        }

        Items.Add(new FlyoutItem
        {
            Title = "Beenden",
            Items =
            {
                new ShellContent
                {
                    Title = "Beenden",
                    Route = "exit",
                    ContentTemplate = new DataTemplate(typeof(ExitPage))
                }
            }
        });
    }
}

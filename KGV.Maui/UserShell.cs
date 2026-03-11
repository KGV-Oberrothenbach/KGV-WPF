using KGV.Maui.Pages;
using KGV.Maui.State;
using Microsoft.Extensions.DependencyInjection;

namespace KGV.Maui;

public sealed class UserShell : Shell, IAppShellInitializer
{
    private readonly IServiceProvider _services;
    private readonly UserContextState _state;

    public UserShell(IServiceProvider services, UserContextState state)
    {
        _services = services;
        _state = state;

        FlyoutBehavior = FlyoutBehavior.Flyout;
    }

    public void BuildMenu()
    {
        Items.Clear();

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

        // Start: direkt "Meine Stammdaten"
        Items.Add(new FlyoutItem
        {
            Title = "Meine Stammdaten",
            Items =
            {
                new ShellContent
                {
                    Title = "Meine Stammdaten",
                    Route = "myprofile",
                    ContentTemplate = new DataTemplate(() => _services.GetRequiredService<MyProfilePage>())
                }
            }
        });

        if (_state.CurrentNebenMitgliedId != null
            && _state.CurrentNebenMitgliedId.Value > 0
            && _state.CurrentNebenMitgliedId.Value <= int.MaxValue
            && _state.CurrentMitgliedId != null
            && _state.CurrentMitgliedId.Value > 0)
        {
            Items.Add(new FlyoutItem
            {
                Title = "Nebenmitglied",
                Items =
                {
                    new ShellContent
                    {
                        Title = "Nebenmitglied",
                        Route = "nebenmitglied",
                        ContentTemplate = new DataTemplate(() => _services.GetRequiredService<NebenmitgliedPage>())
                    }
                }
            });
        }

        Items.Add(new FlyoutItem
        {
            Title = "Meine Arbeitsstunden",
            Items =
            {
                new ShellContent
                {
                    Title = "Meine Arbeitsstunden",
                    Route = "workhours",
                    ContentTemplate = new DataTemplate(() => _services.GetRequiredService<MyArbeitsstundenPage>())
                }
            }
        });

        if (_state.CurrentMitgliedId != null
            && _state.CurrentMitgliedId.Value > 0
            && _state.CurrentMitgliedId.Value <= int.MaxValue)
        {
            Items.Add(new FlyoutItem
            {
                Title = "Meine Dokumente",
                Items =
                {
                    new ShellContent
                    {
                        Title = "Meine Dokumente",
                        Route = "mydocs",
                        ContentTemplate = new DataTemplate(() => _services.GetRequiredService<DokumentePage>())
                    }
                }
            });
        }

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

        if (Items.Count > 0)
            CurrentItem = Items[0];
    }
}

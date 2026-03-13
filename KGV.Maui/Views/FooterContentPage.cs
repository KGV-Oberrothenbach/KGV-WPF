using KGV.Maui.State;

namespace KGV.Maui.Views;

public class FooterContentPage : ContentPage
{
    private bool _isWrapped;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        EnsureWrapped();
    }

    private void EnsureWrapped()
    {
        if (_isWrapped)
            return;

        var body = Content;
        if (body == null)
            return;

        var state = TryResolveState();

        var footer = state != null
            ? new AppFooterView(state)
            : new ContentView();

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            }
        };

        Grid.SetRow(body, 0);
        Grid.SetRow(footer, 1);

        root.Children.Add(body);
        root.Children.Add(footer);

        Content = root;
        _isWrapped = true;
    }

    private static AppStatusState? TryResolveState()
    {
        try
        {
            var services = Application.Current?.Handler?.MauiContext?.Services;
            return services?.GetService<AppStatusState>();
        }
        catch
        {
            return null;
        }
    }
}

using KGV.Maui.State;

namespace KGV.Maui.Views;

public sealed class AppFooterView : ContentView
{
    public AppFooterView(AppStatusState state)
    {
        BindingContext = state;

        var update = new Label
        {
            FontSize = 12,
            TextColor = Colors.Gray,
            LineBreakMode = LineBreakMode.TailTruncation,
            VerticalTextAlignment = TextAlignment.Center
        };
        update.SetBinding(Label.TextProperty, nameof(AppStatusState.UpdateStatusText));

        var version = new Label
        {
            FontSize = 12,
            TextColor = Colors.Gray,
            VerticalTextAlignment = TextAlignment.Center
        };
        version.SetBinding(Label.TextProperty, nameof(AppStatusState.VersionText));

        var grid = new Grid
        {
            BackgroundColor = Colors.Transparent,
            Padding = new Thickness(12, 6),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        grid.Add(update, 1, 0);
        grid.Add(version, 2, 0);

        Content = grid;
    }
}

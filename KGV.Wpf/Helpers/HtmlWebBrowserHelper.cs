using System.Windows;
using System.Windows.Controls;

namespace KGV.Wpf.Helpers
{
    public static class HtmlWebBrowserHelper
    {
        public static readonly DependencyProperty HtmlProperty = DependencyProperty.RegisterAttached(
            "Html",
            typeof(string),
            typeof(HtmlWebBrowserHelper),
            new PropertyMetadata(string.Empty, OnHtmlChanged));

        public static string GetHtml(DependencyObject obj)
            => (string)obj.GetValue(HtmlProperty);

        public static void SetHtml(DependencyObject obj, string value)
            => obj.SetValue(HtmlProperty, value);

        private static void OnHtmlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not WebBrowser wb)
                return;

            void Navigate()
            {
                var html = GetHtml(wb) ?? string.Empty;

                // Minimaler Wrapper für Charset + einfache lesbare Darstellung.
                var wrapped =
                    "<html><head><meta charset='utf-8' />" +
                    "<style>body{font-family:Segoe UI,Arial,sans-serif;font-size:14px;margin:0;padding:0;}" +
                    "p{margin:0 0 8px 0;}</style>" +
                    "</head><body>" +
                    html +
                    "</body></html>";

                wb.NavigateToString(wrapped);
            }

            if (wb.IsLoaded)
            {
                Navigate();
                return;
            }

            RoutedEventHandler? onLoaded = null;
            onLoaded = (_, _) =>
            {
                wb.Loaded -= onLoaded;
                Navigate();
            };

            wb.Loaded += onLoaded;
        }
    }
}

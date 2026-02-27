// Datei: Helpers/WatermarkHelper.cs
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace KGV.Helpers
{
    public static class WatermarkHelper
    {
        public static readonly DependencyProperty WatermarkProperty =
            DependencyProperty.RegisterAttached(
                "Watermark",
                typeof(string),
                typeof(WatermarkHelper),
                new PropertyMetadata(null, OnWatermarkChanged));

        public static string GetWatermark(DependencyObject obj) => (string)obj.GetValue(WatermarkProperty);
        public static void SetWatermark(DependencyObject obj, string value) => obj.SetValue(WatermarkProperty, value);

        private static void OnWatermarkChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox tb)
            {
                if (DesignerProperties.GetIsInDesignMode(d))
                {
                    return;
                }

                tb.Loaded -= OnTextBoxLoaded;
                tb.Loaded += OnTextBoxLoaded;
            }
        }

        private static void OnTextBoxLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                ShowOrHideWatermark(tb);
                tb.TextChanged -= OnTextBoxTextChanged;
                tb.TextChanged += OnTextBoxTextChanged;
            }
        }

        private static void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                ShowOrHideWatermark(tb);
            }
        }

        private static void ShowOrHideWatermark(TextBox tb)
        {
            AdornerLayer? layer = AdornerLayer.GetAdornerLayer(tb);
            if (layer == null) return;

            var existing = layer.GetAdorners(tb)?.FirstOrDefault(a => a is WatermarkAdorner) as WatermarkAdorner;

            if (string.IsNullOrEmpty(tb.Text))
            {
                if (existing == null)
                {
                    layer.Add(new WatermarkAdorner(tb, GetWatermark(tb)));
                }
            }
            else
            {
                if (existing != null)
                    layer.Remove(existing);
            }
        }
    }

    public class WatermarkAdorner : Adorner
    {
        private readonly TextBlock _textBlock;

        public WatermarkAdorner(UIElement adornedElement, string watermark) : base(adornedElement)
        {
            _textBlock = new TextBlock
            {
                Text = watermark,
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(2, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            AddVisualChild(_textBlock);
        }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => _textBlock;

        protected override Size MeasureOverride(Size constraint)
        {
            _textBlock.Measure(constraint);
            return _textBlock.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _textBlock.Arrange(new Rect(finalSize));
            return finalSize;
        }
    }
}
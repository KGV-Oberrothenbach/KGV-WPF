using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KGV.Helpers
{
    public class BoolToVisibilityConverterLocal : IValueConverter
    {
        public bool InvertLocal { get; set; } = false;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool visible = value is bool b && b;
            if (InvertLocal) visible = !visible;
            return visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
            {
                bool result = v == Visibility.Visible;
                return InvertLocal ? !result : result;
            }
            return false;
        }
    }
}
using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace KGV.Helpers
{
    /// <summary>
    /// MultiValueConverter that returns the first non-null/non-empty string from its bindings.
    /// Useful when different model types use different property names (e.g. Name vs Nachname).
    /// </summary>
    public class FirstNonEmptyConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null) return string.Empty;
            foreach (var v in values)
            {
                // Skip unset bindings from WPF
                if (v == null) continue;
                if (object.ReferenceEquals(v, System.Windows.DependencyProperty.UnsetValue)) continue;

                var s = v as string ?? v.ToString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

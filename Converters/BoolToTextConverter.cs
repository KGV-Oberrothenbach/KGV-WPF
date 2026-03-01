using System;
using System.Globalization;
using System.Windows.Data;

namespace KGV.Converters;

public sealed class BoolToTextConverter : IValueConverter
{
    public string TrueText { get; set; } = "True";
    public string FalseText { get; set; } = "False";
    public string NullText { get; set; } = "";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value switch
        {
            bool b => b ? TrueText : FalseText,
            null => NullText,
            _ => NullText
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing; // Anzeige-only
}
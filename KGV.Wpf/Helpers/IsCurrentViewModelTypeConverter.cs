using System;
using System.Globalization;
using System.Windows.Data;

namespace KGV.Wpf.Helpers
{
    public sealed class IsCurrentViewModelTypeConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return false;

            var currentVm = values[0];
            var itemVmType = values[1] as Type;

            if (currentVm == null || itemVmType == null)
                return false;

            return itemVmType.IsInstanceOfType(currentVm);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

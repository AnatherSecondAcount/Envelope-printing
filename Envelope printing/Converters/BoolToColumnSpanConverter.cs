using System.Globalization;
using System.Windows.Data;

namespace Envelope_printing.Converters
{
    public sealed class BoolToColumnSpanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool expanded = value is bool b && b;
            return expanded ? 1 : 2; // when collapsed span both columns to center icon
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i)
                return i == 1; // reverse: span1 -> expanded
            return false;
        }
    }
}

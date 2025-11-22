using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Envelope_printing.Converters
{
    public sealed class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = false;
            if (value is bool bb) b = bb;
            return b ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
                return v != Visibility.Visible;
            return Binding.DoNothing;
        }
    }
}

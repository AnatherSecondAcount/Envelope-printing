using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Envelope_printing
{
    public class DoubleToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return new GridLength(0);
            if (value is double d)
            {
                // treat negative values as 0
                if (double.IsNaN(d) || d < 0) d = 0;
                return new GridLength(d);
            }
            if (double.TryParse(value.ToString(), out var parsed))
            {
                return new GridLength(parsed);
            }
            return new GridLength(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is GridLength gl)
            {
                return gl.Value;
            }
            return 0.0;
        }
    }
}

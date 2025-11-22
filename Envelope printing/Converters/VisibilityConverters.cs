using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Envelope_printing
{
    // Visible when numeric value is0 (e.g., list count==0), collapsed otherwise
    public class ZeroToVisibleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value == null) return Visibility.Visible;
                double d = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return Math.Abs(d) < double.Epsilon ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { return Visibility.Collapsed; }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}

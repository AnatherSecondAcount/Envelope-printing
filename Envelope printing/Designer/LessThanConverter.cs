using System.Globalization;
using System.Windows.Data;

namespace Envelope_printing.Designer
{
    public class LessThanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double actualWidth && parameter is string thresholdString &&
            double.TryParse(thresholdString, out double threshold))
            {
                return actualWidth < threshold;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

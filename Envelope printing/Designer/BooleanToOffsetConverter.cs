using System.Globalization;
using System.Windows.Data;

namespace Envelope_printing
{
    // Converts a bool to an offset (double) for TranslateTransform.X.
    // True ->0 (panel visible). False -> ConverterParameter (default308).
    public class BooleanToOffsetConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isOpen = value is bool b && b;
            if (isOpen) return 0d;
            double offset = 308d;
            if (parameter != null && double.TryParse(parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var p))
                offset = p;
            return offset;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                double offset = 308d;
                if (parameter != null && double.TryParse(parameter.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var p))
                    offset = p;
                return Math.Abs(d) < 0.5; // ~0 => true
            }
            return false;
        }
    }
}

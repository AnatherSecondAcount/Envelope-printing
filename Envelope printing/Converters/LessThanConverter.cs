using System.Globalization;
using System.Windows.Data;

namespace Envelope_printing
{
    /// <summary>
    /// Returns true if value (double) is less than the numeric ConverterParameter.
    /// </summary>
    public sealed class LessThanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double actual && parameter != null && double.TryParse(parameter.ToString(), out var threshold))
                return actual < threshold;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
    }
}

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Envelope_printing
{
    /// <summary>
    /// Конвертер, который преобразует true в Collapsed и false в Visible.
    /// Обратный стандартному BooleanToVisibilityConverter.
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = false;
            if (value is bool v)
            {
                boolValue = v;
            }

            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
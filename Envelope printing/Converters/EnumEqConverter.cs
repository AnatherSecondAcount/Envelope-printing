using System.Globalization;
using System.Windows.Data;

namespace Envelope_printing
{
    // Binds RadioButton.IsChecked to enum equality (value == parameter)
    public class EnumEqConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            var s = parameter.ToString();
            if (value.GetType().IsEnum && Enum.IsDefined(value.GetType(), value))
            {
                return string.Equals(value.ToString(), s, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool b) || !b || parameter == null) return Binding.DoNothing;
            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, parameter.ToString(), true);
            }
            return Binding.DoNothing;
        }
    }
}

using System;
using System.Globalization;
using System.Windows.Data;

namespace Envelope_printing.Converters
{
    public class SelectedItemDisplayConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            foreach (var v in values)
            {
                if (v == null) continue;
                var s = v.ToString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
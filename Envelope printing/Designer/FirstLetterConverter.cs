using System;
using System.Globalization;
using System.Windows.Data;

namespace Envelope_printing
{
    public class FirstLetterConverter : IValueConverter
    {
        public static readonly FirstLetterConverter Instance = new FirstLetterConverter();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s && s.Length > 0) return s.Substring(0,1).ToUpperInvariant();
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

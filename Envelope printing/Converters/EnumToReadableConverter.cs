using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace Envelope_printing.Converters
{
    public sealed class EnumToReadableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;
            var s = value.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            // Replace underscores with spaces
            s = s.Replace('_', ' ');
            // Split PascalCase: PhotographicHighGloss -> Photographic High Gloss
            s = Regex.Replace(s, "(?<=[a-z])(?=[A-Z])", " ");
            // Fix common abbreviations
            s = s.Replace("ISOA", "ISO A");
            // Specific niceties
            s = s.Replace("Semi Gloss", "Semi-Gloss");
            return s.Trim();
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}

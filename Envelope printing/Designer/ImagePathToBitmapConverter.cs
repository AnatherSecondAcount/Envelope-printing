using Envelope_printing.Utils;
using System.Globalization;
using System.Windows.Data;

namespace Envelope_printing
{
    // Simple converter that loads image from path with limited decode size to reduce memory usage.
    // Uses central ImageCache to avoid duplicate decoding.
    public class ImagePathToBitmapConverter : IValueConverter
    {
        // Maximum decode width in pixels. Adjust according to expected display size.
        private const int MaxDecodeWidth = 1024;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value == null) return null;
                var path = value as string;
                if (string.IsNullOrWhiteSpace(path)) return null;

                return ImageCache.Get(path, MaxDecodeWidth);
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

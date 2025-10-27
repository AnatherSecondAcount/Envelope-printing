using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Envelope_printing
{
    // Simple converter that loads image from path with limited decode size to reduce memory usage.
    // Keeps BitmapImage frozen and uses OnLoad to release file handle.
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

                // If path is not a file path, try to treat as URI
                BitmapImage bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad; // load into memory and release file
                bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;

                if (Uri.IsWellFormedUriString(path, UriKind.Absolute))
                {
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                }
                else
                {
                    var abs = Path.GetFullPath(path);
                    if (!File.Exists(abs)) return null;
                    bmp.UriSource = new Uri(abs, UriKind.Absolute);
                }

                // Set decode width to limit memory. If image is small, decoder will ignore.
                bmp.DecodePixelWidth = MaxDecodeWidth;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
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

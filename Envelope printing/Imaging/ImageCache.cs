using System;
using System.IO;
using System.Windows.Media.Imaging;
using Envelope_printing.Utils;

namespace Envelope_printing.Utils
{
 public static class ImageCache
 {
 private static readonly LruCache<string, BitmapImage> _cache = new(128);

 public static BitmapImage Get(string path, int maxDecodeWidth =0)
 {
 if (string.IsNullOrWhiteSpace(path)) return null;
 try
 {
 var key = path + "|" + maxDecodeWidth;
 return _cache.GetOrAdd(key, k => LoadBitmap(path, maxDecodeWidth));
 }
 catch
 {
 return null;
 }
 }

 private static BitmapImage LoadBitmap(string path, int maxDecodeWidth)
 {
 try
 {
 string abs = path;
 if (!Uri.IsWellFormedUriString(path, UriKind.Absolute))
 {
 abs = Path.GetFullPath(path);
 }
 if (!File.Exists(abs) && !Uri.IsWellFormedUriString(path, UriKind.Absolute)) return null;

 var bmp = new BitmapImage();
 bmp.BeginInit();
 bmp.CacheOption = BitmapCacheOption.OnLoad;
 bmp.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
 if (maxDecodeWidth >0) bmp.DecodePixelWidth = maxDecodeWidth;

 bmp.UriSource = new Uri(abs, UriKind.Absolute);
 bmp.EndInit();
 bmp.Freeze();
 return bmp;
 }
 catch
 {
 return null;
 }
 }

 public static void Clear() => _cache.Clear();
 }
}

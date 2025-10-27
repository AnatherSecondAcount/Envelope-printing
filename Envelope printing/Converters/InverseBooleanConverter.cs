using System;
using System.Globalization;
using System.Windows.Data;

namespace Envelope_printing
{
 /// <summary>
 /// Inverts a boolean value: true -> false, false -> true.
 /// </summary>
 public class InverseBooleanConverter : IValueConverter
 {
 public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
 {
 if (value is bool b) return !b;
 return value;
 }

 public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
 {
 if (value is bool b) return !b;
 return value;
 }
 }
}

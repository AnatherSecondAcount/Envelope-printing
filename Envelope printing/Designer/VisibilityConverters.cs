using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Envelope_printing
{
 // Converts two values (e.g., SelectedItem and current item) to true if they are equal (reference or value equality)
 public class EqualityMultiConverter : IMultiValueConverter
 {
 public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
 {
 if (values == null || values.Length <2)
 return false;
 var a = values[0];
 var b = values[1];
 if (ReferenceEquals(a, b)) return true;
 if (a == null || b == null) return false;
 return a.Equals(b);
 }

 public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
 {
 throw new NotSupportedException();
 }
 }
}

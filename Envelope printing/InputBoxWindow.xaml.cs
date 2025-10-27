using System.Windows;

namespace Envelope_printing
{
 public partial class InputBoxWindow : Window
 {
 public string InputText => InputBox.Text;
 public InputBoxWindow(string title, string message)
 {
 InitializeComponent();
 TitleText.Text = title;
 MessageText.Text = message;
 }
 private void Ok_Click(object sender, RoutedEventArgs e)
 {
 DialogResult = true;
 }
 }
}

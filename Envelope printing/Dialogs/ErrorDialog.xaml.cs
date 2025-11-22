using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Envelope_printing
{
    public partial class ErrorDialog : Window
    {
        private readonly string _logPath;
        public ErrorDialog(string message, Exception ex, string logPath)
        {
            InitializeComponent();
            _logPath = logPath;
            DetailsBox.Text = BuildText(message, ex);
            LogPathText.Text = string.IsNullOrWhiteSpace(logPath) ? string.Empty : logPath;
        }
        private static string BuildText(string message, Exception ex)
        {
            try { return (message ?? "") + Environment.NewLine + Environment.NewLine + (ex?.ToString() ?? ""); }
            catch { return message; }
        }
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current?.Shutdown(-1);
        }
        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(DetailsBox.Text); } catch { }
        }
        private void OpenLog_Click(object sender, RoutedEventArgs e)
        {
            try { if (!string.IsNullOrWhiteSpace(_logPath) && File.Exists(_logPath)) Process.Start(new ProcessStartInfo { FileName = _logPath, UseShellExecute = true }); } catch { }
        }
    }
}

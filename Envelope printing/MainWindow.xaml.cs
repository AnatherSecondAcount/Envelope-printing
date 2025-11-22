using System.Windows;
using System.Windows.Input;

namespace Envelope_printing
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // DataContext is set by App after initialization. If not, create default VM.
            if (DataContext == null)
                DataContext = new ShellViewModel();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void MaxRestore_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                MaxRestore_Click(sender, e);
            }
            else
            {
                try { DragMove(); } catch { }
            }
        }
    }
}
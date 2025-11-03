using System.Windows;

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
    }
}
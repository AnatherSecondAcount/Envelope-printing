using System;
using System.Windows;

namespace Envelope_printing
{
    public partial class PrintProgressWindow : Window
    {
        public event EventHandler CancelRequested;

        public PrintProgressWindow()
        {
            InitializeComponent();
        }

        public void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try { CancelRequested?.Invoke(this, EventArgs.Empty); } catch { }
            Close();
        }
    }
}

using System.Windows;
using System.Windows.Controls;

namespace Envelope_printing
{
    public partial class HomeView : UserControl
    {
        // Reduced threshold: switch to 2x2 only when width < 900
        private const double TwoColumnThreshold = 910;

        public HomeView()
        {
            InitializeComponent();
        }

        private void HomeView_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyResponsiveLayout();
        }

        private void HomeView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveLayout();
        }

        private void ApplyResponsiveLayout()
        {
            if (QuickActionsGrid == null) return;
            double width = ActualWidth;
            if (width <= 0 && Window.GetWindow(this) != null)
                width = Window.GetWindow(this).ActualWidth;

            if (width < TwoColumnThreshold)
            {
                QuickActionsGrid.Columns = 2;
                QuickActionsGrid.Rows = 2;
                foreach (var child in QuickActionsGrid.Children)
                {
                    if (child is Button btn)
                    {
                        btn.Margin = new Thickness(0, 0, 12, 12);
                    }
                }
            }
            else
            {
                QuickActionsGrid.Columns = 4;
                QuickActionsGrid.Rows = 1;
                int index = 0;
                int last = QuickActionsGrid.Children.Count - 1;
                foreach (var child in QuickActionsGrid.Children)
                {
                    if (child is Button btn)
                    {
                        btn.Margin = index == last ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 12, 12);
                        index++;
                    }
                }
            }
        }
    }
}

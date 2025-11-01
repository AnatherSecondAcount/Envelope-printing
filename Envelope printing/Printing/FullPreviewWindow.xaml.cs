using System.Windows;
using System.Windows.Input;

namespace Envelope_printing
{
    public partial class FullPreviewWindow : Window
    {
        public FullPreviewWindow()
        {
            InitializeComponent();
        }

        private Point? _panStart;
        private Point _scrollStart;

        private void PreviewArea_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && DataContext is PrintPreviewViewModel vm)
            {
                int step = e.Delta > 0 ? 10 : -10;
                vm.ZoomPercentage = System.Math.Clamp(vm.ZoomPercentage + step, 20, 200);
                e.Handled = true;
            }
        }

        private void PreviewArea_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                _panStart = e.GetPosition(PreviewArea);
                _scrollStart = new Point(PreviewArea.HorizontalOffset, PreviewArea.VerticalOffset);
                PreviewArea.Cursor = Cursors.SizeAll;
                PreviewArea.CaptureMouse();
                e.Handled = true;
            }
        }

        private void PreviewArea_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_panStart.HasValue && e.MiddleButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(PreviewArea);
                var dx = pos.X - _panStart.Value.X;
                var dy = pos.Y - _panStart.Value.Y;
                PreviewArea.ScrollToHorizontalOffset(_scrollStart.X - dx);
                PreviewArea.ScrollToVerticalOffset(_scrollStart.Y - dy);
                e.Handled = true;
            }
        }

        private void PreviewArea_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_panStart.HasValue && e.ChangedButton == MouseButton.Middle)
            {
                _panStart = null;
                PreviewArea.ReleaseMouseCapture();
                PreviewArea.Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Envelope_printing.Designer
{
    public partial class TemplateDesignerView : UserControl
    {
        private Point _dragStartPoint;
        private Point _dragStartItemPos;
        private FrameworkElement _currentColorInvoker;
        private string _currentColorTargetProp;

        private bool _isDragging;
        private int _clicks;
        private const int DoubleClickThresholdMs = 300;
        private System.Diagnostics.Stopwatch _clickTimer = new System.Diagnostics.Stopwatch();

        private bool _isPanning;
        private Point _panStart;
        private Point _scrollStart;

        private const double ArrowMargin = 10;
        private const double ArrowWidth = 36;
        // New: extra left shifts for nicer in-panel padding in both states
        private const double ArrowOpenExtraLeft = 12; // when panel is open, move a bit more to the left (deeper into panel)
        private const double ArrowClosedExtraLeft = 6; // when panel is closed, nudge slightly left from the base margin

        private bool _initialZoomApplied = false;

        private Point _resizeStart;
        private double _startWidth;
        private double _startHeight;

        private Point _rotateStartCenter;
        private double _startAngle;

        private double _rotationOffsetDeg; // keeps difference between item angle and mouse vector at drag start

        public TemplateDesignerView()
        {
            InitializeComponent();
            Loaded += TemplateDesignerView_Loaded;
            DataContextChanged += TemplateDesignerView_DataContextChanged;
            PreviewKeyDown += TemplateDesignerView_PreviewKeyDown;
            PreviewMouseWheel += TemplateDesignerView_PreviewMouseWheel; // CTRL + wheel
        }

        private void TemplateDesignerView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && DataContext is Envelope_printing.TemplateDesignerViewModel vm)
            {
                if (e.Delta >0) { if (vm.ZoomInCommand.CanExecute(null)) vm.ZoomInCommand.Execute(null); }
                else { if (vm.ZoomOutCommand.CanExecute(null)) vm.ZoomOutCommand.Execute(null); }
                e.Handled = true;
            }
        }

        private void TemplateDesignerView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is Envelope_printing.TemplateDesignerViewModel vm)
            {
                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                if (ctrl && (e.Key == Key.OemPlus || e.Key == Key.Add)) { if (vm.ZoomInCommand.CanExecute(null)) vm.ZoomInCommand.Execute(null); e.Handled = true; }
                if (ctrl && (e.Key == Key.OemMinus || e.Key == Key.Subtract)) { if (vm.ZoomOutCommand.CanExecute(null)) vm.ZoomOutCommand.Execute(null); e.Handled = true; }
                if (ctrl && (e.Key == Key.D0 || e.Key == Key.NumPad0)) { if (vm.ResetZoomCommand.CanExecute(null)) vm.ResetZoomCommand.Execute(null); e.Handled = true; }
                if (e.Key == Key.Delete && vm.SelectedTemplateItem != null)
                {
                    if (vm.DeleteItemCommand.CanExecute(null)) vm.DeleteItemCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private void TemplateDesignerView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateArrowPosition(animated: false);
            _initialZoomApplied = false;
        }

        private void TemplateDesignerView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is Envelope_printing.TemplateDesignerViewModel vm)
            {
                vm.GetViewSizeRequested += () => new Size(ActualWidth, ActualHeight);
                vm.PropertyChanged += (s, ev) =>
                {
                    if (ev.PropertyName == nameof(Envelope_printing.TemplateDesignerViewModel.IsLeftPanelVisible))
                    {
                        Dispatcher.Invoke(() => UpdateArrowPosition(animated: true));
                    }
                };
            }
            SizeChanged += (s, ev) =>
            {
                UpdateArrowPosition(animated: false);
                if (!_initialZoomApplied && DataContext is Envelope_printing.TemplateDesignerViewModel vm2)
                {
                    _initialZoomApplied = true;
                    // Defer to next tick after layout
                    Dispatcher.BeginInvoke(new Action(() => vm2.CalculateAndSetInitialZoom()), System.Windows.Threading.DispatcherPriority.Background);
                }
            };
            UpdateArrowPosition(animated: false);
        }

        private void OnZoomPercentRightClick(object sender, MouseButtonEventArgs e)
        {
            if (ZoomPopup != null && ZoomPanelBorder != null)
            {
                ZoomPopup.IsOpen = true;
                // center popup horizontally and add a bit more gap from the panel below
                ZoomPopup.HorizontalOffset = (ZoomPanelBorder.ActualWidth - ZoomPopupContent.ActualWidth) /2.0;
                ZoomPopup.VerticalOffset = -6;
                e.Handled = true;
            }
        }

        private void UpdateArrowPosition(bool animated)
        {
            if (ArrowToggle == null) return;
            double targetX;
            bool isLeftOpen = (DataContext as Envelope_printing.TemplateDesignerViewModel)?.IsLeftPanelVisible == true;
            double panelWidth = LeftOverlayPanel != null && LeftOverlayPanel.ActualWidth >0 ? LeftOverlayPanel.ActualWidth :280;
            if (isLeftOpen && panelWidth >0)
            {
                // Position near the right inner edge of the panel, then move a bit further left for nicer padding
                targetX = Math.Max(ArrowMargin, panelWidth - ArrowWidth - ArrowMargin - ArrowOpenExtraLeft);
                (ArrowToggle.Content as TextBlock)!.Text = "\uE76B"; // left arrow
            }
            else
            {
                // Base margin from left, then nudge left
                targetX = Math.Max(0, ArrowMargin - ArrowClosedExtraLeft);
                (ArrowToggle.Content as TextBlock)!.Text = "\uE76C"; // right arrow
            }
            var tt = ArrowToggle.RenderTransform as System.Windows.Media.TranslateTransform;
            if (tt == null) return;
            if (animated)
            {
                var anim = new DoubleAnimation { To = targetX, Duration = TimeSpan.FromMilliseconds(180), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
                tt.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, anim);
            }
            else
            {
                tt.X = targetX;
            }
        }

        // dragging and hide panel handlers unchanged below
        private void OnItemMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is Envelope_printing.TemplateDesignerViewModel vm)
            {
                var element = sender as FrameworkElement;
                var item = element?.DataContext as Envelope_printing.TemplateItemViewModel;
                if (item == null) return;

                // Click counting for double-click
                if (!_clickTimer.IsRunning || _clickTimer.ElapsedMilliseconds > DoubleClickThresholdMs)
                {
                    _clicks = 0;
                    _clickTimer.Restart();
                }
                _clicks++;

                if (_clicks >= 2)
                {
                    // Double click: open item properties panel
                    vm.ReopenItemProperties(item);
                    _clicks = 0;
                    _clickTimer.Reset();
                    e.Handled = true;
                    return;
                }

                // single click: just select and start drag; do NOT open properties automatically
                _isDragging = true;
                _dragStartPoint = e.GetPosition(ItemsCanvas);
                _dragStartItemPos = new Point(item.Transform.X, item.Transform.Y);
                vm.StartDragging(item, _dragStartPoint);
                element?.CaptureMouse();
                e.Handled = true;
            }
        }

        private void OnItemMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;
            if (DataContext is Envelope_printing.TemplateDesignerViewModel vm)
            {
                var currentOnCanvas = e.GetPosition(ItemsCanvas);
                var dx = currentOnCanvas.X - _dragStartPoint.X;
                var dy = currentOnCanvas.Y - _dragStartPoint.Y;
                if (vm.SelectedTemplateItem != null)
                {
                    // With Rotate then Translate, translation is applied in canvas space; add raw dx/dy
                    vm.SelectedTemplateItem.Transform.X = _dragStartItemPos.X + dx;
                    vm.SelectedTemplateItem.Transform.Y = _dragStartItemPos.Y + dy;
                }
            }
        }

        private void OnItemMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                if (sender is IInputElement el) el.ReleaseMouseCapture();
                if (DataContext is Envelope_printing.TemplateDesignerViewModel vm)
                {
                    vm.SelectedTemplateItem.PositionX = vm.SelectedTemplateItem.Transform.X;
                    vm.SelectedTemplateItem.PositionY = vm.SelectedTemplateItem.Transform.Y;
                    vm.StopDragging();
                }
            }
        }

        private void OnCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is Envelope_printing.TemplateDesignerViewModel vm)
            {
                vm.SelectedTemplateItem = null; // clear selection -> outline hides
                vm.HideProperties();
            }
        }

        private void OnViewportMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is Envelope_printing.TemplateDesignerViewModel vm)
            {
                vm.SelectedTemplateItem = null; // clear selection -> outline hides
                vm.HideProperties();
            }
        }

        private void OnScrollViewerMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                _isPanning = true;
                _panStart = e.GetPosition(DesignerScrollViewer);
                _scrollStart = new Point(DesignerScrollViewer.HorizontalOffset, DesignerScrollViewer.VerticalOffset);
                Mouse.Capture(DesignerScrollViewer);
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.Left)
            {
                if (DataContext is Envelope_printing.TemplateDesignerViewModel vm)
                {
                    vm.HideProperties();
                }
            }
        }

        private void OnScrollViewerMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                var pos = e.GetPosition(DesignerScrollViewer);
                var delta = pos - _panStart;
                DesignerScrollViewer.ScrollToHorizontalOffset(_scrollStart.X - delta.X);
                DesignerScrollViewer.ScrollToVerticalOffset(_scrollStart.Y - delta.Y);
                e.Handled = true;
            }
        }

        private void OnScrollViewerMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning && e.ChangedButton == MouseButton.Middle)
            {
                _isPanning = false;
                if (Mouse.Captured == DesignerScrollViewer) Mouse.Capture(null);
                e.Handled = true;
            }
        }

        private void OnResizeStart(object sender, DragStartedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Envelope_printing.TemplateItemViewModel vm)
            {
                _resizeStart = Mouse.GetPosition(ItemsCanvas);
                _startWidth = vm.Width; _startHeight = vm.Height;
            }
        }
        private void OnResizeEnd(object sender, DragCompletedEventArgs e)
        {
            _startWidth = _startHeight =0;
            if (DataContext is Envelope_printing.TemplateDesignerViewModel vm)
            {
                vm.ValidateAllItemsBounds();
            }
        }

        private void OnResizeE(object sender, DragDeltaEventArgs e)
        {
            if (!(sender is FrameworkElement fe && fe.DataContext is Envelope_printing.TemplateItemViewModel vm)) return;
            var pos = Mouse.GetPosition(ItemsCanvas);
            var dx = pos.X - _resizeStart.X;
            vm.Width = Math.Max(5, _startWidth + dx);
            e.Handled = true;
        }
        private void OnResizeS(object sender, DragDeltaEventArgs e)
        {
            if (!(sender is FrameworkElement fe && fe.DataContext is Envelope_printing.TemplateItemViewModel vm)) return;
            var pos = Mouse.GetPosition(ItemsCanvas);
            var dy = pos.Y - _resizeStart.Y;
            vm.Height = Math.Max(5, _startHeight + dy);
            e.Handled = true;
        }
        private void OnResizeSE(object sender, DragDeltaEventArgs e)
        {
            if (!(sender is FrameworkElement fe && fe.DataContext is Envelope_printing.TemplateItemViewModel vm)) return;
            var pos = Mouse.GetPosition(ItemsCanvas);
            var dx = pos.X - _resizeStart.X;
            var dy = pos.Y - _resizeStart.Y;
            vm.Width = Math.Max(5, _startWidth + dx);
            vm.Height = Math.Max(5, _startHeight + dy);
            e.Handled = true;
        }

        private void OnPickColorClick(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is Envelope_printing.TemplateDesignerViewModel vm) || vm.SelectedTemplateItem == null) return;
            if (sender is FrameworkElement fe && fe.Tag is string target)
            {
                var dlg = new System.Windows.Forms.ColorDialog();
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string hex = $"#{dlg.Color.A:X2}{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                    switch (target)
                    {
                        case "ForegroundString":
                            vm.SelectedTemplateItem.ForegroundString = hex;
                            vm.SelectedTemplateItem.ForegroundChoice = "PALETTE";
                            break;
                        case "BackgroundString":
                            vm.SelectedTemplateItem.BackgroundString = hex;
                            vm.SelectedTemplateItem.BackgroundChoice = "PALETTE";
                            break;
                        case "BorderBrushString":
                            vm.SelectedTemplateItem.BorderBrushString = hex;
                            vm.SelectedTemplateItem.BorderBrushChoice = "PALETTE";
                            break;
                    }
                }
            }
        }

        private void OnRotateStart(object sender, DragStartedEventArgs e)
        {
            if (!(sender is FrameworkElement fe && fe.DataContext is Envelope_printing.TemplateItemViewModel vm)) return;
            var itemRoot = FindAncestor<Grid>(fe); // inside DataTemplate Grid
            if (itemRoot == null) return;
            // center based on RenderTransformOrigin (0.5,0.5) in canvas coordinates
            var bounds = itemRoot.TransformToAncestor(ItemsCanvas).TransformBounds(new Rect(0,0, itemRoot.ActualWidth, itemRoot.ActualHeight));
            _rotateStartCenter = new Point(bounds.Left + bounds.Width /2, bounds.Top + bounds.Height /2);
            _startAngle = vm.RotationDegrees;
            // Compute initial mouse angle to avoid immediate snap/jump
            var m0 = Mouse.GetPosition(ItemsCanvas);
            var dx0 = m0.X - _rotateStartCenter.X;
            var dy0 = m0.Y - _rotateStartCenter.Y;
            var initialMouseAngle = Math.Atan2(dy0, dx0) *180.0 / Math.PI;
            _rotationOffsetDeg = _startAngle - initialMouseAngle;
        }
        private static T FindAncestor<T>(DependencyObject child) where T: DependencyObject
        {
            DependencyObject current = child;
            while(current != null && current is not T)
                current = VisualTreeHelper.GetParent(current);
            return current as T;
        }
        private void OnRotateDrag(object sender, DragDeltaEventArgs e)
        {
            if (!(DataContext is Envelope_printing.TemplateDesignerViewModel root) || root.SelectedTemplateItem == null) return;
            var mouse = Mouse.GetPosition(ItemsCanvas);
            var dx = mouse.X - _rotateStartCenter.X;
            var dy = mouse.Y - _rotateStartCenter.Y;
            var mouseAngle = Math.Atan2(dy, dx) *180.0 / Math.PI;
            var angle = mouseAngle + _rotationOffsetDeg;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                angle = Math.Round(angle /15.0) *15.0;
            }
            // Normalize to [-180;180) for consistency
            while (angle >=180) angle -=360;
            while (angle < -180) angle +=360;
            root.SelectedTemplateItem.RotationDegrees = angle;
        }
        private void OnRotateEnd(object sender, DragCompletedEventArgs e)
        {
            if (DataContext is Envelope_printing.TemplateDesignerViewModel vm)
            {
                vm.ValidateAllItemsBounds();
            }
        }
    }
}
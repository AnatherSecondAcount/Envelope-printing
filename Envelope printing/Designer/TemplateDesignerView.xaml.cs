using System.Diagnostics;
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
        private const double DragUpdateThreshold = 0.2; // минимальный порог пикселей дл€ обновлени€ позиции

        private bool _isPanning;
        private Point _panStart;
        private Point _scrollStart;

        private const double ArrowMargin = 10;
        private const double ArrowWidth = 36;
        // New: extra left shifts for nicer in-panel padding in both states
        private const double ArrowOpenExtraLeft = 24; // move slightly more to the left when panel is open
        private const double ArrowClosedExtraLeft = 8; // keep a small inset from window edge when panel is closed

        private bool _initialZoomApplied = false;

        private Point _resizeStart;
        private double _startWidth;
        private double _startHeight;

        private Point _rotateStartCenter;
        private double _startAngle;

        private double _rotationOffsetDeg; // keeps difference between item angle and mouse vector at drag start

        private Stopwatch _dragStopwatch = new Stopwatch();
        private readonly double _dragThrottleMs = 1000.0 / 60.0; // ~60Hz throttle
        private bool _interactiveLowQualitySet = false;

        public TemplateDesignerView()
        {
            InitializeComponent();
            Loaded += TemplateDesignerView_Loaded;
            DataContextChanged += TemplateDesignerView_DataContextChanged;
            PreviewKeyDown += TemplateDesignerView_PreviewKeyDown;
            PreviewMouseWheel += TemplateDesignerView_PreviewMouseWheel; // CTRL + wheel
            Unloaded += TemplateDesignerView_Unloaded; // commit edits on view unload
        }

        private void TemplateDesignerView_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Ensure that any focused editor (TextBox) pushes binding values back into VM
                // Move focus away and clear it
                FocusManager.SetFocusedElement(this, this);
                Keyboard.ClearFocus();
            }
            catch { }
        }

        private void TemplateDesignerView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && DataContext is Envelope_printing.TemplateDesignerViewModel vm)
            {
                if (e.Delta > 0) { if (vm.ZoomInCommand.CanExecute(null)) vm.ZoomInCommand.Execute(null); }
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
                ZoomPopup.HorizontalOffset = (ZoomPanelBorder.ActualWidth - ZoomPopupContent.ActualWidth) / 2.0;
                ZoomPopup.VerticalOffset = -6;
                e.Handled = true;
            }
        }

        private void UpdateArrowPosition(bool animated)
        {
            if (ArrowToggle == null) return;
            double targetX;
            bool isLeftOpen = (DataContext as Envelope_printing.TemplateDesignerViewModel)?.IsLeftPanelVisible == true;
            double panelWidth = LeftOverlayPanel != null && LeftOverlayPanel.ActualWidth > 0 ? LeftOverlayPanel.ActualWidth : 280;
            var tb = ArrowToggle.Content as TextBlock;
            if (isLeftOpen && panelWidth > 0)
            {
                // When panel is open: compute button so its right edge is inset from the panel's right outer edge
                // by ArrowOpenExtraLeft (same as bottom inset). We'll compute panel's absolute right and set
                // desired absolute left for the button accordingly.
                double desiredAbsoluteLeft = ArrowMargin; // fallback
                try
                {
                    if (LeftOverlayPanel != null && Root != null)
                    {
                        var panelOrigin = LeftOverlayPanel.TransformToAncestor(Root).Transform(new Point(0, 0));
                        double panelRightAbsolute = panelOrigin.X + LeftOverlayPanel.ActualWidth;
                        // desired left so that button right edge = panelRightAbsolute - ArrowOpenExtraLeft
                        desiredAbsoluteLeft = panelRightAbsolute - ArrowOpenExtraLeft - ArrowWidth;
                    }
                    else
                    {
                        desiredAbsoluteLeft = ArrowMargin + panelWidth - ArrowWidth - ArrowOpenExtraLeft;
                    }
                }
                catch
                {
                    desiredAbsoluteLeft = ArrowMargin + panelWidth - ArrowWidth - ArrowOpenExtraLeft;
                }
                // Translate.X = desiredAbsoluteLeft - ArrowToggle.Margin.Left
                targetX = desiredAbsoluteLeft - (ArrowToggle?.Margin.Left ?? 0);
                if (tb != null) tb.Text = "\uE76B"; // left arrow
            }
            else
            {
                // When panel is closed, move to the very left window edge with small inset (absolute left = ArrowClosedExtraLeft).
                // TranslateTransform.X = desiredAbsoluteLeft - baseLeft
                double baseLeft = ArrowToggle?.Margin.Left ?? 0;
                double desiredAbsoluteLeft = ArrowClosedExtraLeft;
                targetX = desiredAbsoluteLeft - baseLeft;
                if (tb != null) tb.Text = "\uE76C"; // right arrow
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

        private void OnArrowToggleClick(object sender, RoutedEventArgs e)
        {
            // Toggle is bound to VM.IsLeftPanelVisible two-way; ensure arrow animates to new location
            UpdateArrowPosition(animated: true);
            e.Handled = true;
        }

        private static void SetCacheForItemVisual(FrameworkElement fe, bool enable)
        {
            var root = FindAncestor<Grid>(fe);
            if (root != null)
            {
                if (enable)
                {
                    try
                    {
                        // Compute render scale to create cache at correct resolution and avoid blur when scaled
                        float renderScale = 1f;
                        // Try to get zoom factor from nearest TemplateDesignerView's DataContext
                        var tdv = FindAncestor<TemplateDesignerView>(fe);
                        if (tdv != null && tdv.DataContext is Envelope_printing.TemplateDesignerViewModel vm)
                        {
                            double zoom = vm.ZoomFactor;
                            // Designer has a fixed layout scale (px per mm) of3.78 in XAML; include it
                            const double basePxPerMm = 3.78;
                            var dpi = VisualTreeHelper.GetDpi(fe);
                            renderScale = (float)(basePxPerMm * zoom * dpi.DpiScaleX);
                        }
                        else
                        {
                            var dpi = VisualTreeHelper.GetDpi(fe);
                            renderScale = (float)dpi.DpiScaleX;
                        }
                        if (renderScale < 1f) renderScale = 1f;
                        root.CacheMode = new BitmapCache { RenderAtScale = renderScale };
                        // Prefer high-quality scaling for cached visuals to reduce perceived blurriness
                        RenderOptions.SetBitmapScalingMode(root, BitmapScalingMode.HighQuality);
                    }
                    catch
                    {
                        // fallback to simple cache
                        root.CacheMode = new BitmapCache();
                    }
                }
                else
                {
                    root.CacheMode = null;
                    // reset scaling mode
                    RenderOptions.SetBitmapScalingMode(root, BitmapScalingMode.LowQuality);
                }
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

                if (e.ClickCount >= 2)
                {
                    vm.ReopenItemProperties(item);
                    e.Handled = true;
                    return;
                }

                // single click: just select and start drag; do NOT open properties automatically
                _isDragging = true;
                _dragStartPoint = e.GetPosition(ItemsCanvas);
                _dragStartItemPos = new Point(item.Transform.X, item.Transform.Y);
                vm.StartDragging(item, _dragStartPoint);
                SetCacheForItemVisual(element, true); // ускор€ем перетаскивание
                // Start throttle timer and reduce rendering quality for smoother dragging on high-refresh displays
                _dragStopwatch.Restart();
                try
                {
                    // Lower bitmap scaling quality globally on the designer root to reduce GPU load
                    if (Root != null)
                    {
                        RenderOptions.SetBitmapScalingMode(Root, BitmapScalingMode.LowQuality);
                        _interactiveLowQualitySet = true;
                    }
                }
                catch { }
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
                if (Math.Abs(dx) < DragUpdateThreshold && Math.Abs(dy) < DragUpdateThreshold) return; // избегаем лишних апдейтов
                if (vm.SelectedTemplateItem != null)
                {
                    // Throttle updates to reduce updates on high-refresh-rate displays
                    if (!_dragStopwatch.IsRunning || _dragStopwatch.Elapsed.TotalMilliseconds >= _dragThrottleMs)
                    {
                        vm.SelectedTemplateItem.Transform.X = _dragStartItemPos.X + dx;
                        vm.SelectedTemplateItem.Transform.Y = _dragStartItemPos.Y + dy;
                        _dragStopwatch.Restart();
                    }
                }
            }
        }

        private void OnItemMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                if (sender is FrameworkElement el)
                {
                    el.ReleaseMouseCapture();
                    SetCacheForItemVisual(el, false); // снимаем кеш
                }
                if (DataContext is Envelope_printing.TemplateDesignerViewModel vm)
                {
                    if (vm.SelectedTemplateItem != null)
                    {
                        // Ensure final position applied
                        vm.SelectedTemplateItem.Transform.X = vm.SelectedTemplateItem.Transform.X;
                        vm.SelectedTemplateItem.Transform.Y = vm.SelectedTemplateItem.Transform.Y;
                        vm.SelectedTemplateItem.PositionX = vm.SelectedTemplateItem.Transform.X;
                        vm.SelectedTemplateItem.PositionY = vm.SelectedTemplateItem.Transform.Y;
                        vm.StopDragging();
                    }
                    // Restore high-quality scaling after drag
                    try
                    {
                        if (_interactiveLowQualitySet && Root != null)
                        {
                            RenderOptions.SetBitmapScalingMode(Root, BitmapScalingMode.HighQuality);
                            _interactiveLowQualitySet = false;
                        }
                    }
                    catch { }
                }
            }
        }

        private void OnCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is Envelope_printing.TemplateDesignerViewModel vm)
            {
                // If double-click on empty canvas area (not on an item) -> open canvas properties
                if (e.ClickCount >= 2)
                {
                    var pos = e.GetPosition(ItemsCanvas);
                    var hit = VisualTreeHelper.HitTest(ItemsCanvas, pos);
                    bool hitItem = false;
                    if (hit != null)
                    {
                        // if any ancestor ContentPresenter (ItemsControl container) found, it's an item
                        DependencyObject cur = hit.VisualHit;
                        while (cur != null && !hitItem)
                        {
                            if (cur is ContentPresenter) { hitItem = true; break; }
                            cur = VisualTreeHelper.GetParent(cur);
                        }
                    }
                    if (!hitItem)
                    {
                        vm.ReopenCanvasProperties();
                        e.Handled = true;
                        return;
                    }
                }
                // single click on empty canvas clears selection and hides properties
                vm.SelectedTemplateItem = null;
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

        // Click on item in the canvas items ListBox should only select item without opening properties
        private void OnCanvasItemListClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is Envelope_printing.TemplateDesignerViewModel vm)
            {
                // ѕринудительно выдел€ем элемент, но не открываем свойства
                if (sender is ListBoxItem lbi && lbi.DataContext is Envelope_printing.TemplateItemViewModel item)
                {
                    vm.SelectedTemplateItem = item;
                }
                e.Handled = true; // не даЄм всплыть до обработчиков, которые могли бы открыть свойства
            }
        }

        private void OnResizeStart(object sender, DragStartedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Envelope_printing.TemplateItemViewModel vm)
            {
                _resizeStart = Mouse.GetPosition(ItemsCanvas);
                _startWidth = vm.Width; _startHeight = vm.Height;
                SetCacheForItemVisual(fe, true); // кеширование на врем€ ресайза
            }
        }
        private void OnResizeEnd(object sender, DragCompletedEventArgs e)
        {
            _startWidth = _startHeight = 0;
            if (sender is FrameworkElement fe)
            {
                SetCacheForItemVisual(fe, false);
            }
            if (DataContext is Envelope_printing.TemplateDesignerViewModel vm)
            {
                vm.RequestValidateAllItemsBounds();
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
            var bounds = itemRoot.TransformToAncestor(ItemsCanvas).TransformBounds(new Rect(0, 0, itemRoot.ActualWidth, itemRoot.ActualHeight));
            _rotateStartCenter = new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
            _startAngle = vm.RotationDegrees;
            // Compute initial mouse angle to avoid immediate snap/jump
            var m0 = Mouse.GetPosition(ItemsCanvas);
            var dx0 = m0.X - _rotateStartCenter.X;
            var dy0 = m0.Y - _rotateStartCenter.Y;
            var initialMouseAngle = Math.Atan2(dy0, dx0) * 180.0 / Math.PI;
            _rotationOffsetDeg = _startAngle - initialMouseAngle;
            SetCacheForItemVisual(fe, true); // кеширование на врем€ вращени€
        }
        private static T FindAncestor<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject current = child;
            while (current != null && current is not T)
                current = VisualTreeHelper.GetParent(current);
            return current as T;
        }
        private void OnRotateDrag(object sender, DragDeltaEventArgs e)
        {
            if (!(DataContext is Envelope_printing.TemplateDesignerViewModel root) || root.SelectedTemplateItem == null) return;
            var mouse = Mouse.GetPosition(ItemsCanvas);
            var dx = mouse.X - _rotateStartCenter.X;
            var dy = mouse.Y - _rotateStartCenter.Y;
            var mouseAngle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            var angle = mouseAngle + _rotationOffsetDeg;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                angle = Math.Round(angle / 15.0) * 15.0;
            }
            // Normalize to [-180;180) for consistency
            while (angle >= 180) angle -= 360;
            while (angle < -180) angle += 360;
            root.SelectedTemplateItem.RotationDegrees = angle;
        }
        private void OnRotateEnd(object sender, DragCompletedEventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                SetCacheForItemVisual(fe, false);
            }
            if (DataContext is Envelope_printing.TemplateDesignerViewModel vm)
            {
                vm.RequestValidateAllItemsBounds();
            }
        }

        private void OnCanvasPropsDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is Envelope_printing.TemplateDesignerViewModel vm)
            {
                vm.ReopenCanvasProperties();
                e.Handled = true;
            }
        }

        private void OnCanvasPropsClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2 && DataContext is Envelope_printing.TemplateDesignerViewModel vm)
            {
                vm.ReopenCanvasProperties();
                e.Handled = true;
            }
        }
    }
}
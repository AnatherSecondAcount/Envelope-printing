using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Collections.Generic;
using System.Windows.Threading;

namespace Envelope_printing
{
    public partial class PrintPreviewCanvas : UserControl
    {
        public static readonly DependencyProperty OverlayContentProperty =
            DependencyProperty.Register(nameof(OverlayContent), typeof(UIElement), typeof(PrintPreviewCanvas), new PropertyMetadata(null));
        public UIElement OverlayContent
        {
            get => (UIElement)GetValue(OverlayContentProperty);
            set => SetValue(OverlayContentProperty, value);
        }

        private PrintPreviewViewModel _vm;
        private readonly Dictionary<string, BitmapImage> _imageCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly DispatcherTimer _refreshTimer;
        private readonly Canvas _contentLayer = new Canvas { RenderTransformOrigin = new Point(0.5,0.5) };

        public PrintPreviewCanvas()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            DataContextChanged += OnDataContextChanged;
            SizeChanged += (_, __) => ScheduleRefresh();
            UseLayoutRounding = true; SnapsToDevicePixels = true;
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);

            _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(60)
            };
            _refreshTimer.Tick += (_, __) => { _refreshTimer.Stop(); RefreshNow(); };
        }

        // Public method to render immediately (used by printing)
        public void ForceRefresh()
        {
            RefreshNow();
        }

        private void EnsureLayers()
        {
            if (!PreviewCanvas.Children.Contains(_contentLayer))
                PreviewCanvas.Children.Add(_contentLayer);
            Panel.SetZIndex(ImageableRect,0);
            Panel.SetZIndex(PreviewCanvas,1);
            Panel.SetZIndex(_contentLayer,1);
            Panel.SetZIndex(EnvelopeOutlineCanvas,2);
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_vm != null)
            {
                _vm.PropertyChanged -= OnVmPropertyChanged;
                if (_vm.PreviewItems != null)
                    _vm.PreviewItems.CollectionChanged -= OnPreviewItemsChanged;
            }
            _vm = DataContext as PrintPreviewViewModel;
            if (_vm != null)
            {
                _vm.PropertyChanged += OnVmPropertyChanged;
                if (_vm.PreviewItems != null)
                    _vm.PreviewItems.CollectionChanged += OnPreviewItemsChanged;
            }
            ScheduleRefresh();
        }

        private void OnPreviewItemsChanged(object sender, NotifyCollectionChangedEventArgs e) => ScheduleRefresh();
        private void OnVmPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(PrintPreviewViewModel.SheetWidthMm):
                case nameof(PrintPreviewViewModel.SheetHeightMm):
                case nameof(PrintPreviewViewModel.MarginLeftMm):
                case nameof(PrintPreviewViewModel.MarginTopMm):
                case nameof(PrintPreviewViewModel.MarginRightMm):
                case nameof(PrintPreviewViewModel.MarginBottomMm):
                case nameof(PrintPreviewViewModel.FitToImageableArea):
                case nameof(PrintPreviewViewModel.TemplateScalePercent):
                case nameof(PrintPreviewViewModel.TemplateOffsetXMm):
                case nameof(PrintPreviewViewModel.TemplateOffsetYMm):
                case nameof(PrintPreviewViewModel.EnvelopeWidthMm):
                case nameof(PrintPreviewViewModel.EnvelopeHeightMm):
                case nameof(PrintPreviewViewModel.ZoomFactor):
                case nameof(PrintPreviewViewModel.IsPrinterLandscape):
                case nameof(PrintPreviewViewModel.PreviewItems):
                case nameof(PrintPreviewViewModel.SelectedTemplate):
                case nameof(PrintPreviewViewModel.CurrentPage):
                case nameof(PrintPreviewViewModel.IsRangeAll):
                case nameof(PrintPreviewViewModel.IsRangeCurrent):
                case nameof(PrintPreviewViewModel.IsRangeCustom):
                case nameof(PrintPreviewViewModel.RangeExpression):
                case nameof(PrintPreviewViewModel.IsPrinting):
                    // Rotation is no longer used for placement; ignore
                    ScheduleRefresh();
                    break;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            EnsureLayers();
            ScheduleRefresh();
        }
        private static double Px(double mm) => mm * Units.PxPerMm;
        private void ScheduleRefresh()
        {
            // Render even when not loaded (needed for printing visuals created off-screen)
            _refreshTimer.Stop();
            if (!IsLoaded)
            {
                RefreshNow();
            }
            else
            {
                _refreshTimer.Start();
            }
        }

        private void RefreshNow()
        {
            EnsureLayers();

            if (DataContext is not PrintPreviewViewModel vm)
            {
                _contentLayer.Children.Clear();
                PreviewCanvas.Children.Clear();
                EnvelopeOutlineCanvas.Children.Clear();
                ImageableRect.Visibility = Visibility.Collapsed;
                return;
            }

            double sheetW = Px(vm.SheetWidthMm);
            double sheetH = Px(vm.SheetHeightMm);
            PrinterSheet.Width = sheetW;
            PrinterSheet.Height = sheetH;
            // Hide page border in printing/preview-only modes
            PrinterSheet.BorderThickness = vm.IsPrinting ? new Thickness(0) : new Thickness(1);

            double l = Px(vm.MarginLeftMm);
            double r = Px(vm.MarginRightMm);
            double t = Px(vm.MarginTopMm);
            double b = Px(vm.MarginBottomMm);
            double imgW = Math.Max(0, sheetW - l - r);
            double imgH = Math.Max(0, sheetH - t - b);
            ImageableRect.Width = imgW;
            ImageableRect.Height = imgH;
            ImageableRect.Margin = new Thickness(l, t, r, b);
            ImageableRect.HorizontalAlignment = HorizontalAlignment.Left;
            ImageableRect.VerticalAlignment = VerticalAlignment.Top;

            var clipRect = new Rect(l, t, imgW, imgH);
            PreviewCanvas.Clip = new RectangleGeometry(clipRect);

            if (vm.SelectedTemplate == null)
            {
                _contentLayer.Children.Clear();
                EnvelopeOutlineCanvas.Children.Clear();
                ImageableRect.Visibility = vm.IsPrinting ? Visibility.Collapsed : Visibility.Visible;
                return;
            }

            double baseEnvW = Px(vm.EnvelopeWidthMm);
            double baseEnvH = Px(vm.EnvelopeHeightMm);
            double scale;
            if (vm.FitToImageableArea && baseEnvW >0 && baseEnvH >0)
            {
                double sx = imgW / baseEnvW;
                double sy = imgH / baseEnvH;
                scale = Math.Min(sx, sy);
            }
            else
            {
                scale = vm.TemplateScalePercent /100.0;
            }
            if (vm.FitToImageableArea) scale *=0.999;

            double envW = baseEnvW * scale;
            double envH = baseEnvH * scale;

            double centerX = l + imgW /2.0 + Px(vm.TemplateOffsetXMm);
            double centerY = t + imgH /2.0 + Px(vm.TemplateOffsetYMm);
            double envLeft = centerX - envW /2.0;
            double envTop = centerY - envH /2.0;
            Rect envRect = new Rect(envLeft, envTop, envW, envH);

            _contentLayer.Width = envW;
            _contentLayer.Height = envH;
            Canvas.SetLeft(_contentLayer, envLeft);
            Canvas.SetTop(_contentLayer, envTop);
            _contentLayer.RenderTransform = Transform.Identity;

            int visualIndex =0;
            foreach (var item in vm.PreviewItems.OrderBy(i => i.ZIndex))
            {
                double x = Px(item.PositionX) * scale;
                double y = Px(item.PositionY) * scale;
                double w = Px(item.Width) * scale;
                double h = Px(item.Height) * scale;
                if (item.IsImage)
                {
                    Image img;
                    if (visualIndex < _contentLayer.Children.Count && _contentLayer.Children[visualIndex] is Image existingImg) img = existingImg; else { img = new Image { RenderTransformOrigin = new Point(0.5,0.5) }; _contentLayer.Children.Insert(visualIndex, img); }
                    img.Width = w; img.Height = h; img.Opacity = item.Opacity; img.Stretch = item.Stretch; img.RenderTransform = Transform.Identity;
                    img.Source = GetCachedBitmap(item.ImagePath);
                    Canvas.SetLeft(img, x); Canvas.SetTop(img, y);
                    visualIndex++;
                }
                else
                {
                    Border border; TextBlock tb;
                    if (visualIndex < _contentLayer.Children.Count && _contentLayer.Children[visualIndex] is Border existingBorder && existingBorder.Child is Grid grid && grid.Children.Count >0 && grid.Children[0] is TextBlock existingTb) { border = existingBorder; tb = existingTb; }
                    else { border = new Border { RenderTransformOrigin = new Point(0.5,0.5) }; tb = new TextBlock(); var inner = new Grid(); inner.Children.Add(tb); border.Child = inner; _contentLayer.Children.Insert(visualIndex, border); }
                    border.Width = w; border.Height = h; border.Background = item.Background; border.BorderBrush = item.BorderBrush; border.BorderThickness = new Thickness(item.BorderThickness * Units.PxPerMm * scale);
                    border.CornerRadius = new CornerRadius(item.CornerRadius.TopLeft * Units.PxPerMm * scale); border.Opacity = item.Opacity; border.RenderTransform = Transform.Identity;
                    tb.Text = item.StaticText ?? string.Empty; tb.FontFamily = new FontFamily(item.FontFamily); tb.FontSize = item.FontSize * scale; tb.FontWeight = item.IsBold ? FontWeights.Bold : FontWeights.Normal; tb.FontStyle = item.FontStyle; tb.Foreground = item.Foreground;
                    tb.TextAlignment = item.TextAlignment; tb.Padding = new Thickness(item.Padding * Units.PxPerMm * scale); tb.TextWrapping = TextWrapping.Wrap; tb.HorizontalAlignment = item.HorizontalAlignment; tb.VerticalAlignment = item.VerticalAlignment;
                    Canvas.SetLeft(border, x); Canvas.SetTop(border, y); visualIndex++;
                }
            }
            while (_contentLayer.Children.Count > visualIndex) _contentLayer.Children.RemoveAt(_contentLayer.Children.Count -1);

            EnvelopeOutlineCanvas.Children.Clear();
            if (!vm.IsPrinting)
            {
                bool leftOverflow = envRect.Left < clipRect.Left -0.5;
                bool rightOverflow = envRect.Right > clipRect.Right +0.5;
                bool topOverflow = envRect.Top < clipRect.Top -0.5;
                bool bottomOverflow = envRect.Bottom > clipRect.Bottom +0.5;
                DrawTemplateBorder(envRect, leftOverflow, topOverflow, rightOverflow, bottomOverflow);
            }

            ImageableRect.Visibility = vm.IsPrinting ? Visibility.Collapsed : Visibility.Visible;
        }

        private void DrawTemplateBorder(Rect envRect, bool leftOv, bool topOv, bool rightOv, bool bottomOv)
        {
            var ok = Color.FromRgb(255,152,0); // orange normal
            var bad = Color.FromRgb(220,20,60); // red overflow
            double th =3;
            AddLine(new Point(envRect.Left, envRect.Top), new Point(envRect.Left, envRect.Bottom), leftOv ? bad : ok, th);
            AddLine(new Point(envRect.Left, envRect.Top), new Point(envRect.Right, envRect.Top), topOv ? bad : ok, th);
            AddLine(new Point(envRect.Right, envRect.Top), new Point(envRect.Right, envRect.Bottom), rightOv ? bad : ok, th);
            AddLine(new Point(envRect.Left, envRect.Bottom), new Point(envRect.Right, envRect.Bottom), bottomOv ? bad : ok, th);
        }

        private void AddLine(Point A, Point B, Color color, double thickness)
        {
            var line = new System.Windows.Shapes.Line
            {
                X1 = A.X, Y1 = A.Y,
                X2 = B.X, Y2 = B.Y,
                Stroke = new SolidColorBrush(color), StrokeThickness = thickness
            };
            EnvelopeOutlineCanvas.Children.Add(line);
        }

        private ImageSource GetCachedBitmap(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (_imageCache.TryGetValue(path, out var cached)) return cached;
            try
            {
                var bmp = new BitmapImage(); bmp.BeginInit(); bmp.UriSource = new Uri(path, UriKind.Absolute); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache; bmp.EndInit(); bmp.Freeze(); _imageCache[path] = bmp; return bmp;
            }
            catch { return null; }
        }
    }
}

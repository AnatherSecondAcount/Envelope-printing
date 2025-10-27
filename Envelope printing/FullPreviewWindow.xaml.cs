using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Printing;

namespace Envelope_printing
{
    public partial class FullPreviewWindow : Window
    {
        public FullPreviewWindow()
        {
            InitializeComponent();
        }

        private PrintPreviewViewModel VM => DataContext as PrintPreviewViewModel;

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private Point? _panStart;
        private Point _scrollStart;

        private void PreviewArea_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && VM != null)
            {
                int step = e.Delta > 0 ? 10 : -10;
                VM.ZoomPercentage = System.Math.Clamp(VM.ZoomPercentage + step, 20, 200);
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

        private async void Print_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (VM == null || VM.SelectedTemplate == null || VM.Recipients?.Any() != true)
                {
                    MessageBox.Show("Нет данных для печати.");
                    return;
                }
                var dlg = new System.Windows.Controls.PrintDialog();
                if (VM.SelectedPrinter != null) dlg.PrintQueue = VM.SelectedPrinter;
                if (dlg.ShowDialog() != true) return;
                var queue = dlg.PrintQueue;
                var pages = VM.GetPagesToPrint();
                if (pages == null || pages.Count == 0)
                {
                    MessageBox.Show("Не выбраны страницы для печати.");
                    return;
                }

                // Validate ticket to ensure orientation and media are respected
                var inputTicket = dlg.PrintTicket ?? new PrintTicket();
                var validated = queue.MergeAndValidatePrintTicket(queue.UserPrintTicket ?? queue.DefaultPrintTicket, inputTicket);
                var ticket = validated.ValidatedPrintTicket;

                // Determine media size in mm (base, portrait reference)
                double mediaWmm = VM.SelectedPageSize?.WidthMm ?? Units.DiuToMm(ticket.PageMediaSize?.Width ?? (96 * 8.27));
                double mediaHmm = VM.SelectedPageSize?.HeightMm ?? Units.DiuToMm(ticket.PageMediaSize?.Height ?? (96 * 11.69));
                bool isLandscape = ticket.PageOrientation == PageOrientation.Landscape;

                // Oriented page DIU
                double pageWidth = Units.MmToDiu(isLandscape ? mediaHmm : mediaWmm);
                double pageHeight = Units.MmToDiu(isLandscape ? mediaWmm : mediaHmm);

                // Margins from capabilities for that ticket (already oriented)
                double mLeftMm = 0, mTopMm = 0, mRightMm = 0, mBottomMm = 0;
                try
                {
                    var caps = queue.GetPrintCapabilities(ticket);
                    var area = caps?.PageImageableArea;
                    if (area != null)
                    {
                        double rDiu = System.Math.Max(0, pageWidth - (area.OriginWidth + area.ExtentWidth));
                        double bDiu = System.Math.Max(0, pageHeight - (area.OriginHeight + area.ExtentHeight));
                        mLeftMm = Units.DiuToMm(area.OriginWidth);
                        mTopMm = Units.DiuToMm(area.OriginHeight);
                        mRightMm = Units.DiuToMm(rDiu);
                        mBottomMm = Units.DiuToMm(bDiu);
                    }
                }
                catch { }

                VM.IsPrinting = true;
                VM.PrintedPages = 0;
                VM.TotalPagesToPrint = pages.Count;
                await Task.Delay(1);

                var doc = new FixedDocument();
                doc.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);
                int oldPage = VM.CurrentPage;
                foreach (var i in pages)
                {
                    var recipient = VM.Recipients.Skip((i - 1) * VM.PageSize).FirstOrDefault();
                    if (recipient == null) continue;
                    VM.CurrentPage = i;
                    VM.LoadPreviewItems();

                    var pvm = new PrintPreviewViewModel(skipInitialization: true)
                    {
                        SelectedTemplate = VM.SelectedTemplate,
                        TemplateOffsetXMm = VM.TemplateOffsetXMm,
                        TemplateOffsetYMm = VM.TemplateOffsetYMm,
                        FitToImageableArea = VM.FitToImageableArea,
                        TemplateScalePercent = VM.TemplateScalePercent,
                        SheetWidthMm = isLandscape ? mediaHmm : mediaWmm,
                        SheetHeightMm = isLandscape ? mediaWmm : mediaHmm,
                        IsPrinterLandscape = isLandscape,
                        MarginLeftMm = mLeftMm,
                        MarginTopMm = mTopMm,
                        MarginRightMm = mRightMm,
                        MarginBottomMm = mBottomMm,
                        Recipients = VM.Recipients,
                        PageSize = VM.PageSize,
                        CurrentPage = i,
                        IsPrinting = true
                    };
                    pvm.LoadPreviewItems();

                    var page = new FixedPage { Width = pageWidth, Height = pageHeight, Background = Brushes.White };
                    var canvas = new PrintPreviewCanvas { DataContext = pvm, Width = pageWidth, Height = pageHeight };
                    FixedPage.SetLeft(canvas, 0);
                    FixedPage.SetTop(canvas, 0);
                    page.Children.Add(canvas);
                    var content = new PageContent();
                    ((IAddChild)content).AddChild(page);
                    doc.Pages.Add(content);
                    VM.PrintedPages++;
                    await Task.Yield();
                }
                VM.CurrentPage = oldPage;
                dlg.PrintDocument(doc.DocumentPaginator, "Envelope batch");
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка печати: {ex.Message}");
            }
            finally
            {
                if (VM != null) VM.IsPrinting = false;
            }
        }

        private void SavePreset_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            var dialog = new InputBoxWindow("Save preset", "Enter preset name:");
            if (dialog.ShowDialog() == true)
            {
                VM.SavePresetCommand?.Execute(dialog.InputText);
            }
        }

        private void DeletePreset_Click(object sender, RoutedEventArgs e)
        {
            VM?.DeletePresetCommand?.Execute(null);
        }

        private void OpenPrinterSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Controls.PrintDialog();
            try
            {
                if (VM?.SelectedPrinter != null)
                {
                    dlg.PrintQueue = VM.SelectedPrinter;
                }
                if (dlg.ShowDialog() == true)
                {
                    VM?.GetType().GetMethod("LoadPrinters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(VM, null);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии параметров принтера: {ex.Message}");
            }
        }
    }
}

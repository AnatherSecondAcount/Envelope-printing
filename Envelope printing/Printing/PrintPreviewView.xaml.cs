using System.Printing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace Envelope_printing
{
    public partial class PrintPreviewView : UserControl
    {
        private CancellationTokenSource _printCts;
        private PrintProgressWindow _progressWindow;

        public PrintPreviewView()
        {
            InitializeComponent();
            DataContext = new PrintPreviewViewModel();
        }

        private PrintPreviewViewModel VM => DataContext as PrintPreviewViewModel;

        // Win32 interop for vendor printer preferences
        private static class NativePrint
        {
            public const int DM_OUT_BUFFER = 2;
            public const int DM_IN_BUFFER = 8;
            public const int DM_PROMPT = 4;

            [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern int DocumentProperties(IntPtr hwnd, IntPtr hPrinter,
                string pDeviceName, IntPtr pDevModeOutput, IntPtr pDevModeInput, int fMode);

            [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

            [DllImport("winspool.drv", SetLastError = true)]
            public static extern bool ClosePrinter(IntPtr hPrinter);
        }

        private static bool TryShowVendorPrinterPreferences(Window owner, PrintQueue queue, out PrintTicket updatedTicket)
        {
            updatedTicket = null;
            try
            {
                IntPtr hPrinter;
                if (!NativePrint.OpenPrinter(queue.FullName, out hPrinter, IntPtr.Zero)) return false;
                try
                {
                    var hwnd = new System.Windows.Interop.WindowInteropHelper(owner).Handle;
                    // First call to get size
                    int size = NativePrint.DocumentProperties(hwnd, hPrinter, queue.FullName, IntPtr.Zero, IntPtr.Zero, 0);
                    if (size <= 0) return false;
                    IntPtr devIn = Marshal.AllocHGlobal(size);
                    IntPtr devOut = Marshal.AllocHGlobal(size);
                    try
                    {
                        // Initialize with current settings from PrintTicket if possible
                        // We cannot convert directly without printing pipeline; so pass null for input
                        int ret = NativePrint.DocumentProperties(hwnd, hPrinter, queue.FullName, devOut, devIn,
                            NativePrint.DM_PROMPT | NativePrint.DM_OUT_BUFFER | NativePrint.DM_IN_BUFFER);
                        if (ret <= 0) return false;
                        // Convert DEVMODE -> PrintTicket via PrintQueue API
                        updatedTicket = queue.UserPrintTicket ?? queue.DefaultPrintTicket;
                        // Merge validated ticket to reflect device changes
                        updatedTicket = queue.MergeAndValidatePrintTicket(updatedTicket, updatedTicket).ValidatedPrintTicket;
                        return true;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(devIn);
                        Marshal.FreeHGlobal(devOut);
                    }
                }
                finally
                {
                    NativePrint.ClosePrinter(hPrinter);
                }
            }
            catch { }
            return false;
        }

        private static bool IsMicrosoftPrintToPdf(PrintQueue q)
        {
            try
            {
                var name = q?.FullName ?? q?.Name ?? string.Empty;
                return name.IndexOf("Microsoft Print to PDF", StringComparison.OrdinalIgnoreCase) >= 0
                    || (q?.QueueDriver?.Name?.IndexOf("Microsoft Print To PDF", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
            }
            catch { return false; }
        }

        private static void GetSheetSizeFromTicket(PrintTicket ticket, out double widthMm, out double heightMm, out bool isLandscape)
        {
            widthMm = heightMm = 0; isLandscape = false;
            var size = ticket?.PageMediaSize;
            if (size != null)
            {
                var w = Units.DiuToMm(size.Width ?? 0); var h = Units.DiuToMm(size.Height ?? 0);
                bool orientLandscape = ticket?.PageOrientation == PageOrientation.Landscape;
                if (!orientLandscape && ticket?.PageOrientation == PageOrientation.Portrait) orientLandscape = false;
                else if (ticket?.PageOrientation == null || ticket.PageOrientation == PageOrientation.Unknown) orientLandscape = w > h;
                if (orientLandscape) { widthMm = Math.Max(w, h); heightMm = Math.Min(w, h); }
                else { widthMm = Math.Min(w, h); heightMm = Math.Max(w, h); }
                isLandscape = orientLandscape;
            }
        }
        private static (double LeftMm, double TopMm, double RightMm, double BottomMm) GetMarginsFromTicket(PrintQueue queue, PrintTicket ticket, double sheetWidthMm, double sheetHeightMm, bool isLandscape)
        {
            try
            {
                var caps = queue?.GetPrintCapabilities(ticket); var area = caps?.PageImageableArea; if (area == null) return (0, 0, 0, 0);
                double pageW = Units.MmToDiu(sheetWidthMm); double pageH = Units.MmToDiu(sheetHeightMm);
                double oW = area.OriginWidth; double oH = area.OriginHeight; double eW = area.ExtentWidth; double eH = area.ExtentHeight;
                bool needSwap = isLandscape && (eW < eH && pageW > pageH);
                if (needSwap) { (oW, oH) = (oH, oW); (eW, eH) = (eH, eW); }
                double right = Math.Max(0, pageW - (oW + eW)); double bottom = Math.Max(0, pageH - (oH + eH));
                return (Units.DiuToMm(oW), Units.DiuToMm(oH), Units.DiuToMm(right), Units.DiuToMm(bottom));
            }
            catch { return (0, 0, 0, 0); }
        }
        private void ApplyTicketToViewModel(PrintQueue queue, PrintTicket ticket)
        {
            try
            {
                var size = ticket?.PageMediaSize; if (size == null) return;
                double wmm = Units.DiuToMm(size.Width ?? 0); double hmm = Units.DiuToMm(size.Height ?? 0);
                bool landscape = (ticket.PageOrientation == PageOrientation.Landscape) || wmm > hmm;
                VM.IsPrinterLandscape = landscape;
                VM.SheetWidthMm = landscape ? Math.Max(wmm, hmm) : Math.Min(wmm, hmm);
                VM.SheetHeightMm = landscape ? Math.Min(wmm, hmm) : Math.Max(wmm, hmm);
                var caps = queue?.GetPrintCapabilities(ticket); var area = caps?.PageImageableArea;
                if (area != null)
                {
                    double pageW = Units.MmToDiu(VM.SheetWidthMm); double pageH = Units.MmToDiu(VM.SheetHeightMm);
                    double oW = area.OriginWidth; double oH = area.OriginHeight; double eW = area.ExtentWidth; double eH = area.ExtentHeight;
                    bool needSwap = landscape && (eW < eH && pageW > pageH);
                    if (needSwap) { (oW, oH) = (oH, oW); (eW, eH) = (eH, eW); }
                    double r = Math.Max(0, pageW - (oW + eW)); double b = Math.Max(0, pageH - (oH + eH));
                    VM.MarginLeftMm = Units.DiuToMm(oW); VM.MarginTopMm = Units.DiuToMm(oH); VM.MarginRightMm = Units.DiuToMm(r); VM.MarginBottomMm = Units.DiuToMm(b);
                }

                // Reflect InputBin and MediaType picked in the system dialog back to VM selections
                if (ticket?.InputBin != null)
                    VM.SelectedInputBin = ticket.InputBin;
                if (ticket?.PageMediaType != null)
                    VM.SelectedMediaType = ticket.PageMediaType;
            }
            catch { }
        }

        // open printer settings from UI
        private void OpenPrinterSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (VM?.SelectedPrinter == null) { MessageBox.Show("Принтер не выбран."); return; }
                var owner = Window.GetWindow(this) ?? Application.Current?.MainWindow;
                if (owner != null && TryShowVendorPrinterPreferences(owner, VM.SelectedPrinter, out var newTicket) && newTicket != null)
                {
                    // Validate and apply
                    var validated = VM.SelectedPrinter.MergeAndValidatePrintTicket(VM.SelectedPrinter.UserPrintTicket ?? VM.SelectedPrinter.DefaultPrintTicket, newTicket).ValidatedPrintTicket;
                    VM.SelectedPrinter.UserPrintTicket = validated;
                    ApplyTicketToViewModel(VM.SelectedPrinter, validated);
                    VM.GetType().GetMethod("LoadPageSizes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(VM, null);
                    return;
                }
                var dlg = new PrintDialog { PrintQueue = VM.SelectedPrinter, PrintTicket = VM.SelectedPrinter.UserPrintTicket ?? VM.SelectedPrinter.DefaultPrintTicket };
                if (dlg.ShowDialog() == true)
                {
                    var validated = dlg.PrintQueue.MergeAndValidatePrintTicket(dlg.PrintQueue.UserPrintTicket ?? dlg.PrintQueue.DefaultPrintTicket, dlg.PrintTicket).ValidatedPrintTicket;
                    dlg.PrintQueue.UserPrintTicket = validated;
                    ApplyTicketToViewModel(dlg.PrintQueue, validated);
                    VM.GetType().GetMethod("LoadPageSizes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(VM, null);
                }
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка при открытии параметров печати: {ex.Message}"); }
        }

        private async void Print_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (VM == null || VM.SelectedTemplate == null || VM.Recipients?.Any() != true) { MessageBox.Show("Нет данных для печати."); return; }
                var queue = VM.SelectedPrinter ?? new LocalPrintServer().DefaultPrintQueue; if (queue == null) { MessageBox.Show("Принтер не найден."); return; }
                var pages = VM.GetPagesToPrint(); if (pages == null || pages.Count == 0) { MessageBox.Show("Не выбраны страницы для печати."); return; }

                _printCts?.Cancel(); _printCts = new CancellationTokenSource(); var token = _printCts.Token;
                VM.IsPrinting = true; VM.PrintedPages = 0; VM.TotalPagesToPrint = pages.Count; VM.PrintStatus = "Подготовка";
                _progressWindow?.Close();
                _progressWindow = new PrintProgressWindow { DataContext = VM, Owner = Application.Current?.MainWindow };
                _progressWindow.CancelRequested += (_, __) => _printCts?.Cancel();
                _progressWindow.Show();
                await Task.Yield();

                // Build ticket on UI thread
                double mediaWmm = VM.SelectedPageSize?.WidthMm ?? VM.SheetWidthMm; double mediaHmm = VM.SelectedPageSize?.HeightMm ?? VM.SheetHeightMm;
                bool landscape = VM.IsPrinterLandscape; double targetWmm = Math.Min(mediaWmm, mediaHmm); double targetHmm = Math.Max(mediaWmm, mediaHmm);
                var baseTicket = new PrintTicket();
                if (VM.SelectedPageSize?.Media?.PageMediaSizeName != null) baseTicket.PageMediaSize = new PageMediaSize(VM.SelectedPageSize.Media.PageMediaSizeName.Value);
                else baseTicket.PageMediaSize = new PageMediaSize(Units.MmToDiu(targetWmm), Units.MmToDiu(targetHmm));
                baseTicket.PageOrientation = landscape ? PageOrientation.Landscape : PageOrientation.Portrait;
                if (VM.SelectedInputBin != null) baseTicket.InputBin = VM.SelectedInputBin.Value;
                if (VM.SelectedMediaType != null) baseTicket.PageMediaType = VM.SelectedMediaType.Value;

                var validated = queue.MergeAndValidatePrintTicket(queue.UserPrintTicket ?? queue.DefaultPrintTicket, baseTicket).ValidatedPrintTicket; queue.UserPrintTicket = validated;
                GetSheetSizeFromTicket(validated, out var sheetWmm, out var sheetHmm, out var ticketLandscape);
                var margins = GetMarginsFromTicket(queue, validated, sheetWmm, sheetHmm, ticketLandscape);
                double pageWidth = Units.MmToDiu(sheetWmm); double pageHeight = Units.MmToDiu(sheetHmm);

                // Build FixedDocument on UI thread
                var fixedDoc = new FixedDocument { DocumentPaginator = { PageSize = new Size(pageWidth, pageHeight) } };
                int processed = 0; int total = pages.Count;
                foreach (var pageIndex in pages)
                {
                    if (token.IsCancellationRequested) { VM.PrintStatus = "Печать отменена"; break; }
                    VM.PrintStatus = $"Подготовка страницы {processed + 1} из {total}";
                    var pvm = new PrintPreviewViewModel(skipInitialization: true)
                    {
                        SelectedTemplate = VM.SelectedTemplate,
                        TemplateOffsetXMm = VM.TemplateOffsetXMm,
                        TemplateOffsetYMm = VM.TemplateOffsetYMm,
                        FitToImageableArea = VM.FitToImageableArea,
                        TemplateScalePercent = VM.TemplateScalePercent,
                        IsPrinterLandscape = ticketLandscape,
                        Recipients = VM.Recipients,
                        PageSize = VM.PageSize,
                        CurrentPage = pageIndex,
                        IsPrinting = true
                    };
                    pvm.SheetWidthMm = sheetWmm; pvm.SheetHeightMm = sheetHmm; pvm.MarginLeftMm = margins.LeftMm; pvm.MarginTopMm = margins.TopMm; pvm.MarginRightMm = margins.RightMm; pvm.MarginBottomMm = margins.BottomMm; pvm.LoadPreviewItems();
                    var root = new Grid { Width = pageWidth, Height = pageHeight, Background = Brushes.White };
                    var canvas = new PrintPreviewCanvas { DataContext = pvm, Width = pageWidth, Height = pageHeight };
                    canvas.Measure(new Size(pageWidth, pageHeight)); canvas.Arrange(new Rect(0, 0, pageWidth, pageHeight)); canvas.UpdateLayout(); canvas.ForceRefresh();
                    root.Children.Add(canvas);
                    var page = new FixedPage { Width = pageWidth, Height = pageHeight, Background = Brushes.White };
                    FixedPage.SetLeft(root, 0); FixedPage.SetTop(root, 0); page.Children.Add(root);
                    page.Measure(new Size(pageWidth, pageHeight)); page.Arrange(new Rect(0, 0, pageWidth, pageHeight)); page.UpdateLayout();
                    var content = new PageContent(); ((IAddChild)content).AddChild(page); fixedDoc.Pages.Add(content);
                    VM.PrintedPages = ++processed; await Task.Yield();
                }

                if (!token.IsCancellationRequested && processed == total)
                {
                    VM.PrintStatus = "Отправка в очередь печати";
                    var dlg = new PrintDialog { PrintQueue = queue, PrintTicket = validated };
                    dlg.PrintDocument(fixedDoc.DocumentPaginator, "Envelope printing");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка печати: {ex.Message}");
            }
            finally
            {
                VM.IsPrinting = false; VM.PrintStatus = null; _printCts?.Dispose(); _printCts = null;
                if (_progressWindow != null) { try { _progressWindow.Close(); } catch { } _progressWindow = null; }
            }
        }

        private void CancelPrint_Click(object sender, RoutedEventArgs e)
        {
            _printCts?.Cancel();
        }

        private void SavePreset_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as PrintPreviewViewModel;
            if (vm == null) return;
            var dialog = new InputBoxWindow("Сохранение пресета", "Введите имя пресета:");
            if (dialog.ShowDialog() == true)
            {
                vm.SavePresetCommand?.Execute(dialog.InputText);
            }
        }

        private void DeletePreset_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as PrintPreviewViewModel;
            if (vm == null) return;
            vm.DeletePresetCommand?.Execute(null);
        }
    }
}

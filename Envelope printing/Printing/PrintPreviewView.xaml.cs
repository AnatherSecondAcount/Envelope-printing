using System;
using System.Diagnostics;
using System.Linq;
using System.Printing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Xps; // for XpsDocumentWriter
using System; // StringComparison
using EnvelopePrinter.Core; // Recipient, Template, etc.

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

        private static bool IsMicrosoftPrintToPdf(PrintQueue q)
        {
            try
            {
                var name = q?.FullName ?? q?.Name ?? string.Empty;
                return name.IndexOf("Microsoft Print to PDF", StringComparison.OrdinalIgnoreCase) >=0
                    || (q?.QueueDriver?.Name?.IndexOf("Microsoft Print To PDF", StringComparison.OrdinalIgnoreCase) ?? -1) >=0;
            }
            catch { return false; }
        }

        private static void GetSheetSizeFromTicket(PrintTicket ticket, out double widthMm, out double heightMm, out bool isLandscape)
        {
            widthMm = heightMm =0; isLandscape = false;
            var size = ticket?.PageMediaSize;
            if (size != null)
            {
                var w = Units.DiuToMm(size.Width ??0); var h = Units.DiuToMm(size.Height ??0);
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
                var caps = queue?.GetPrintCapabilities(ticket); var area = caps?.PageImageableArea; if (area == null) return (0,0,0,0);
                double pageW = Units.MmToDiu(sheetWidthMm); double pageH = Units.MmToDiu(sheetHeightMm);
                double oW = area.OriginWidth; double oH = area.OriginHeight; double eW = area.ExtentWidth; double eH = area.ExtentHeight;
                bool needSwap = isLandscape && (eW < eH && pageW > pageH);
                if (needSwap) { (oW, oH) = (oH, oW); (eW, eH) = (eH, eW); }
                double right = Math.Max(0, pageW - (oW + eW)); double bottom = Math.Max(0, pageH - (oH + eH));
                return (Units.DiuToMm(oW), Units.DiuToMm(oH), Units.DiuToMm(right), Units.DiuToMm(bottom));
            }
            catch { return (0,0,0,0); }
        }
        private void ApplyTicketToViewModel(PrintQueue queue, PrintTicket ticket)
        {
            try
            {
                var size = ticket?.PageMediaSize; if (size == null) return;
                double wmm = Units.DiuToMm(size.Width ??0); double hmm = Units.DiuToMm(size.Height ??0);
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
            }
            catch { }
        }

        // open printer settings from UI
        private void OpenPrinterSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (VM?.SelectedPrinter == null) { MessageBox.Show("Принтер не выбран."); return; }
                var dlg = new PrintDialog { PrintQueue = VM.SelectedPrinter, PrintTicket = VM.SelectedPrinter.UserPrintTicket ?? VM.SelectedPrinter.DefaultPrintTicket };
                if (dlg.ShowDialog() == true)
                {
                    VM.SelectedPrinter.UserPrintTicket = dlg.PrintTicket;
                    ApplyTicketToViewModel(dlg.PrintQueue, dlg.PrintTicket);
                }
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка при открытии параметров печати: {ex.Message}"); }
        }

        private async void Print_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (VM == null || VM.SelectedTemplate == null || VM.Recipients?.Any() != true) { MessageBox.Show("Нет данных для печати."); return; }
                var queue = VM.SelectedPrinter ?? new LocalPrintServer().DefaultPrintQueue; if (queue == null) { MessageBox.Show("Принтер не выбран."); return; }
                var pages = VM.GetPagesToPrint(); if (pages == null || pages.Count ==0) { MessageBox.Show("Не выбраны страницы для печати."); return; }

                _printCts?.Cancel(); _printCts = new CancellationTokenSource(); var token = _printCts.Token;
                VM.IsPrinting = true; VM.PrintedPages =0; VM.TotalPagesToPrint = pages.Count; VM.PrintStatus = "Подготовка…";
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
                var validated = queue.MergeAndValidatePrintTicket(queue.UserPrintTicket ?? queue.DefaultPrintTicket, baseTicket).ValidatedPrintTicket; queue.UserPrintTicket = validated;
                GetSheetSizeFromTicket(validated, out var sheetWmm, out var sheetHmm, out var ticketLandscape);
                var margins = GetMarginsFromTicket(queue, validated, sheetWmm, sheetHmm, ticketLandscape);
                double pageWidth = Units.MmToDiu(sheetWmm); double pageHeight = Units.MmToDiu(sheetHmm);

                // Build FixedDocument on UI thread
                var fixedDoc = new FixedDocument { DocumentPaginator = { PageSize = new Size(pageWidth, pageHeight) } };
                int processed =0; int total = pages.Count;
                foreach (var pageIndex in pages)
                {
                    if (token.IsCancellationRequested) { VM.PrintStatus = "Отменено пользователем"; break; }
                    VM.PrintStatus = $"Формирование страницы {processed +1} из {total}…";
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
                    canvas.Measure(new Size(pageWidth, pageHeight)); canvas.Arrange(new Rect(0,0, pageWidth, pageHeight)); canvas.UpdateLayout(); canvas.ForceRefresh();
                    root.Children.Add(canvas);
                    var page = new FixedPage { Width = pageWidth, Height = pageHeight, Background = Brushes.White };
                    FixedPage.SetLeft(root,0); FixedPage.SetTop(root,0); page.Children.Add(root);
                    page.Measure(new Size(pageWidth, pageHeight)); page.Arrange(new Rect(0,0, pageWidth, pageHeight)); page.UpdateLayout();
                    var content = new PageContent(); ((IAddChild)content).AddChild(page); fixedDoc.Pages.Add(content);
                    VM.PrintedPages = ++processed; await Task.Yield();
                }

                if (!token.IsCancellationRequested && processed == total)
                {
                    VM.PrintStatus = "Отправка на принтер…";
                    if (IsMicrosoftPrintToPdf(queue))
                    {
                        // блокирующий диалог драйвера — ожидаемо
                        var dlg = new PrintDialog { PrintQueue = queue, PrintTicket = validated };
                        dlg.PrintDocument(fixedDoc.DocumentPaginator, "Envelope printing");
                    }
                    else
                    {
                        var writer = PrintQueue.CreateXpsDocumentWriter(queue);
                        var tcs = new TaskCompletionSource();
                        writer.WritingCompleted += (s2, a2) => tcs.TrySetResult();
                        writer.WritingProgressChanged += (s2, a2) => { if (a2 != null && a2.Number >0 && total >0) VM.PrintStatus = $"Отправка на принтер… ({Math.Min(100, (int)(a2.Number *100.0 / Math.Max(1, total))) }%)"; };
                        writer.WriteAsync(fixedDoc.DocumentPaginator, validated);
                        await tcs.Task;
                    }
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

using System;
using System.Diagnostics;
using System.Linq;
using System.Printing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace Envelope_printing
{
    public partial class PrintPreviewView : UserControl
    {
        public PrintPreviewView()
        {
            InitializeComponent();
            DataContext = new PrintPreviewViewModel();
        }

        private PrintPreviewViewModel VM => DataContext as PrintPreviewViewModel;

        private void OpenPrinterSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (VM?.SelectedPrinter == null)
                {
                    MessageBox.Show("Принтер не выбран.");
                    return;
                }

                // Open native printer preferences dialog for the selected printer
                var printerName = VM.SelectedPrinter.FullName;
                var psi = new ProcessStartInfo
                {
                    FileName = "rundll32",
                    Arguments = $"printui.dll,PrintUIEntry /p /n \"{printerName}\"",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии настроек принтера: {ex.Message}");
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
                var queue = VM.SelectedPrinter ?? new LocalPrintServer().DefaultPrintQueue;
                if (queue == null)
                {
                    MessageBox.Show("Принтер не найден.");
                    return;
                }
                var pages = VM.GetPagesToPrint();
                if (pages == null || pages.Count ==0)
                {
                    MessageBox.Show("Не выбраны страницы для печати.");
                    return;
                }

                // Base media size in mm from selection (portrait reference)
                double mediaWmm = VM.SelectedPageSize?.WidthMm ?? VM.SheetWidthMm;
                double mediaHmm = VM.SelectedPageSize?.HeightMm ?? VM.SheetHeightMm;
                // Desired oriented page size in mm
                double targetWmm = VM.IsPrinterLandscape ? mediaHmm : mediaWmm;
                double targetHmm = VM.IsPrinterLandscape ? mediaWmm : mediaHmm;

                // Build a ticket with explicit PageMediaSize matching the desired orientation (no explicit PageOrientation)
                var baseTicket = new PrintTicket
                {
                    PageMediaSize = new PageMediaSize(Units.MmToDiu(targetWmm), Units.MmToDiu(targetHmm))
                };
                var validated = queue.MergeAndValidatePrintTicket(queue.UserPrintTicket ?? queue.DefaultPrintTicket, baseTicket);
                var ticketToUse = validated.ValidatedPrintTicket;
                queue.UserPrintTicket = ticketToUse; // persist

                // Page size for FixedDocument must match the oriented page
                double pageWidth = Units.MmToDiu(targetWmm);
                double pageHeight = Units.MmToDiu(targetHmm);

                // Read imageable area as portrait, rotate if landscape
                double mLeftMm =0, mTopMm =0, mRightMm =0, mBottomMm =0;
                try
                {
                    var caps = queue.GetPrintCapabilities(ticketToUse);
                    var area = caps?.PageImageableArea;
                    if (area != null)
                    {
                        double pw = Math.Min(pageWidth, pageHeight);
                        double ph = Math.Max(pageWidth, pageHeight);
                        double lP = area.OriginWidth;
                        double tP = area.OriginHeight;
                        double rP = Math.Max(0, pw - (area.OriginWidth + area.ExtentWidth));
                        double bP = Math.Max(0, ph - (area.OriginHeight + area.ExtentHeight));
                        double l, t, r, b;
                        if (VM.IsPrinterLandscape)
                        { l = tP; t = rP; r = bP; b = lP; }
                        else
                        { l = lP; t = tP; r = rP; b = bP; }
                        mLeftMm = Units.DiuToMm(l);
                        mTopMm = Units.DiuToMm(t);
                        mRightMm = Units.DiuToMm(r);
                        mBottomMm = Units.DiuToMm(b);
                    }
                }
                catch { }

                VM.IsPrinting = true;
                VM.PrintedPages =0; VM.TotalPagesToPrint = pages.Count;
                await Task.Delay(1);

                var fixedDoc = new FixedDocument { DocumentPaginator = { PageSize = new Size(pageWidth, pageHeight) } };
                int oldPage = VM.CurrentPage;

                foreach (var pageIndex in pages)
                {
                    var pvm = new PrintPreviewViewModel(skipInitialization: true)
                    {
                        SelectedTemplate = VM.SelectedTemplate,
                        TemplateOffsetXMm = VM.TemplateOffsetXMm,
                        TemplateOffsetYMm = VM.TemplateOffsetYMm,
                        FitToImageableArea = VM.FitToImageableArea,
                        TemplateScalePercent = VM.TemplateScalePercent,
                        IsPrinterLandscape = VM.IsPrinterLandscape,
                        Recipients = VM.Recipients,
                        PageSize = VM.PageSize,
                        CurrentPage = pageIndex,
                        IsPrinting = true
                    };
                    pvm.SheetWidthMm = targetWmm; pvm.SheetHeightMm = targetHmm;
                    pvm.MarginLeftMm = mLeftMm; pvm.MarginTopMm = mTopMm; pvm.MarginRightMm = mRightMm; pvm.MarginBottomMm = mBottomMm;
                    pvm.LoadPreviewItems();

                    var root = new Grid { Width = pageWidth, Height = pageHeight, Background = Brushes.White };
                    var canvas = new PrintPreviewCanvas { DataContext = pvm, Width = pageWidth, Height = pageHeight };
                    canvas.Measure(new Size(pageWidth, pageHeight));
                    canvas.Arrange(new Rect(0,0, pageWidth, pageHeight));
                    canvas.UpdateLayout();
                    canvas.ForceRefresh();
                    root.Children.Add(canvas);

                    var page = new FixedPage { Width = pageWidth, Height = pageHeight, Background = Brushes.White };
                    FixedPage.SetLeft(root,0); FixedPage.SetTop(root,0);
                    page.Children.Add(root);
                    page.Measure(new Size(pageWidth, pageHeight));
                    page.Arrange(new Rect(0,0, pageWidth, pageHeight));
                    page.UpdateLayout();

                    var content = new PageContent();
                    ((IAddChild)content).AddChild(page);
                    fixedDoc.Pages.Add(content);
                    VM.PrintedPages++;
                    await Task.Yield();
                }

                VM.CurrentPage = oldPage;
                var writer = PrintQueue.CreateXpsDocumentWriter(queue);
                writer.Write(fixedDoc.DocumentPaginator, ticketToUse);
            }
            catch (Exception ex)
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

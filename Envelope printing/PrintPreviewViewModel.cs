using EnvelopePrinter.Core;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Printing;
using System.Windows.Input;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows; // for Application

namespace Envelope_printing
{
 public partial class PrintPreviewViewModel : INotifyPropertyChanged
 {
 private readonly DataService _dataService = new();
 private readonly FilterPresetsService _presetsService = new();

 public ObservableCollection<Template> Templates { get; private set; }
 private Template _selectedTemplate;
 public Template SelectedTemplate { get => _selectedTemplate; set { _selectedTemplate = value; OnPropertyChanged(); UpdateEnvelopeSizeFromTemplate(); LoadPreviewItems(); } }

 public ObservableCollection<Recipient> Recipients { get; set; }
 public ObservableCollection<string> AvailableCities { get; private set; }
 private string _selectedCity;
 public string SelectedCity { get => _selectedCity; set { _selectedCity = value; OnPropertyChanged(); FilterRecipients(); } }

 private string _searchText;
 public string SearchText { get => _searchText; set { _searchText = value; OnPropertyChanged(); FilterRecipients(); } }

 private int _currentPage =1;
 public int CurrentPage { get => _currentPage; set { _currentPage = value; OnPropertyChanged(); LoadPreviewItems(); OnPropertyChanged(nameof(CurrentPageDisplay)); } }
 public int PageSize { get; set; } =1;
 private int _totalCount;
 public int TotalCount { get => _totalCount; set { _totalCount = value; OnPropertyChanged(); } }
 public string CurrentPageDisplay => $"{CurrentPage} / {TotalPages}";
 public int TotalPages => Recipients == null || Recipients.Count ==0 ?1 : (Recipients.Count + PageSize -1) / PageSize;

 public ObservableCollection<TemplateItemViewModel> PreviewItems { get; private set; } = new();

 // Printers
 public ObservableCollection<PrintQueue> Printers { get; } = new();
 private PrintQueue _selectedPrinter;
 public PrintQueue SelectedPrinter { get => _selectedPrinter; set { _selectedPrinter = value; OnPropertyChanged(); LoadPageSizes(); } }
 public ObservableCollection<PageSizeOption> AvailablePageSizes { get; } = new();
 private PageSizeOption _selectedPageSize;
 public PageSizeOption SelectedPageSize { get => _selectedPageSize; set { _selectedPageSize = value; OnPropertyChanged(); UpdateSheetSizeFromPageSize(); } }

 private double _sheetWidthMm =210;
 public double SheetWidthMm { get => _sheetWidthMm; set { _sheetWidthMm = value; OnPropertyChanged(); } }
 private double _sheetHeightMm =297;
 public double SheetHeightMm { get => _sheetHeightMm; set { _sheetHeightMm = value; OnPropertyChanged(); } }

 private bool _isPrinterLandscape;
 public bool IsPrinterLandscape { get => _isPrinterLandscape; set { _isPrinterLandscape = value; OnPropertyChanged(); UpdateSheetSizeFromPageSize(); } }

 // Rotation of the whole envelope content (0/90/180/270)
 private int _envelopeRotationDegrees =0;
 public int EnvelopeRotationDegrees
 {
 get => _envelopeRotationDegrees;
 set
 {
 int v = ((value %360) +360) %360;
 if (v %90 !=0) v =0;
 if (_envelopeRotationDegrees == v) return;
 _envelopeRotationDegrees = v;
 OnPropertyChanged();
 }
 }

 // Envelope size (from template, exposed for preview refresh)
 public double EnvelopeWidthMm => SelectedTemplate?.EnvelopeWidth ??0;
 public double EnvelopeHeightMm => SelectedTemplate?.EnvelopeHeight ??0;
 private void UpdateEnvelopeSizeFromTemplate()
 {
 OnPropertyChanged(nameof(EnvelopeWidthMm));
 OnPropertyChanged(nameof(EnvelopeHeightMm));
 }

 // Template placement and scaling on page
 private double _templateOffsetXMm =0;
 public double TemplateOffsetXMm { get => _templateOffsetXMm; set { _templateOffsetXMm = value; OnPropertyChanged(); } }
 private double _templateOffsetYMm =0;
 public double TemplateOffsetYMm { get => _templateOffsetYMm; set { _templateOffsetYMm = value; OnPropertyChanged(); } }
 private bool _fitToImageableArea = true;
 public bool FitToImageableArea { get => _fitToImageableArea; set { _fitToImageableArea = value; OnPropertyChanged(); } }
 private int _templateScalePercent =100;
 public int TemplateScalePercent { get => _templateScalePercent; set { _templateScalePercent = value <10 ?10 : (value >300 ?300 : value); OnPropertyChanged(); } }

 private int _zoomPercentage =100;
 public int ZoomPercentage { get => _zoomPercentage; set { if (_zoomPercentage == value) return; _zoomPercentage = value <20 ?20 : (value >200 ?200 : value); OnPropertyChanged(); OnPropertyChanged(nameof(ZoomFactor)); } }
 public double ZoomFactor => ZoomPercentage /100.0;
 public ICommand ZoomInCommand { get; }
 public ICommand ZoomOutCommand { get; }
 public ICommand ResetZoomCommand { get; }
 public ICommand OpenFullPreviewCommand { get; }

 // Copies
 private int _copies =1;
 public int Copies { get => _copies; set { _copies = value <1 ?1 : value; OnPropertyChanged(); } }
 public ICommand IncreaseCopiesCommand { get; }
 public ICommand DecreaseCopiesCommand { get; }

 // Margins from printer (read-only for bindings)
 private double _marginLeftMm, _marginTopMm, _marginRightMm, _marginBottomMm;
 public double MarginLeftMm { get => _marginLeftMm; set { _marginLeftMm = value; OnPropertyChanged(); } }
 public double MarginTopMm { get => _marginTopMm; set { _marginTopMm = value; OnPropertyChanged(); } }
 public double MarginRightMm { get => _marginRightMm; set { _marginRightMm = value; OnPropertyChanged(); } }
 public double MarginBottomMm { get => _marginBottomMm; set { _marginBottomMm = value; OnPropertyChanged(); } }

 // Print range (new model)
 private bool _isRangeAll = true;
 public bool IsRangeAll
 {
 get => _isRangeAll;
 set
 {
 if (_isRangeAll == value) return; _isRangeAll = value; OnPropertyChanged();
 if (value) { IsRangeCurrent = false; IsRangeCustom = false; }
 OnPropertyChanged(nameof(IsRangeCustomEnabled));
 OnPropertyChanged(nameof(SelectedPagesPreview));
 }
 }
 private bool _isRangeCurrent;
 public bool IsRangeCurrent
 {
 get => _isRangeCurrent;
 set
 {
 if (_isRangeCurrent == value) return; _isRangeCurrent = value; OnPropertyChanged();
 if (value) { IsRangeAll = false; IsRangeCustom = false; }
 OnPropertyChanged(nameof(IsRangeCustomEnabled));
 OnPropertyChanged(nameof(SelectedPagesPreview));
 }
 }
 private bool _isRangeCustom;
 public bool IsRangeCustom
 {
 get => _isRangeCustom;
 set
 {
 if (_isRangeCustom == value) return; _isRangeCustom = value; OnPropertyChanged();
 if (value) { IsRangeAll = false; IsRangeCurrent = false; }
 OnPropertyChanged(nameof(IsRangeCustomEnabled));
 OnPropertyChanged(nameof(SelectedPagesPreview));
 }
 }
 public bool IsRangeCustomEnabled => IsRangeCustom;
 private string _rangeExpression;
 public string RangeExpression
 {
 get => _rangeExpression;
 set { _rangeExpression = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedPagesPreview)); RebuildPageSequence(); }
 }
 public string SelectedPagesPreview
 {
 get
 {
 var pages = GetPagesToPrint();
 if (pages == null || pages.Count ==0) return "Нет страниц для печати";
 if (pages.Count <=10) return "Страницы: " + string.Join(", ", pages);
 return $"Страницы: {pages.First()}-{pages.Last()} (всего {pages.Count})";
 }
 }

 // Legacy explicit range fields (kept for compatibility if used elsewhere)
 public int RangeFrom { get => _rangeFrom; set { _rangeFrom = value; OnPropertyChanged(); } }
 private int _rangeFrom =1;
 public int RangeTo { get => _rangeTo; set { _rangeTo = value; OnPropertyChanged(); } }
 private int _rangeTo =1;

 // Sorting (will be hidden in UI but keep functional)
 private string _sortBy;
 public string SortBy { get => _sortBy; set { _sortBy = value; OnPropertyChanged(); FilterRecipients(); } }
 private bool _sortDescending;
 public bool SortDescending { get => _sortDescending; set { _sortDescending = value; OnPropertyChanged(); FilterRecipients(); } }

 // Presets
 public ObservableCollection<FilterPreset> Presets { get; } = new();
 private FilterPreset _selectedPreset;
 public FilterPreset SelectedPreset { get => _selectedPreset; set { _selectedPreset = value; OnPropertyChanged(); ApplyPreset(value); } }
 public ICommand SavePresetCommand { get; }
 public ICommand DeletePresetCommand { get; }

 // Navigation
 public ICommand PrevPageCommand { get; }
 public ICommand NextPageCommand { get; }

 // Print progress
 private bool _isPrinting;
 public bool IsPrinting { get => _isPrinting; set { _isPrinting = value; OnPropertyChanged(); } }
 private int _printedPages;
 public int PrintedPages { get => _printedPages; set { _printedPages = value; OnPropertyChanged(); OnPropertyChanged(nameof(PrintProgressPercent)); } }
 private int _totalPagesToPrint;
 public int TotalPagesToPrint { get => _totalPagesToPrint; set { _totalPagesToPrint = value; OnPropertyChanged(); OnPropertyChanged(nameof(PrintProgressPercent)); } }
 public int PrintProgressPercent => TotalPagesToPrint ==0 ?0 : (int)(PrintedPages *100.0 / TotalPagesToPrint);

 // Extended filter
 private string _filterOrgContains;
 public string FilterOrgContains { get => _filterOrgContains; set { _filterOrgContains = value; OnPropertyChanged(); ApplyFilters(); } }
 private string _filterCityEquals;
 public string FilterCityEquals { get => _filterCityEquals; set { _filterCityEquals = value; OnPropertyChanged(); ApplyFilters(); } }
 private string _filterPostalStarts;
 public string FilterPostalStarts { get => _filterPostalStarts; set { _filterPostalStarts = value; OnPropertyChanged(); ApplyFilters(); } }

 private System.Collections.Generic.List<Recipient> _baseFiltered; // after base filter/sort
 private Template _lastItemsForTemplate;
 private readonly Dictionary<string, PropertyInfo> _recipientPropCache = new(StringComparer.OrdinalIgnoreCase);

 private List<int> _pageSequence = new();
 public IReadOnlyList<int> PageSequence => _pageSequence;
 private void RebuildPageSequence()
 {
 _pageSequence = GetPagesToPrint() ?? new List<int>();
 if (_pageSequence.Count ==0) _pageSequence = Enumerable.Range(1, TotalPages).ToList();
 OnPropertyChanged(nameof(PageSequence));
 if (!_pageSequence.Contains(CurrentPage) && _pageSequence.Count >0)
 CurrentPage = _pageSequence[0];
 OnPropertyChanged(nameof(CurrentPageDisplay));
 }
 private int NextInSequence(int current)
 {
 if (_pageSequence == null || _pageSequence.Count ==0) return current;
 var idx = _pageSequence.IndexOf(current);
 if (idx <0) return _pageSequence[0];
 if (idx +1 < _pageSequence.Count) return _pageSequence[idx +1];
 return _pageSequence[idx];
 }
 private int PrevInSequence(int current)
 {
 if (_pageSequence == null || _pageSequence.Count ==0) return current;
 var idx = _pageSequence.IndexOf(current);
 if (idx <=0) return _pageSequence[0];
 return _pageSequence[idx -1];
 }

 public PrintPreviewViewModel()
 {
 Templates = new ObservableCollection<Template>(_dataService.GetAllTemplates() ?? new System.Collections.Generic.List<Template>());
 SelectedTemplate = Templates.FirstOrDefault();
 var allRecipients = _dataService.GetAllRecipients() ?? new System.Collections.Generic.List<Recipient>();
 Recipients = new ObservableCollection<Recipient>(allRecipients);
 AvailableCities = new ObservableCollection<string>(allRecipients.Select(r => r.City).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c));
 SelectedCity = AvailableCities.FirstOrDefault();

 PrevPageCommand = new RelayCommand(_ => { RebuildPageSequence(); CurrentPage = PrevInSequence(CurrentPage); });
 NextPageCommand = new RelayCommand(_ => { RebuildPageSequence(); CurrentPage = NextInSequence(CurrentPage); });
 SavePresetCommand = new RelayCommand(SavePreset);
 DeletePresetCommand = new RelayCommand(DeletePreset);
 IncreaseCopiesCommand = new RelayCommand(_ => Copies++);
 DecreaseCopiesCommand = new RelayCommand(_ => { if (Copies >1) Copies--; });
 ZoomInCommand = new RelayCommand(_ => ZoomPercentage = System.Math.Min(200, ZoomPercentage +10));
 ZoomOutCommand = new RelayCommand(_ => ZoomPercentage = System.Math.Max(20, ZoomPercentage -10));
 ResetZoomCommand = new RelayCommand(_ => ZoomPercentage =100);
 OpenFullPreviewCommand = new RelayCommand(_ => OpenFullPreviewWindow());

 // defaults for print range
 IsRangeAll = true;
 RangeExpression = "1-" + TotalPages.ToString();

 LoadPresets();
 LoadPrinters();
 TrySelectDefaultPrinter();
 FilterRecipients();
 }

 // Snapshot constructor for printing (skip data loading)
 public PrintPreviewViewModel(bool skipInitialization)
 {
 if (!skipInitialization) throw new InvalidOperationException("Use default constructor for UI; for printing pass true.");
 Templates = new ObservableCollection<Template>();
 Recipients = new ObservableCollection<Recipient>();
 AvailableCities = new ObservableCollection<string>();
 PreviewItems = new ObservableCollection<TemplateItemViewModel>();
 }

 private void OpenFullPreviewWindow()
 {
 var wnd = new FullPreviewWindow { DataContext = this };
 if (Application.Current != null && Application.Current.MainWindow != wnd) wnd.Owner = Application.Current.MainWindow;
 wnd.Show();
 }

 private void LoadPrinters()
 {
 try
 {
 var server = new LocalPrintServer();
 var queues = server.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections });
 Printers.Clear();
 foreach (var q in queues) Printers.Add(q);
 // don't set SelectedPrinter here; call TrySelectDefaultPrinter which also triggers sizes
 TrySelectDefaultPrinter();
 }
 catch { }
 }

 private void TrySelectDefaultPrinter()
 {
 try
 {
 var server = new LocalPrintServer();
 var def = server.DefaultPrintQueue;
 if (def != null)
 {
 // if the same queue exists in Printers list, prefer that instance
 var match = Printers.FirstOrDefault(p => string.Equals(p.FullName, def.FullName, StringComparison.OrdinalIgnoreCase));
 SelectedPrinter = match ?? def;
 }
 else if (Printers.Count >0)
 {
 SelectedPrinter = Printers[0];
 }
 }
 catch
 {
 if (Printers.Count >0) SelectedPrinter = Printers[0];
 }
 }

 private void LoadPageSizes()
 {
 AvailablePageSizes.Clear();
 if (SelectedPrinter == null) { SelectedPageSize = null; return; }
 try
 {
 var caps = SelectedPrinter.GetPrintCapabilities();
 var list = caps?.PageMediaSizeCapability ?? new System.Collections.ObjectModel.ReadOnlyCollection<PageMediaSize>(new System.Collections.Generic.List<PageMediaSize>());
 foreach (var m in list) AvailablePageSizes.Add(new PageSizeOption(m));
 var a4 = AvailablePageSizes.FirstOrDefault(p => p.Media.PageMediaSizeName == PageMediaSizeName.ISOA4);
 SelectedPageSize = a4 ?? AvailablePageSizes.FirstOrDefault();
 }
 catch { SelectedPageSize = null; }
 UpdatePrinterMargins();
 }

 private void UpdateSheetSizeFromPageSize()
 {
 if (SelectedPageSize != null)
 {
 var w = SelectedPageSize.WidthMm; var h = SelectedPageSize.HeightMm;
 if (IsPrinterLandscape) { SheetWidthMm = h; SheetHeightMm = w; }
 else { SheetWidthMm = w; SheetHeightMm = h; }
 }
 else { SheetWidthMm =210; SheetHeightMm =297; }
 UpdatePrinterMargins();
 // No need to reload items; geometry only
 }

 private void ApplyFilters()
 {
 if (_baseFiltered == null) return;
 var q = _baseFiltered.AsEnumerable();
 if (!string.IsNullOrWhiteSpace(FilterOrgContains))
 {
 var s = FilterOrgContains.ToLowerInvariant();
 q = q.Where(r => (r.OrganizationName ?? "").ToLowerInvariant().Contains(s));
 }
 if (!string.IsNullOrWhiteSpace(FilterCityEquals))
 {
 q = q.Where(r => string.Equals(r.City ?? string.Empty, FilterCityEquals, StringComparison.OrdinalIgnoreCase));
 }
 if (!string.IsNullOrWhiteSpace(FilterPostalStarts))
 {
 var s = FilterPostalStarts.ToLowerInvariant();
 q = q.Where(r => (r.PostalCode ?? "").ToLowerInvariant().StartsWith(s));
 }
 Recipients = new ObservableCollection<Recipient>(q.ToList());
 TotalCount = Recipients.Count;
 CurrentPage =1;
 OnPropertyChanged(nameof(Recipients));
 OnPropertyChanged(nameof(TotalPages));
 OnPropertyChanged(nameof(CurrentPageDisplay));
 LoadPreviewItems();
 RebuildPageSequence();
 }

 private void FilterRecipients()
 {
 var all = _dataService.GetAllRecipients();
 var filtered = all.AsEnumerable();
 if (!string.IsNullOrEmpty(SelectedCity)) filtered = filtered.Where(r => r.City == SelectedCity);
 if (!string.IsNullOrWhiteSpace(SearchText))
 {
 var s = SearchText.ToLowerInvariant();
 filtered = filtered.Where(r =>
 (r.OrganizationName ?? string.Empty).ToLowerInvariant().Contains(s) ||
 (r.AddressLine1 ?? string.Empty).ToLowerInvariant().Contains(s) ||
 (r.City ?? string.Empty).ToLowerInvariant().Contains(s) ||
 (r.PostalCode ?? string.Empty).ToLowerInvariant().Contains(s));
 }
 filtered = ApplySorting(filtered);
 _baseFiltered = filtered.ToList();
 Recipients = new ObservableCollection<Recipient>(_baseFiltered);
 TotalCount = Recipients.Count;
 RangeFrom =1; RangeTo = TotalPages; CurrentPage =1;
 OnPropertyChanged(nameof(Recipients));
 OnPropertyChanged(nameof(TotalCount));
 OnPropertyChanged(nameof(TotalPages));
 OnPropertyChanged(nameof(CurrentPageDisplay));
 RangeExpression = $"1-{TotalPages}";
 LoadPreviewItems();
 RebuildPageSequence();
 }

 private void UpdatePrinterMargins()
 {
 double leftMm =0, topMm =0, rightMm =0, bottomMm =0;
 try
 {
 if (SelectedPrinter != null)
 {
 // Build a ticket that reflects selected media size and orientation
 var ticket = new PrintTicket();
 if (SelectedPageSize?.Media?.PageMediaSizeName != null)
 {
 ticket.PageMediaSize = new PageMediaSize(SelectedPageSize.Media.PageMediaSizeName.Value);
 }
 else if (SelectedPageSize != null)
 {
 // Fallback: explicit size in DIU
 ticket.PageMediaSize = new PageMediaSize(Units.MmToDiu(SelectedPageSize.WidthMm), Units.MmToDiu(SelectedPageSize.HeightMm));
 }
 ticket.PageOrientation = IsPrinterLandscape ? PageOrientation.Landscape : PageOrientation.Portrait;

 var caps = SelectedPrinter.GetPrintCapabilities(ticket);
 var area = caps?.PageImageableArea;
 if (area != null)
 {
 double pageW = Units.MmToDiu(SheetWidthMm); // already oriented by UpdateSheetSizeFromPageSize
 double pageH = Units.MmToDiu(SheetHeightMm);

 double oW = area.OriginWidth;
 double oH = area.OriginHeight;
 double eW = area.ExtentWidth;
 double eH = area.ExtentHeight;

 // Heuristic: some drivers return portrait values even when ticket is landscape.
 // If orientation is landscape but imageable area looks portrait (eW < eH while pageW > pageH), swap axes.
 bool needSwap = IsPrinterLandscape && (eW < eH && pageW > pageH);
 if (needSwap)
 {
 (oW, oH) = (oH, oW);
 (eW, eH) = (eH, eW);
 }

 double rDiu = Math.Max(0, pageW - (oW + eW));
 double bDiu = Math.Max(0, pageH - (oH + eH));
 leftMm = Units.DiuToMm(oW);
 topMm = Units.DiuToMm(oH);
 rightMm = Units.DiuToMm(rDiu);
 bottomMm = Units.DiuToMm(bDiu);
 }
 }
 }
 catch { }
 MarginLeftMm = leftMm;
 MarginTopMm = topMm;
 MarginRightMm = rightMm;
 MarginBottomMm = bottomMm;
 // geometry only
 }

 private System.Collections.Generic.IEnumerable<Recipient> ApplySorting(System.Collections.Generic.IEnumerable<Recipient> query)
 {
 return SortBy switch
 {
 nameof(Recipient.OrganizationName) => SortDescending ? query.OrderByDescending(r => r.OrganizationName) : query.OrderBy(r => r.OrganizationName),
 nameof(Recipient.City) => SortDescending ? query.OrderByDescending(r => r.City) : query.OrderBy(r => r.City),
 nameof(Recipient.PostalCode) => SortDescending ? query.OrderByDescending(r => r.PostalCode) : query.OrderBy(r => r.PostalCode),
 _ => query
 };
 }

 private void LoadPresets()
 {
 Presets.Clear();
 foreach (var p in _presetsService.Load()) Presets.Add(p);
 SelectedPreset = Presets.FirstOrDefault();
 }

 private void SavePreset(object obj)
 {
 var name = obj as string;
 if (string.IsNullOrWhiteSpace(name)) name = $"Preset {Presets.Count +1}";
 var preset = new FilterPreset
 {
 Name = name,
 City = SelectedCity,
 SearchText = SearchText,
 SortBy = SortBy,
 SortDescending = SortDescending
 };
 var list = Presets.ToList();
 var existing = list.FirstOrDefault(p => p.Name == name);
 if (existing != null) list.Remove(existing);
 list.Insert(0, preset);
 _presetsService.Save(list);
 Presets.Clear(); foreach (var p in list) Presets.Add(p);
 SelectedPreset = preset;
 }

 private void DeletePreset(object obj)
 {
 if (SelectedPreset == null) return;
 var list = Presets.ToList();
 list.Remove(SelectedPreset);
 _presetsService.Save(list);
 Presets.Clear(); foreach (var p in list) Presets.Add(p);
 SelectedPreset = Presets.FirstOrDefault();
 }

 private void ApplyPreset(FilterPreset p)
 {
 if (p == null) return;
 SelectedCity = p.City;
 SearchText = p.SearchText;
 SortBy = p.SortBy;
 SortDescending = p.SortDescending;
 }

 public void LoadPreviewItems()
 {
 if (SelectedTemplate == null)
 {
 PreviewItems.Clear();
 OnPropertyChanged(nameof(PreviewItems));
 return;
 }

 // Initialize items only when template changes or items count mismatch
 if (!ReferenceEquals(_lastItemsForTemplate, SelectedTemplate) || PreviewItems.Count != (SelectedTemplate.Items?.Count ??0))
 {
 PreviewItems.Clear();
 foreach (var item in SelectedTemplate.Items.OrderBy(i => i.ZIndex))
 {
 var vmItem = new TemplateItemViewModel(item);
 PreviewItems.Add(vmItem);
 }
 _lastItemsForTemplate = SelectedTemplate;
 OnPropertyChanged(nameof(PreviewItems));
 }

 // Update texts for the current page's recipient if exists; otherwise keep template static text
 var recipient = Recipients != null && Recipients.Count >0 ? Recipients.Skip((CurrentPage -1) * PageSize).FirstOrDefault() : null;
 foreach (var vmItem in PreviewItems)
 {
 var item = vmItem.Model;
 if (!string.IsNullOrEmpty(item.ContentBindingPath) && recipient != null)
 {
 if (!_recipientPropCache.TryGetValue(item.ContentBindingPath, out var prop))
 {
 prop = typeof(Recipient).GetProperty(item.ContentBindingPath, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
 if (prop != null) _recipientPropCache[item.ContentBindingPath] = prop;
 }
 var value = prop?.GetValue(recipient)?.ToString();
 vmItem.StaticText = value ?? string.Empty;
 }
 else
 {
 vmItem.StaticText = item.StaticText;
 }
 }
 }

 public List<int> GetPagesToPrint()
 {
 int total = TotalPages;
 if (total <=0) return new List<int>();
 if (IsRangeCurrent) return new List<int> { Math.Max(1, Math.Min(CurrentPage, total)) };
 if (IsRangeAll) return Enumerable.Range(1, total).ToList();
 // custom
 var set = new SortedSet<int>();
 var expr = RangeExpression ?? string.Empty;
 foreach (var token in expr.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
 {
 var t = token.Trim();
 if (t.Contains('-'))
 {
 var parts = t.Split('-', StringSplitOptions.RemoveEmptyEntries);
 if (parts.Length ==1)
 {
 if (int.TryParse(parts[0], out int a)) set.Add(Math.Clamp(a,1, total));
 }
 else
 {
 if (int.TryParse(parts[0], out int a) && int.TryParse(parts[1], out int b))
 {
 int from = Math.Clamp(Math.Min(a, b),1, total);
 int to = Math.Clamp(Math.Max(a, b),1, total);
 for (int i = from; i <= to; i++) set.Add(i);
 }
 }
 }
 else if (int.TryParse(t, out int p))
 {
 set.Add(Math.Clamp(p,1, total));
 }
 }
 return set.ToList();
 }

 public event PropertyChangedEventHandler PropertyChanged;
 protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
 {
 PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
 }
 }
}

using EnvelopePrinter.Core;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Envelope_printing
{
 public class OptionItem
 {
 public string Display { get; }
 public string Value { get; }
 public OptionItem(string display, string value) { Display = display; Value = value; }
 public override string ToString() => Display;
 }

 public class TemplateDesignerViewModel : INotifyPropertyChanged
 {
 public event PropertyChangedEventHandler PropertyChanged;
 protected void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

 private readonly DataService _dataService;
 private readonly UIService _ui_service = UIService.Instance;
 private bool _isDragging;
 private Point _dragStartPoint;
 private Point _dragStartItemPos;
 private const double PxPerMm =3.78;

 public event Func<Size> GetViewSizeRequested;

 private bool _isLeftPanelVisible = true;
 public bool IsLeftPanelVisible { get => _isLeftPanelVisible; set { if (_isLeftPanelVisible == value) return; _isLeftPanelVisible = value; OnPropertyChanged(); } }
 public double LeftPanelHiddenPosition => _ui_service.IsMainNavExpanded ? -258 : -258 + (220 -60);

 private bool _isRightPanelOpen;
 public bool IsRightPanelOpen { get => _isRightPanelOpen; set { if (_isRightPanelOpen == value) return; _isRightPanelOpen = value; OnPropertyChanged(); UpdatePropertiesPanelVisibility(); } }

 private bool _isCanvasPropertiesVisible;
 public bool IsCanvasPropertiesVisible { get => _isCanvasPropertiesVisible; private set { if (_isCanvasPropertiesVisible == value) return; _isCanvasPropertiesVisible = value; OnPropertyChanged(); } }
 private bool _isItemPropertiesVisible;
 public bool IsItemPropertiesVisible { get => _isItemPropertiesVisible; private set { if (_isItemPropertiesVisible == value) return; _isItemPropertiesVisible = value; OnPropertyChanged(); } }

 public ObservableCollection<Template> Templates { get; private set; }
 private Template _selectedTemplate;
 public Template SelectedTemplate { get => _selectedTemplate; set { _selectedTemplate = value; OnPropertyChanged(); UpdateSelectedTemplateProperties(); } }

 private string _selectedTemplateName;
 public string SelectedTemplateName { get => _selectedTemplateName; set { if (_selectedTemplateName == value) return; _selectedTemplateName = value; if (SelectedTemplate != null) SelectedTemplate.Name = value; CollectionViewSource.GetDefaultView(Templates)?.Refresh(); OnPropertyChanged(); } }
 private double _selectedTemplateWidth;
 public double SelectedTemplateWidth { get => _selectedTemplateWidth; set { if (_selectedTemplateWidth == value) return; _selectedTemplateWidth = value; if (SelectedTemplate != null) SelectedTemplate.EnvelopeWidth = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayWidth)); ValidateAllItemsBounds(); } }
 private double _selectedTemplateHeight;
 public double SelectedTemplateHeight { get => _selectedTemplateHeight; set { if (_selectedTemplateHeight == value) return; _selectedTemplateHeight = value; if (SelectedTemplate != null) SelectedTemplate.EnvelopeHeight = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayHeight)); ValidateAllItemsBounds(); } }
 public double DisplayWidth => SelectedTemplateWidth * PxPerMm;
 public double DisplayHeight => SelectedTemplateHeight * PxPerMm;

 private ObservableCollection<TemplateItemViewModel> _selectedTemplateItems;
 public ObservableCollection<TemplateItemViewModel> SelectedTemplateItems { get => _selectedTemplateItems; private set { _selectedTemplateItems = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanvasItems)); } }
 private TemplateItemViewModel _selectedTemplateItem;
 public TemplateItemViewModel SelectedTemplateItem { get => _selectedTemplateItem; set { _selectedTemplateItem = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsItemSelected)); UpdateSelectedItemProperties(); UpdatePropertiesPanelVisibility(); } }

 public ObservableCollection<string> AvailableRecipientColumns { get; private set; }
 private string _selectedItemContentBinding;
 public string SelectedItemContentBinding { get => _selectedItemContentBinding; set { if (_selectedItemContentBinding == value) return; _selectedItemContentBinding = value; if (SelectedTemplateItem != null) SelectedTemplateItem.ContentBindingPath = value; OnPropertyChanged(); } }
 public ObservableCollection<string> FontFamilies { get; } = new ObservableCollection<string> { "Arial", "Times New Roman", "Segoe UI", "Verdana", "Calibri", "Tahoma", "Courier New", "Georgia", "Garamond" };
 public ObservableCollection<OptionItem> HorizontalAlignments { get; } = new ObservableCollection<OptionItem>
 {
 new OptionItem("Слева","Left"), new OptionItem("По центру","Center"), new OptionItem("Справа","Right")
 };
 public ObservableCollection<OptionItem> VerticalAlignments { get; } = new ObservableCollection<OptionItem>
 {
 new OptionItem("Сверху","Top"), new OptionItem("По центру","Center"), new OptionItem("Снизу","Bottom")
 };
 public ObservableCollection<OptionItem> StretchModes { get; } = new ObservableCollection<OptionItem>
 {
 new OptionItem("Без растяжения","None"), new OptionItem("Равномерно","Uniform"), new OptionItem("Заполнение","Fill"), new OptionItem("Заполнение (обрезка)","UniformToFill")
 };
 public ObservableCollection<OptionItem> ColorChoices { get; } = new ObservableCollection<OptionItem>
 {
 new OptionItem("Прозрачный","Transparent"),
 new OptionItem("Чёрный","Black"),
 new OptionItem("Белый","White"),
 new OptionItem("Серый","Gray"),
 new OptionItem("Красный","Red"),
 new OptionItem("Синий","Blue"),
 new OptionItem("Голубой","LightBlue"),
 new OptionItem("Зелёный","Green")
 };
 public ObservableCollection<OptionItem> TextAlignments { get; } = new ObservableCollection<OptionItem>
 {
 new OptionItem("Слева","Left"), new OptionItem("По центру","Center"), new OptionItem("Справа","Right"), new OptionItem("По ширине","Justify")
 };

 private int _zoomPercentage =100;
 public int ZoomPercentage { get => _zoomPercentage; set { if (_zoomPercentage == value) return; _zoomPercentage = value; OnPropertyChanged(); OnPropertyChanged(nameof(ZoomFactor)); } }
 public double ZoomFactor => ZoomPercentage /100.0;
 private const int MinZoomPercent =20, MaxZoomPercent =200, ZoomStepPercent =10;

 public bool IsTemplateSelected => SelectedTemplate != null;
 public bool IsItemSelected => SelectedTemplateItem != null;

 public ICommand AddTemplateCommand { get; }
 public ICommand RemoveTemplateCommand { get; }
 public ICommand SaveChangesCommand { get; }
 public ICommand AddTextBlockCommand { get; }
 public ICommand AddImageCommand { get; }
 public ICommand DeleteItemCommand { get; }
 public ICommand ShowItemPropertiesCommand { get; }
 public ICommand ShowCanvasPropertiesCommand { get; }
 public ICommand FixItemCommand { get; }
 public ICommand MoveItemUpCommand { get; }
 public ICommand MoveItemDownCommand { get; }
 public ICommand BringToFrontCommand { get; }
 public ICommand SendToBackCommand { get; }
 public ICommand ZoomInCommand { get; }
 public ICommand ZoomOutCommand { get; }
 public ICommand ResetZoomCommand { get; }
 public ICommand PreviewCommand { get; }
 public ICommand BindSelectedItemToColumnCommand { get; }
 public ICommand UnbindSelectedItemCommand { get; }
 public ICommand ToggleLeftPanelCommand { get; }
 public ICommand ChangeImageCommand { get; }

 public TemplateDesignerViewModel()
 {
 if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
 {
 Templates = new ObservableCollection<Template>();
 AvailableRecipientColumns = new ObservableCollection<string>(new[] { "Name", "Address", "City" });
 SelectedTemplateItems = new ObservableCollection<TemplateItemViewModel>();
 IsRightPanelOpen = false;
 IsCanvasPropertiesVisible = false;
 IsItemPropertiesVisible = false;
 return;
 }

 _dataService = new DataService();
 LoadTemplates();
 LoadAvailableRecipientColumns();

 _ui_service.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(UIService.IsMainNavExpanded)) OnPropertyChanged(nameof(LeftPanelHiddenPosition)); };

 AddTemplateCommand = new RelayCommand(AddTemplate);
 RemoveTemplateCommand = new RelayCommand(_ => RemoveTemplate(SelectedTemplate), _ => IsTemplateSelected);
 SaveChangesCommand = new RelayCommand(SaveChanges, _ => IsTemplateSelected);
 AddTextBlockCommand = new RelayCommand(AddTextBlock, _ => IsTemplateSelected);
 AddImageCommand = new RelayCommand(AddImage, _ => IsTemplateSelected);
 DeleteItemCommand = new RelayCommand(DeleteItem, _ => IsItemSelected);
 FixItemCommand = new RelayCommand(FixItem, p => SelectedTemplate != null && (p is TemplateItemViewModel || IsItemSelected));

 ShowItemPropertiesCommand = new RelayCommand(p => { if (p is TemplateItemViewModel item) { SelectedTemplateItem = item; ShowItemProperties(); } else { ShowItemProperties(); } });
 ShowCanvasPropertiesCommand = new RelayCommand(p => ShowCanvasProperties());
 MoveItemUpCommand = new RelayCommand(p => MoveItemUp(p as TemplateItemViewModel), p => CanMoveUp(p as TemplateItemViewModel));
 MoveItemDownCommand = new RelayCommand(p => MoveItemDown(p as TemplateItemViewModel), p => CanMoveDown(p as TemplateItemViewModel));
 BringToFrontCommand = new RelayCommand(p => BringToFront(p as TemplateItemViewModel), p => p is TemplateItemViewModel);
 SendToBackCommand = new RelayCommand(p => SendToBack(p as TemplateItemViewModel), p => p is TemplateItemViewModel);
 ToggleLeftPanelCommand = new RelayCommand(p => IsLeftPanelVisible = !IsLeftPanelVisible);

 ZoomInCommand = new RelayCommand(_ => ZoomPercentage = Math.Min(MaxZoomPercent, ZoomPercentage + ZoomStepPercent), _ => IsTemplateSelected);
 ZoomOutCommand = new RelayCommand(_ => ZoomPercentage = Math.Max(MinZoomPercent, ZoomPercentage - ZoomStepPercent), _ => IsTemplateSelected);
 ResetZoomCommand = new RelayCommand(_ => ZoomPercentage =100, _ => IsTemplateSelected);

 PreviewCommand = new RelayCommand(PreviewTemplate, _ => IsTemplateSelected);
 BindSelectedItemToColumnCommand = new RelayCommand(BindSelectedItemToColumn, _ => IsItemSelected && !string.IsNullOrEmpty(SelectedItemContentBinding));
 UnbindSelectedItemCommand = new RelayCommand(UnbindSelectedItem, _ => IsItemSelected && SelectedTemplateItem.IsBound);
 ChangeImageCommand = new RelayCommand(ChangeImage, _ => SelectedTemplateItem != null && SelectedTemplateItem.IsImage);

 SelectedTemplateItems = new ObservableCollection<TemplateItemViewModel>();
 IsRightPanelOpen = false;
 }

 private void LoadTemplates() => Templates = new ObservableCollection<Template>(_dataService.GetAllTemplates());
 private void LoadAvailableRecipientColumns() { var props = typeof(Recipient).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Where(p => p.Name != "Id").Select(p => p.Name).ToList(); AvailableRecipientColumns = new ObservableCollection<string>(props); }

 private void AddTemplate(object obj)
 {
 var dialog = new NewTemplateDialog();
 if (dialog.ShowDialog() == true)
 {
 var newTemplate = new Template { Name = dialog.TemplateName };
 _dataService.AddTemplate(newTemplate);
 Templates.Add(newTemplate);
 SelectedTemplate = newTemplate;
 }
 }

 public void RemoveTemplate(Template template)
 {
 if (template == null) return;
 var result = MessageBox.Show($"Удалить шаблон '{template.Name}'?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
 if (result == MessageBoxResult.Yes)
 {
 _dataService.DeleteTemplate(template);
 Templates.Remove(template);
 SelectedTemplate = Templates.FirstOrDefault();
 }
 }

 private void AddTextBlock(object obj)
 {
 if (SelectedTemplate == null) return;
 var newItem = new TemplateItem { PositionX =50, PositionY =50, Width =150, Height =25, StaticText = "Новый текст", TemplateId = SelectedTemplate.Id };
 SelectedTemplate.Items.Add(newItem);
 var newItemVM = new TemplateItemViewModel(newItem);
 SelectedTemplateItems.Add(newItemVM);
 SelectedTemplateItem = newItemVM;
 }

 private void AddImage(object obj)
 {
 if (SelectedTemplate == null) return;
 var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Выбор изображения", Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp|Все файлы|*.*" };
 if (dlg.ShowDialog() == true)
 {
 var newItem = new TemplateItem { PositionX =50, PositionY =50, Width =100, Height =100, IsImage = true, ImagePath = dlg.FileName, TemplateId = SelectedTemplate.Id };
 SelectedTemplate.Items.Add(newItem);
 var newItemVM = new TemplateItemViewModel(newItem);
 SelectedTemplateItems.Add(newItemVM);
 SelectedTemplateItem = newItemVM;
 }
 }

 private void DeleteItem(object obj)
 {
 if (SelectedTemplateItem == null) return;
 SelectedTemplate.Items.Remove(SelectedTemplateItem.Model);
 SelectedTemplateItems.Remove(SelectedTemplateItem);
 // hide properties and clear selection
 SelectedTemplateItem = null;
 IsRightPanelOpen = false;
 UpdatePropertiesPanelVisibility();
 }

 private void FixItem(object obj)
 {
 if (SelectedTemplate == null) return;
 var item = obj as TemplateItemViewModel ?? SelectedTemplateItem;
 if (item == null) return;
 double maxX = Math.Max(0, SelectedTemplate.EnvelopeWidth - item.Width);
 double maxY = Math.Max(0, SelectedTemplate.EnvelopeHeight - item.Height);
 if (item.PositionX <0) item.PositionX =0;
 if (item.PositionY <0) item.PositionY =0;
 if (item.PositionX > maxX) item.PositionX = maxX;
 if (item.PositionY > maxY) item.PositionY = maxY;
 item.Transform.X = item.PositionX;
 item.Transform.Y = item.PositionY;
 item.CheckBounds(SelectedTemplate.EnvelopeWidth, SelectedTemplate.EnvelopeHeight);
 }

 private void BindSelectedItemToColumn(object obj) { if (SelectedTemplateItem == null) return; SelectedTemplateItem.ContentBindingPath = SelectedItemContentBinding; }
 private void UnbindSelectedItem(object obj) { if (SelectedTemplateItem == null) return; SelectedTemplateItem.ContentBindingPath = string.Empty; }

 private void SaveChanges(object obj)
 {
 if (SelectedTemplate == null) return;
 // normalize items to satisfy NOT NULL constraints
 foreach (var it in SelectedTemplate.Items)
 {
 if (it.ContentBindingPath == null) it.ContentBindingPath = string.Empty;
 if (it.Name == null) it.Name = string.Empty;
 if (it.StaticText == null) it.StaticText = string.Empty;
 if (it.ImagePath == null) it.ImagePath = string.Empty;
 if (it.Foreground == null) it.Foreground = "Black";
 if (it.Background == null) it.Background = "Transparent";
 if (it.BorderBrush == null) it.BorderBrush = "Transparent";
 }
 SelectedTemplate.Name = SelectedTemplateName; SelectedTemplate.EnvelopeWidth = SelectedTemplateWidth; SelectedTemplate.EnvelopeHeight = SelectedTemplateHeight;
 _dataService.UpdateTemplate(SelectedTemplate);
 MessageBox.Show("Изменения сохранены.", "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
 }

 private void PreviewTemplate(object obj) => MessageBox.Show("Preview not implemented.");

 private void UpdateSelectedTemplateProperties()
 {
 if (SelectedTemplate != null)
 {
 SelectedTemplateName = SelectedTemplate.Name;
 SelectedTemplateWidth = SelectedTemplate.EnvelopeWidth;
 SelectedTemplateHeight = SelectedTemplate.EnvelopeHeight;
 SelectedTemplateItems = new ObservableCollection<TemplateItemViewModel>(SelectedTemplate.Items.Select(item => new TemplateItemViewModel(item)));
 ValidateAllItemsBounds();
 // Removed automatic CalculateAndSetInitialZoom here; it will be invoked by the View after layout is ready
 }
 else { SelectedTemplateItems?.Clear(); }
 // Reset selection and keep properties panel closed by default
 SelectedTemplateItem = null;
 IsRightPanelOpen = false;
 OnPropertyChanged(nameof(IsTemplateSelected));
 OnPropertyChanged(nameof(DisplayWidth));
 OnPropertyChanged(nameof(DisplayHeight));
 }

 private void UpdateSelectedItemProperties()
 {
 if (SelectedTemplateItem != null)
 {
 SelectedItemContentBinding = SelectedTemplateItem.ContentBindingPath;
 }
 OnPropertyChanged(nameof(IsItemSelected));
 }

 private void UpdatePropertiesPanelVisibility()
 {
 IsCanvasPropertiesVisible = IsRightPanelOpen && SelectedTemplateItem == null;
 IsItemPropertiesVisible = IsRightPanelOpen && SelectedTemplateItem != null;
 }

 public void ShowCanvasProperties() { SelectedTemplateItem = null; IsRightPanelOpen = true; IsCanvasPropertiesVisible = true; IsItemPropertiesVisible = false; UpdatePropertiesPanelVisibility(); OnPropertyChanged(nameof(IsItemSelected)); }
 public void ShowItemProperties() { IsRightPanelOpen = true; IsCanvasPropertiesVisible = false; IsItemPropertiesVisible = true; UpdatePropertiesPanelVisibility(); }
 public void HideProperties() { IsRightPanelOpen = false; UpdatePropertiesPanelVisibility(); }

 public void ReopenItemProperties(TemplateItemViewModel item)
 {
 SelectedTemplateItem = item;
 if (IsRightPanelOpen) { IsRightPanelOpen = false; UpdatePropertiesPanelVisibility(); }
 IsRightPanelOpen = true; UpdatePropertiesPanelVisibility();
 }

 public void ReopenCanvasProperties()
 {
 SelectedTemplateItem = null;
 if (IsRightPanelOpen) { IsRightPanelOpen = false; UpdatePropertiesPanelVisibility(); }
 IsRightPanelOpen = true; UpdatePropertiesPanelVisibility();
 }

 public void StartDragging(TemplateItemViewModel item, Point startPoint)
 {
 if (item == null) return;
 SelectedTemplateItem = item;
 _isDragging = true; _dragStartPoint = startPoint; _dragStartItemPos = new Point(item.Transform.X, item.Transform.Y);
 }
 public void Drag(Point currentPoint)
 {
 if (!_isDragging || SelectedTemplateItem == null) return;
 var offset = currentPoint - _dragStartPoint;
 SelectedTemplateItem.Transform.X = _dragStartItemPos.X + offset.X; SelectedTemplateItem.Transform.Y = _dragStartItemPos.Y + offset.Y;
 }
 public void StopDragging()
 {
 if (!_isDragging || SelectedTemplateItem == null) return;
 _isDragging = false; SelectedTemplateItem.PositionX = SelectedTemplateItem.Transform.X; SelectedTemplateItem.PositionY = SelectedTemplateItem.Transform.Y; ValidateAllItemsBounds();
 }

 public void ValidateAllItemsBounds() { if (SelectedTemplateItems == null || SelectedTemplate == null) return; foreach (var item in SelectedTemplateItems) item.CheckBounds(SelectedTemplate.EnvelopeWidth, SelectedTemplate.EnvelopeHeight); }
 public void CalculateAndSetInitialZoom()
 {
 var viewSize = GetViewSizeRequested?.Invoke(); if (viewSize == null || viewSize.Value.Width <=0 || viewSize.Value.Height <=0) return;
 double margin =100; double availableWidth = viewSize.Value.Width - margin; double availableHeight = viewSize.Value.Height - margin;
 double envelopePxWidth = DisplayWidth; double envelopePxHeight = DisplayHeight; if (envelopePxWidth <=0 || envelopePxHeight <=0) return;
 double scaleX = availableWidth / envelopePxWidth; double scaleY = availableHeight / envelopePxHeight; double zoomFactor = Math.Min(1.0, Math.Min(scaleX, scaleY)); ZoomPercentage = (int)(zoomFactor *100);
 }

 private void ChangeImage(object obj) { if (SelectedTemplateItem == null) return; var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Выбор изображения", Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp" }; if (dlg.ShowDialog() == true) { SelectedTemplateItem.ImagePath = dlg.FileName; } }
 private bool CanMoveUp(TemplateItemViewModel item) => item != null && SelectedTemplateItems != null && item.ZIndex <10;
 private bool CanMoveDown(TemplateItemViewModel item) => item != null && SelectedTemplateItems != null && item.ZIndex >0;
 private void MoveItemUp(TemplateItemViewModel item) { if (item == null || SelectedTemplateItems == null) return; int cur = item.ZIndex; if (cur >=10) return; var target = SelectedTemplateItems.Where(i => i.ZIndex > cur).OrderBy(i => i.ZIndex).FirstOrDefault(); if (target != null) { target.ZIndex = cur; item.ZIndex = Math.Min(10, cur +1); } else { item.ZIndex = Math.Min(10, cur +1); } ReorderByZ(); }
 private void MoveItemDown(TemplateItemViewModel item) { if (item == null || SelectedTemplateItems == null) return; int cur = item.ZIndex; if (cur <=0) return; var target = SelectedTemplateItems.Where(i => i.ZIndex < cur).OrderByDescending(i => i.ZIndex).FirstOrDefault(); if (target != null) { target.ZIndex = cur; item.ZIndex = Math.Max(0, cur -1); } else { item.ZIndex = Math.Max(0, cur -1); } ReorderByZ(); }
 private void BringToFront(TemplateItemViewModel item) { if (item == null || SelectedTemplateItems == null) return; int max = SelectedTemplateItems.Max(i => i.ZIndex); item.ZIndex = Math.Min(10, Math.Max(max, item.ZIndex) +1); ReorderByZ(); }
 private void SendToBack(TemplateItemViewModel item) { if (item == null || SelectedTemplateItems == null) return; int min = SelectedTemplateItems.Min(i => i.ZIndex); item.ZIndex = Math.Max(0, Math.Min(min, item.ZIndex) -1); ReorderByZ(); }
 private void ReorderByZ() { if (SelectedTemplateItems == null) return; var ordered = SelectedTemplateItems.OrderBy(i => i.ZIndex).ToList(); SelectedTemplateItems.Clear(); foreach (var it in ordered) SelectedTemplateItems.Add(it); }

 public ObservableCollection<TemplateItemViewModel> CanvasItems => SelectedTemplateItems;

 // Ensure DB not-null by normalizing nulls to empty strings
 private static string Normalize(string s) => s ?? string.Empty;
 }
}
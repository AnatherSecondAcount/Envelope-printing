using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Envelope_printing
{
    using EnvelopePrinter.Core;

    public class OptionItem
    {
        public string Display { get; }
        public string Value { get; }
        public OptionItem(string display, string value) { Display = display; Value = value; }
        public override string ToString() => Display;
    }

    public class TemplateDesignerViewModel : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private readonly DataService _dataService;
        private readonly UIService _ui_service = UIService.Instance;
        private bool _isDragging;
        private Point _dragStartPoint;
        private Point _dragStartItemPos;
        private const double PxPerMm = 3.78;

        // Default sizes for newly created items (in mm)
        private const double DefaultTextWidth = 60.0;
        private const double DefaultTextHeight = 12.0;
        private const double DefaultImageWidth = 45.0;
        private const double DefaultImageHeight = 30.0;

        public event Func<Size> GetViewSizeRequested;

        private bool _isLeftPanelVisible = true;
        public bool IsLeftPanelVisible { get => _isLeftPanelVisible; set { if (_isLeftPanelVisible == value) return; _isLeftPanelVisible = value; OnPropertyChanged(); } }
        public double LeftPanelHiddenPosition => _ui_service.IsMainNavExpanded ? -258 : -258 + (220 - 60);

        private bool _isRightPanelOpen;
        public bool IsRightPanelOpen { get => _isRightPanelOpen; set { if (_isRightPanelOpen == value) return; _isRightPanelOpen = value; OnPropertyChanged(); UpdatePropertiesPanelVisibility(); } }

        private bool _isCanvasPropertiesVisible;
        public bool IsCanvasPropertiesVisible { get => _isCanvasPropertiesVisible; private set { if (_isCanvasPropertiesVisible == value) return; _isCanvasPropertiesVisible = value; OnPropertyChanged(); } }
        private bool _isItemPropertiesVisible;
        public bool IsItemPropertiesVisible { get => _isItemPropertiesVisible; private set { if (_isItemPropertiesVisible == value) return; _isItemPropertiesVisible = value; OnPropertyChanged(); } }

        public ObservableCollection<Template> Templates { get; private set; }
        private Template _selectedTemplate;
        public Template SelectedTemplate { get => _selectedTemplate; set { PersistPendingChanges(); _selectedTemplate = value; OnPropertyChanged(); UpdateSelectedTemplateProperties(); } }

        private string _selectedTemplateName;
        public string SelectedTemplateName { get => _selectedTemplateName; set { if (_selectedTemplateName == value) return; _selectedTemplateName = value; if (SelectedTemplate != null) SelectedTemplate.Name = value; CollectionViewSource.GetDefaultView(Templates)?.Refresh(); OnPropertyChanged(); } }
        private double _selectedTemplateWidth;
        public double SelectedTemplateWidth { get => _selectedTemplateWidth; set { if (_selectedTemplateWidth == value) return; _selectedTemplateWidth = value; if (SelectedTemplate != null) SelectedTemplate.EnvelopeWidth = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayWidth)); MarkAllDirty(); RequestValidateAllItemsBounds(); } }
        private double _selectedTemplateHeight;
        public double SelectedTemplateHeight { get => _selectedTemplateHeight; set { if (_selectedTemplateHeight == value) return; _selectedTemplateHeight = value; if (SelectedTemplate != null) SelectedTemplate.EnvelopeHeight = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayHeight)); MarkAllDirty(); RequestValidateAllItemsBounds(); } }
        public double DisplayWidth => SelectedTemplateWidth * PxPerMm;
        public double DisplayHeight => SelectedTemplateHeight * PxPerMm;

        private ObservableCollection<TemplateItemViewModel> _selectedTemplateItems;
        public ObservableCollection<TemplateItemViewModel> SelectedTemplateItems { get => _selectedTemplateItems; private set { _selectedTemplateItems = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanvasItems)); } }
        public ObservableCollection<TemplateItemViewModel> CanvasItems => SelectedTemplateItems;
        private TemplateItemViewModel _selectedTemplateItem;
        public TemplateItemViewModel SelectedTemplateItem { get => _selectedTemplateItem; set { _selectedTemplateItem = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsItemSelected)); UpdateSelectedItemProperties(); /* do not auto toggle panels here to keep canvas properties visible when selecting from list */ } }

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
 new OptionItem("Не изменять","None"), new OptionItem("По размеру","Uniform"), new OptionItem("Заполнить","Fill"), new OptionItem("Обрезать","UniformToFill")
 };

        // Путь к фоновому изображению (относительно папки Assets)
        public string CanvasBackgroundImagePath
        {
            get => SelectedTemplate?.BackgroundImagePath ?? string.Empty;
            set { if (SelectedTemplate == null) return; if (SelectedTemplate.BackgroundImagePath == value) return; SelectedTemplate.BackgroundImagePath = value; OnPropertyChanged(); }
        }
        public string CanvasBackgroundStretch
        {
            get => SelectedTemplate?.BackgroundStretch ?? "Uniform";
            set { if (SelectedTemplate == null) return; if (SelectedTemplate.BackgroundStretch == value) return; SelectedTemplate.BackgroundStretch = value; OnPropertyChanged(); }
        }

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

        private int _zoomPercentage = 100;
        public int ZoomPercentage { get => _zoomPercentage; set { if (_zoomPercentage == value) return; _zoomPercentage = value; OnPropertyChanged(); OnPropertyChanged(nameof(ZoomFactor)); } }
        public double ZoomFactor => ZoomPercentage / 100.0;
        private const int MinZoomPercent = 20, MaxZoomPercent = 200, ZoomStepPercent = 10;

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
        public ICommand ChangeBackgroundImageCommand { get; }
        public ICommand ClearBackgroundImageCommand { get; }

        // Debounce timer for validation
        private System.Timers.Timer _validateTimer;
        private const double DefaultValidateDebounceMs = 120; //120 ms debounce

        // New: dirty-tracking
        private readonly HashSet<TemplateItemViewModel> _dirtyItems = new HashSet<TemplateItemViewModel>();
        private readonly object _dirtyLock = new();

        // Keep reference to current collection subscription to unsubscribe later
        private NotifyCollectionChangedEventHandler _itemsCollectionChangedHandler;

        private bool _disposed = false;

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
            ResetZoomCommand = new RelayCommand(_ => ZoomPercentage = 100, _ => IsTemplateSelected);

            PreviewCommand = new RelayCommand(PreviewTemplate, _ => IsTemplateSelected);
            BindSelectedItemToColumnCommand = new RelayCommand(BindSelectedItemToColumn, _ => IsItemSelected && !string.IsNullOrEmpty(SelectedItemContentBinding));
            UnbindSelectedItemCommand = new RelayCommand(UnbindSelectedItem, _ => IsItemSelected && SelectedTemplateItem.IsBound);
            ChangeImageCommand = new RelayCommand(ChangeImage, _ => SelectedTemplateItem != null && SelectedTemplateItem.IsImage);
            ChangeBackgroundImageCommand = new RelayCommand(ChangeBackgroundImage, _ => IsTemplateSelected);
            ClearBackgroundImageCommand = new RelayCommand(_ => { if (SelectedTemplate == null) return; SelectedTemplate.BackgroundImagePath = string.Empty; OnPropertyChanged(nameof(CanvasBackgroundImagePath)); _dataService.UpdateTemplate(SelectedTemplate); }, _ => IsTemplateSelected);

            SelectedTemplateItems = new ObservableCollection<TemplateItemViewModel>();
            // subscribe to changes on collection
            _itemsCollectionChangedHandler = new NotifyCollectionChangedEventHandler(OnSelectedTemplateItemsChanged);
            SelectedTemplateItems.CollectionChanged += _itemsCollectionChangedHandler;
            IsRightPanelOpen = false;
        }

        private void PersistPendingChanges()
        {
            try
            {
                if (SelectedTemplate == null) return;
                // Save template and its items to prevent data loss when switching views
                _dataService.UpdateTemplate(SelectedTemplate);
            }
            catch { }
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
            var result = MessageBox.Show($"Удалить холст '{template.Name}'?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
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
            var newItem = new TemplateItem { PositionX = 10, PositionY = 10, Width = DefaultTextWidth, Height = DefaultTextHeight, StaticText = "Новый текст", TemplateId = SelectedTemplate.Id, ZIndex = 5 };
            SelectedTemplate.Items.Add(newItem);
            var newItemVM = new TemplateItemViewModel(newItem);
            newItemVM.PropertyChanged += OnTemplateItemPropertyChanged;
            SelectedTemplateItems.Add(newItemVM);
            SelectedTemplateItem = newItemVM;
            MarkItemDirty(newItemVM);
        }

        private void AddImage(object obj)
        {
            if (SelectedTemplate == null) return;
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Выбор изображения", Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp|Все файлы|*.*" };
            if (dlg.ShowDialog() == true)
            {
                var storedPath = ImportToAssets(dlg.FileName);
                var newItem = new TemplateItem { PositionX = 15, PositionY = 15, Width = DefaultImageWidth, Height = DefaultImageHeight, IsImage = true, ImagePath = storedPath, TemplateId = SelectedTemplate.Id, ZIndex = 4 };
                SelectedTemplate.Items.Add(newItem);
                var newItemVM = new TemplateItemViewModel(newItem);
                newItemVM.PropertyChanged += OnTemplateItemPropertyChanged;
                SelectedTemplateItems.Add(newItemVM);
                SelectedTemplateItem = newItemVM;
                MarkItemDirty(newItemVM);
            }
        }

        private void DeleteItem(object obj)
        {
            if (SelectedTemplateItem == null) return;
            var toRemove = SelectedTemplateItem;
            SelectedTemplate.Items.Remove(SelectedTemplateItem.Model);
            SelectedTemplateItems.Remove(SelectedTemplateItem);
            toRemove.PropertyChanged -= OnTemplateItemPropertyChanged;
            // persist deletion immediately
            try { _dataService.UpdateTemplate(SelectedTemplate); } catch { }
            // clear selection; keep right panel state if it was showing canvas properties
            SelectedTemplateItem = null;
            UpdatePropertiesPanelVisibility();
        }

        private void FixItem(object obj)
        {
            if (SelectedTemplate == null) return;
            var item = obj as TemplateItemViewModel ?? SelectedTemplateItem;
            if (item == null) return;
            double maxX = Math.Max(0, SelectedTemplate.EnvelopeWidth - item.Width);
            double maxY = Math.Max(0, SelectedTemplate.EnvelopeHeight - item.Height);
            bool oversizedX = item.Width > SelectedTemplate.EnvelopeWidth;
            bool oversizedY = item.Height > SelectedTemplate.EnvelopeHeight;
            if (item.PositionX < 0) item.PositionX = 0;
            if (item.PositionY < 0) item.PositionY = 0;
            if (item.PositionX > maxX) item.PositionX = maxX;
            if (item.PositionY > maxY) item.PositionY = maxY;
            item.Transform.X = item.PositionX;
            item.Transform.Y = item.PositionY;
            item.CheckBounds(SelectedTemplate.EnvelopeWidth, SelectedTemplate.EnvelopeHeight);
            if (oversizedX || oversizedY)
            {
                try
                {
                    MessageBox.Show("Объект больше размеров холста. Он перемещён влево верхний угол, но часть может оставаться вне видимой области. Уменьшите его размер, чтобы полностью уместить.",
                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch { }
            }
        }

        private void BindSelectedItemToColumn(object obj) { if (SelectedTemplateItem == null) return; SelectedTemplateItem.ContentBindingPath = SelectedItemContentBinding; }
        private void UnbindSelectedItem(object obj) { if (SelectedTemplateItem == null) return; SelectedTemplateItem.ContentBindingPath = string.Empty; }

        private void SaveChanges(object obj)
        {
            if (SelectedTemplate == null) return;
            foreach (var it in SelectedTemplate.Items)
            {
                if (it.ContentBindingPath == null) it.ContentBindingPath = string.Empty;
                if (it.Name == null) it.Name = string.Empty;
                if (it.StaticText == null) it.StaticText = string.Empty;
                if (it.ImagePath == null) it.ImagePath = string.Empty;
                if (it.Foreground == null) it.Foreground = "Black";
                if (it.Background == null) it.Background = "Transparent";
                if (it.BorderBrush == null) it.BorderBrush = "Transparent";
                if (it.ZIndex < 0) it.ZIndex = 0;
            }
            SelectedTemplate.Name = SelectedTemplateName; SelectedTemplate.EnvelopeWidth = SelectedTemplateWidth; SelectedTemplate.EnvelopeHeight = SelectedTemplateHeight;
            _dataService.UpdateTemplate(SelectedTemplate);
            MessageBox.Show("Изменения сохранены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PreviewTemplate(object obj)
        {
            try
            {
                var vm = new PrintPreviewViewModel(skipInitialization: true)
                {
                    SelectedTemplate = this.SelectedTemplate,
                    SheetWidthMm = this.SelectedTemplate?.EnvelopeWidth ?? 0,
                    SheetHeightMm = this.SelectedTemplate?.EnvelopeHeight ?? 0,
                    MarginLeftMm = 0,
                    MarginTopMm = 0,
                    MarginRightMm = 0,
                    MarginBottomMm = 0,
                    FitToImageableArea = true,
                    TemplateScalePercent = 100,
                    TemplateOffsetXMm = 0,
                    TemplateOffsetYMm = 0,
                    CurrentPage = 1,
                    PageSize = 1,
                    IsPrinting = true
                };
                var list = _dataService.GetAllRecipients() ?? new System.Collections.Generic.List<Recipient>();
                vm.Recipients = new ObservableCollection<Recipient>(list);
                vm.LoadPreviewItems();
                var wnd = new FullPreviewWindow { DataContext = vm };
                if (Application.Current != null && Application.Current.MainWindow != wnd) wnd.Owner = Application.Current.MainWindow;
                // делаем модальным, чтобы блокировать основное окно
                wnd.ShowDialog();
            }
            catch { }
        }

        private void UpdateSelectedTemplateProperties()
        {
            if (SelectedTemplate != null)
            {
                SelectedTemplateName = SelectedTemplate.Name;
                SelectedTemplateWidth = SelectedTemplate.EnvelopeWidth;
                SelectedTemplateHeight = SelectedTemplate.EnvelopeHeight;
                // поднять уведомления об изменениях для настроек фона
                OnPropertyChanged(nameof(CanvasBackgroundImagePath));
                OnPropertyChanged(nameof(CanvasBackgroundStretch));
                // unsubscribe old collection
                if (SelectedTemplateItems != null && _itemsCollectionChangedHandler != null) SelectedTemplateItems.CollectionChanged -= _itemsCollectionChangedHandler;
                SelectedTemplateItems = new ObservableCollection<TemplateItemViewModel>(SelectedTemplate.Items.Select(item => new TemplateItemViewModel(item)));
                // subscribe to new collection
                SelectedTemplateItems.CollectionChanged += _itemsCollectionChangedHandler;
                // attach property changed handlers for initial items
                foreach (var it in SelectedTemplateItems) it.PropertyChanged += OnTemplateItemPropertyChanged;
                MarkAllDirty(); RequestValidateAllItemsBounds();
            }
            else { SelectedTemplateItems?.Clear(); }
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
            _isDragging = false; SelectedTemplateItem.PositionX = SelectedTemplateItem.Transform.X; SelectedTemplateItem.PositionY = SelectedTemplateItem.Transform.Y; MarkItemDirty(SelectedTemplateItem); RequestValidateAllItemsBounds();
            // Persist change to DB/storage when user finishes drag
            try { PersistPendingChanges(); } catch { }
        }

        private void OnSelectedTemplateItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (TemplateItemViewModel oldItem in e.OldItems)
                {
                    oldItem.PropertyChanged -= OnTemplateItemPropertyChanged;
                    lock (_dirtyLock) { _dirtyItems.Remove(oldItem); }
                }
            }
            if (e.NewItems != null)
            {
                foreach (TemplateItemViewModel newItem in e.NewItems)
                {
                    newItem.PropertyChanged += OnTemplateItemPropertyChanged;
                    MarkItemDirty(newItem);
                }
            }
        }

        private void OnTemplateItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // track changes that affect bounds/placement
            if (sender is not TemplateItemViewModel item) return;
            switch (e.PropertyName)
            {
                case nameof(TemplateItemViewModel.PositionX):
                case nameof(TemplateItemViewModel.PositionY):
                case nameof(TemplateItemViewModel.Width):
                case nameof(TemplateItemViewModel.Height):
                case nameof(TemplateItemViewModel.RotationDegrees):
                case nameof(TemplateItemViewModel.BorderThickness):
                case nameof(TemplateItemViewModel.Padding):
                case nameof(TemplateItemViewModel.CornerRadius):
                case "": // some changes may send empty property name
                    MarkItemDirty(item);
                    break;
                default:
                    return; // ignore unrelated properties
            }
            // schedule debounced validation
            RequestValidateAllItemsBounds();
        }

        private void MarkItemDirty(TemplateItemViewModel item)
        {
            if (item == null) return;
            lock (_dirtyLock) { _dirtyItems.Add(item); }
        }
        private void MarkAllDirty()
        {
            if (SelectedTemplateItems == null) return;
            lock (_dirtyLock)
            {
                _dirtyItems.Clear();
                foreach (var it in SelectedTemplateItems) _dirtyItems.Add(it);
            }
        }

        // Replace ValidateAllItemsBounds with one that validates only dirty items
        public void ValidateAllItemsBounds()
        {
            // keep compatibility: full validation if no dirty tracking
            if (SelectedTemplateItems == null || SelectedTemplate == null) return;
            foreach (var item in SelectedTemplateItems) item.CheckBounds(SelectedTemplate.EnvelopeWidth, SelectedTemplate.EnvelopeHeight);
        }

        private void ValidateDirtyItemsBounds()
        {
            if (SelectedTemplateItems == null || SelectedTemplate == null) return;
            List<TemplateItemViewModel> toValidate;
            lock (_dirtyLock)
            {
                if (_dirtyItems.Count == 0) return;
                toValidate = _dirtyItems.ToList();
                _dirtyItems.Clear();
            }
            foreach (var item in toValidate)
            {
                // item may have been removed; guard
                if (SelectedTemplateItems.Contains(item)) item.CheckBounds(SelectedTemplate.EnvelopeWidth, SelectedTemplate.EnvelopeHeight);
            }
        }

        public void RequestValidateAllItemsBounds(double debounceMs = DefaultValidateDebounceMs)
        {
            try
            {
                if (_validateTimer == null)
                {
                    _validateTimer = new System.Timers.Timer { AutoReset = false, Interval = debounceMs };
                    _validateTimer.Elapsed += (s, e) =>
                    {
                        _validateTimer?.Stop();
                        var d = Application.Current?.Dispatcher;
                        if (d != null)
                        {
                            d.BeginInvoke(new Action(() => ValidateDirtyItemsBounds()), System.Windows.Threading.DispatcherPriority.Background);
                        }
                        else
                        {
                            ValidateDirtyItemsBounds();
                        }
                    };
                }
                else
                {
                    _validateTimer.Interval = debounceMs;
                }
                _validateTimer.Stop();
                _validateTimer.Start();
            }
            catch { }
        }

        public void CalculateAndSetInitialZoom()
        {
            var viewSize = GetViewSizeRequested?.Invoke(); if (viewSize == null || viewSize.Value.Width <= 0 || viewSize.Value.Height <= 0) return;
            double margin = 100; double availableWidth = viewSize.Value.Width - margin; double availableHeight = viewSize.Value.Height - margin;
            double envelopePxWidth = DisplayWidth; double envelopePxHeight = DisplayHeight; if (envelopePxWidth <= 0 || envelopePxHeight <= 0) return;
            double scaleX = availableWidth / envelopePxWidth; double scaleY = availableHeight / envelopePxHeight; double zoomFactor = Math.Min(1.0, Math.Min(scaleX, scaleY)); ZoomPercentage = (int)(zoomFactor * 100);
        }

        private static string ImportToAssets(string sourcePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return sourcePath;
                var assets = DataService.GetAssetsFolder();
                var name = Path.GetFileName(sourcePath);
                // sanitize and ensure unique
                foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
                var dest = Path.Combine(assets, name);
                if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                {
                    // if exists and different content, add numeric suffix
                    string baseName = Path.GetFileNameWithoutExtension(name);
                    string ext = Path.GetExtension(name);
                    int i = 1;
                    while (File.Exists(dest))
                    {
                        try
                        {
                            var srcLen = new FileInfo(sourcePath).Length;
                            var dstLen = new FileInfo(dest).Length;
                            if (srcLen == dstLen) break; // same size -> probably same file
                        }
                        catch { }
                        dest = Path.Combine(assets, $"{baseName}_{i}{ext}");
                        i++;
                    }
                    try { File.Copy(sourcePath, dest, true); } catch { }
                }
                return dest;
            }
            catch { return sourcePath; }
        }

        private void ChangeImage(object obj)
        {
            if (SelectedTemplateItem == null) return;
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Выбор изображения", Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp" };
            if (dlg.ShowDialog() == true)
            {
                SelectedTemplateItem.ImagePath = ImportToAssets(dlg.FileName);
            }
        }
        private void ChangeBackgroundImage(object obj)
        {
            if (SelectedTemplate == null) return;
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = "Фон конверта", Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp" };
            if (dlg.ShowDialog() == true)
            {
                SelectedTemplate.BackgroundImagePath = ImportToAssets(dlg.FileName);
                OnPropertyChanged(nameof(CanvasBackgroundImagePath));
                _dataService.UpdateTemplate(SelectedTemplate);
            }
        }

        private bool CanMoveUp(TemplateItemViewModel item) => item != null;
        private bool CanMoveDown(TemplateItemViewModel item) => item != null && item.ZIndex > 0;
        private void MoveItemUp(TemplateItemViewModel item) { if (item == null) return; item.ZIndex += 1; }
        private void MoveItemDown(TemplateItemViewModel item) { if (item == null) return; if (item.ZIndex > 0) item.ZIndex -= 1; }
        private void BringToFront(TemplateItemViewModel item) { if (item == null || SelectedTemplateItems == null) return; int max = SelectedTemplateItems.Max(i => i.ZIndex); item.ZIndex = max + 1; }
        private void SendToBack(TemplateItemViewModel item) { if (item == null || SelectedTemplateItems == null) return; int min = SelectedTemplateItems.Min(i => i.ZIndex); item.ZIndex = Math.Max(0, min - 1); }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                // stop and dispose timer
                try { _validateTimer?.Stop(); _validateTimer?.Dispose(); } catch { }
                _validateTimer = null;

                // unsubscribe collection changed
                try
                {
                    if (SelectedTemplateItems != null && _itemsCollectionChangedHandler != null)
                        SelectedTemplateItems.CollectionChanged -= _itemsCollectionChangedHandler;
                }
                catch { }

                // unsubscribe item property changed
                try
                {
                    if (SelectedTemplateItems != null)
                    {
                        foreach (var it in SelectedTemplateItems)
                        {
                            try { it.PropertyChanged -= OnTemplateItemPropertyChanged; } catch { }
                        }
                    }
                }
                catch { }

                // drop references to large data
                try { SelectedTemplateItems = null; } catch { }
                try { Templates = null; } catch { }
                try { SelectedTemplate = null; } catch { }
            }
            catch { }
        }

        // finalizer just in case
        ~TemplateDesignerViewModel() { Dispose(); }
    }
}
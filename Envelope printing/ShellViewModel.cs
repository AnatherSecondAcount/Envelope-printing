using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace Envelope_printing
{
    public class ShellViewModel : INotifyPropertyChanged
    {
        private object _currentView;
        private readonly UIService _uiService = UIService.Instance;

        // Храним экземпляры ViewModel для каждого экрана
        public HomeViewModel HomeVM { get; set; }
        public RecipientEditorViewModel RecipientEditorVM { get; set; }
        public TemplateDesignerViewModel TemplateDesignerVM { get; set; }
        public PrintPreviewViewModel PrintPreviewVM { get; set; }
        public SettingsView SettingsView { get; set; }

        private bool _isLeftPanelExpanded = true;
        public bool IsLeftPanelExpanded
        {
            get => _isLeftPanelExpanded;
            set
            {
                if (_isLeftPanelExpanded == value) return;
                _isLeftPanelExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LeftPanelWidth));

                // ЗАПИСЫВАЕМ СОСТОЯНИЕ В ОБЩИЙ СЕРВИС
                _uiService.IsMainNavExpanded = value;
            }
        }

        private bool _isLeftPanelVisible = true;
        public bool IsLeftPanelVisible
        {
            get => _isLeftPanelVisible;
            set { _isLeftPanelVisible = value; OnPropertyChanged(); }
        }
        public object CurrentView
        {
            get { return _currentView; }
            set { _currentView = value; OnPropertyChanged(); }
        }


        // Команды для навигации
        public ICommand ShowHomeCommand { get; }
        public ICommand ShowRecipientEditorCommand { get; }
        public ICommand ShowTemplateDesignerCommand { get; }
        public ICommand ShowPrintPreviewCommand { get; }
        public ICommand ShowSettingsCommand { get; }
        public ICommand ToggleLeftPanelCommand { get; }
        public ICommand ToggleLeftPanelVisibilityCommand { get; }
        public GridLength LeftPanelWidth => IsLeftPanelExpanded ? new GridLength(220) : new GridLength(60);

        public ShellViewModel()
        {
            HomeVM = new HomeViewModel();
            RecipientEditorVM = new RecipientEditorViewModel();
            TemplateDesignerVM = new TemplateDesignerViewModel();
            PrintPreviewVM = new PrintPreviewViewModel();
            SettingsView = new SettingsView();

            ToggleLeftPanelVisibilityCommand = new RelayCommand(p => IsLeftPanelVisible = !IsLeftPanelVisible);
            ShowHomeCommand = new RelayCommand(p => CurrentView = HomeVM);
            ShowRecipientEditorCommand = new RelayCommand(p => CurrentView = RecipientEditorVM);
            ShowTemplateDesignerCommand = new RelayCommand(p => CurrentView = TemplateDesignerVM);
            ShowPrintPreviewCommand = new RelayCommand(p => CurrentView = PrintPreviewVM);
            ShowSettingsCommand = new RelayCommand(p => CurrentView = SettingsView);
            ToggleLeftPanelCommand = new RelayCommand(p => IsLeftPanelExpanded = !IsLeftPanelExpanded);

            CurrentView = HomeVM;
        }
        public void ShowLeftPanel() => IsLeftPanelVisible = true;
        public void HideLeftPanel() => IsLeftPanelVisible = false;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
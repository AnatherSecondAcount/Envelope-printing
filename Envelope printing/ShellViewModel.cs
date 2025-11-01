using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Reflection;
using System.Net.Http;
using System.Net;
using System.Text.Json;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Controls;

namespace Envelope_printing
{
    public class ShellViewModel : INotifyPropertyChanged
    {
        private object _currentView;
        private readonly UIService _uiService = UIService.Instance;

        // Keep VMs and Views lazily
        public HomeViewModel HomeVM { get; private set; }
        private RecipientEditorViewModel _recipientEditorVM;
        private UserControl _recipientEditorView;
        private TemplateDesignerViewModel _templateDesignerVM;
        private UserControl _templateDesignerView;
        private PrintPreviewViewModel _printPreviewVM;
        private UserControl _printPreviewView;
        public SettingsView SettingsView { get; private set; }

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

        private bool _hasUpdateAvailable;
        public bool HasUpdateAvailable
        {
            get => _hasUpdateAvailable;
            private set { if (_hasUpdateAvailable == value) return; _hasUpdateAvailable = value; OnPropertyChanged(); }
        }

        // Команды для навигации
        public ICommand ShowHomeCommand { get; }
        public ICommand ShowRecipientEditorCommand { get; }
        public ICommand ShowTemplateDesignerCommand { get; }
        public ICommand ShowPrintPreviewCommand { get; }
        public ICommand ShowSettingsCommand { get; }
        public ICommand ToggleLeftPanelCommand { get; }
        public ICommand ToggleLeftPanelVisibilityCommand { get; }
        // Slightly increase collapsed width to keep icon area comfortable
        public GridLength LeftPanelWidth => IsLeftPanelExpanded ? new GridLength(220) : new GridLength(60);

        public ShellViewModel()
        {
            HomeVM = new HomeViewModel();
            // heavy VMs are created lazily when requested
            SettingsView = new SettingsView();

            ToggleLeftPanelVisibilityCommand = new RelayCommand(p => IsLeftPanelVisible = !IsLeftPanelVisible);
            ShowHomeCommand = new RelayCommand(p => NavigateTo(HomeVM));
            ShowRecipientEditorCommand = new RelayCommand(p => ShowRecipientEditor());
            ShowTemplateDesignerCommand = new RelayCommand(p => ShowTemplateDesigner());
            ShowPrintPreviewCommand = new RelayCommand(p => ShowPrintPreview());
            ShowSettingsCommand = new RelayCommand(p => { NavigateTo(SettingsView); HasUpdateAvailable = false; });
            ToggleLeftPanelCommand = new RelayCommand(p => IsLeftPanelExpanded = !IsLeftPanelExpanded);

            CurrentView = HomeVM;

            // Запускаем отложенную проверку обновлений
            _ = StartUpdateCheckDelayed();
        }

        private void ShowRecipientEditor()
        {
            if (_recipientEditorVM == null) _recipientEditorVM = new RecipientEditorViewModel();
            if (_recipientEditorView == null)
            {
                _recipientEditorView = new RecipientEditorView();
                _recipientEditorView.DataContext = _recipientEditorVM;
            }
            NavigateTo(_recipientEditorView);
        }
        private void ShowTemplateDesigner()
        {
            if (_templateDesignerVM == null) _templateDesignerVM = new TemplateDesignerViewModel();
            if (_templateDesignerView == null)
            {
                _templateDesignerView = new Designer.TemplateDesignerView();
                _templateDesignerView.DataContext = _templateDesignerVM;
            }
            NavigateTo(_templateDesignerView);
        }
        private void ShowPrintPreview()
        {
            if (_printPreviewVM == null) _printPreviewVM = new PrintPreviewViewModel();
            if (_printPreviewView == null)
            {
                _printPreviewView = new PrintPreviewView();
                _printPreviewView.DataContext = _printPreviewVM;
            }
            NavigateTo(_printPreviewView);
        }

        private void NavigateTo(object vmOrView)
        {
            var prev = CurrentView;
            if (prev != null && !ReferenceEquals(prev, vmOrView))
            {
                TryDisposeIfTransient(prev);
            }
            CurrentView = vmOrView;
        }

        private void TryDisposeIfTransient(object prev)
        {
            try
            {
                // If previous was a view, and has a DataContext that is a disposable VM, dispose it and clear references
                if (prev is UserControl uc)
                {
                    if (uc.DataContext is TemplateDesignerViewModel tdvm)
                    {
                        tdvm.Dispose();
                        _templateDesignerVM = null;
                        _templateDesignerView = null;
                        return;
                    }
                    if (uc.DataContext is RecipientEditorViewModel revm)
                    {
                        if (revm is IDisposable d1) d1.Dispose();
                        _recipientEditorVM = null;
                        _recipientEditorView = null;
                        return;
                    }
                    if (uc.DataContext is PrintPreviewViewModel ppvm)
                    {
                        if (ppvm is IDisposable d2) d2.Dispose();
                        _printPreviewVM = null;
                        _printPreviewView = null;
                        return;
                    }
                }

                // If previous was a VM-type directly (legacy), handle same as before
                if (prev is TemplateDesignerViewModel tdvm2) { tdvm2.Dispose(); if (ReferenceEquals(_templateDesignerVM, tdvm2)) _templateDesignerVM = null; return; }
                if (prev is RecipientEditorViewModel revm2) { if (revm2 is IDisposable d3) d3.Dispose(); if (ReferenceEquals(_recipientEditorVM, revm2)) _recipientEditorVM = null; return; }
                if (prev is PrintPreviewViewModel ppvm2) { if (ppvm2 is IDisposable d4) d4.Dispose(); if (ReferenceEquals(_printPreviewVM, ppvm2)) _printPreviewVM = null; return; }
            }
            catch { }
        }

        public void ShowLeftPanel() => IsLeftPanelVisible = true;
        public void HideLeftPanel() => IsLeftPanelVisible = false;

        public async Task StartUpdateCheckDelayed(TimeSpan? delay = null)
        {
            try
            {
                await Task.Delay(delay ?? TimeSpan.FromSeconds(20)); // небольшая пауза после старта
                bool newer = await CheckForUpdatesAsync();
                HasUpdateAvailable = newer;
                if (newer)
                {
                    // имитируем нажатие на "Проверить" внутри панели настроек, чтобы показать инфо
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        // Не переключаем экран автоматически, просто обновляем данные в панели, если она уже создана
                        try { await SettingsView.TriggerUpdateCheckAsync(); } catch { }
                    });
                }
            }
            catch { }
        }

        private static Version TryParseVersionFromText(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var clean = s.Trim();
            int plus = clean.IndexOf('+');
            if (plus >0) clean = clean.Substring(0, plus);
            clean = clean.TrimStart('v', 'V');
            if (Version.TryParse(clean, out var v)) return v;
            if (Version.TryParse(clean + ".0", out v)) return v;
            return null;
        }

        private static string GetCurrentVersionString()
        {
            try
            {
                var exe = Assembly.GetExecutingAssembly().Location;
                var pv = FileVersionInfo.GetVersionInfo(exe).ProductVersion;
                if (!string.IsNullOrWhiteSpace(pv))
                {
                    int plus = pv.IndexOf('+');
                    if (plus >0) pv = pv.Substring(0, plus);
                    return pv;
                }
                return Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            }
            catch { return null; }
        }

        private async Task<bool> CheckForUpdatesAsync()
        {
            const string Owner = "AnatherSecondAcount";
            const string Repo = "Envelope-printing";
            using var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("EnvelopePrinter/1.0");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            try
            {
                var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
                using var resp = await http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return false;
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                string name = root.TryGetProperty("name", out var nEl) ? nEl.GetString() : null;
                string tag = root.TryGetProperty("tag_name", out var tEl) ? tEl.GetString() : null;
                var latest = TryParseVersionFromText(name) ?? TryParseVersionFromText(tag);
                var current = TryParseVersionFromText(GetCurrentVersionString());
                if (latest == null || current == null) return false;
                return latest > current;
            }
            catch { return false; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
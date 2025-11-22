using EnvelopePrinter.Core;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Envelope_printing
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            this.Loaded += (s, e) =>
            {
                if (DataContext is SettingsVM vm)
                {
                    vm.EnsureInitialized();
                }
            };
            DataContext = new SettingsVM();
        }

        public Task TriggerUpdateCheckAsync()
        {
            return Updates != null ? Updates.RunCheckAsync() : Task.CompletedTask;
        }
    }

    internal class SettingsVM : INotifyPropertyChanged
    {
        public ObservableCollection<Option> ThemeOptions { get; } = new ObservableCollection<Option>
        {
            new Option("Системная", AppThemePreference.System),
            new Option("Светлая", AppThemePreference.Light),
            new Option("Тёмная", AppThemePreference.Dark)
        };
        private AppThemePreference _themePreference;
        public AppThemePreference ThemePreference
        {
            get => _themePreference;
            set { if (_themePreference == value) return; _themePreference = value; OnPropertyChanged(); try { ThemeManager.ApplyTheme(value); ThemeManager.SavePreference(value); } catch { } }
        }

        public RelayCommand BackupCommand { get; }
        public RelayCommand RestoreCommand { get; }
        private bool _initialized;

        private DateTime? _lastBackupUtc;
        public string LastBackupDisplay => _lastBackupUtc.HasValue ? $"Последняя копия: {_lastBackupUtc.Value.ToLocalTime():yyyy-MM-dd HH:mm}" : "Резервная копия ещё не выполнялась";

        public SettingsVM()
        {
            BackupCommand = new RelayCommand(_ => Backup());
            RestoreCommand = new RelayCommand(_ => Restore());
        }
        public void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            ThemePreference = ThemeManager.LoadPreference();
            LoadLastBackupTime();
        }

        private static string GetBackupMarkerPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "EnvelopePrinter", "last-backup.txt");
        }
        private void LoadLastBackupTime()
        {
            try
            {
                var path = GetBackupMarkerPath();
                if (File.Exists(path))
                {
                    var text = File.ReadAllText(path).Trim();
                    if (DateTime.TryParse(text, out var dt)) _lastBackupUtc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                }
                OnPropertyChanged(nameof(LastBackupDisplay));
            }
            catch { }
        }
        private void SaveLastBackupTime(DateTime utc)
        {
            try
            {
                var path = GetBackupMarkerPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, utc.ToString("o"));
                _lastBackupUtc = utc;
                OnPropertyChanged(nameof(LastBackupDisplay));
            }
            catch { }
        }

        private void Backup()
        {
            var sfd = new SaveFileDialog
            {
                Title = "Сохранить резервную копию как...",
                Filter = "Файл базы данных (*.db)|*.db",
                FileName = $"Резервная копия {DateTime.Now:yyyy-MM-dd_HH-mm-ss}.db"
            };
            if (sfd.ShowDialog() == true)
            {
                try { new DataService().BackupDatabase(sfd.FileName); SaveLastBackupTime(DateTime.UtcNow); MessageBox.Show("Резервная копия успешно создана!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information); }
                catch (Exception ex) { MessageBox.Show($"Ошибка при создании копии: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }
        private void Restore()
        {
            var ofd = new OpenFileDialog
            {
                Title = "Выберите файл резервной копии",
                Filter = "Файл базы данных (*.db)|*.db|Все файлы (*.*)|*.*"
            };
            if (ofd.ShowDialog() == true)
            {
                if (MessageBox.Show("Текущая база будет заменена. Продолжить?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                try { new DataService().RestoreDatabase(ofd.FileName); MessageBox.Show("База данных успешно восстановлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information); }
                catch (Exception ex) { MessageBox.Show($"Ошибка восстановления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public record Option(string Display, AppThemePreference Value);
    }
}

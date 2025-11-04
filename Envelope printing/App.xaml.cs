using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;

namespace Envelope_printing
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private Mutex _singleInstanceMutex;
        private SplashWindow _splash;
        private string _logFilePath;
        private CancellationTokenSource _startupCts;

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Setup global handlers first
            RegisterGlobalExceptionHandlers();

            // Init basic logging very early
            TryEnsureLogFile();
            Log("Startup: begin");

            // Single-instance guard (disabled while debugging or when EP_ALLOW_MULTI=1)
            var disableGuard = Debugger.IsAttached || string.Equals(Environment.GetEnvironmentVariable("EP_ALLOW_MULTI"), "1");
            if (!disableGuard)
            {
                bool created;
                _singleInstanceMutex = new Mutex(true, "EnvelopePrinter.SingleInstance", out created);
                if (!created)
                {
                    Log("Startup: another instance detected, exiting");
                    try { MessageBox.Show("Приложение уже запущено.", "Envelope Printer", MessageBoxButton.OK, MessageBoxImage.Information); } catch { }
                    Shutdown();
                    return;
                }
            }

            base.OnStartup(e);
            TryRegisterAppLogoResource();

            _startupCts = new CancellationTokenSource();

            try
            {
                // Show splash immediately
                _splash = new SplashWindow();
                _splash.Closing += (_, __) => { try { _startupCts.Cancel(); } catch { } };
                _splash.Show();

                // Do heavy init strictly on background thread; support cancellation when user closes splash
                await Task.Run(() => HeavyInitializeSafe(_startupCts.Token));
                if (_startupCts.IsCancellationRequested) { Shutdown(); return; }

                // Create main window only after init completes
                var main = new MainWindow
                {
                    Width = 900,
                    Height = 600
                };
                // Eagerly set VM; other screens remain lazy inside ShellViewModel
                main.DataContext = new ShellViewModel();
                MainWindow = main;
                main.Show();
                Log("Startup: main window shown");
            }
            catch (Exception ex)
            {
                ShowFatalError("Ошибка при запуске приложения", ex);
            }
            finally
            {
                try { _splash?.Close(); } catch { }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _startupCts?.Cancel();
                RunUpdateOnExitIfRequested();
                Log("Exit");
            }
            catch { }
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            base.OnExit(e);
        }

        private void HeavyInitializeSafe(CancellationToken ct)
        {
            try
            {
                if (ct.IsCancellationRequested) return;
                var ds = new EnvelopePrinter.Core.DataService();
                if (ct.IsCancellationRequested) return;
                // Warm-up simple queries (fast); if cancelled, bail out
                ds.GetAllTemplates();
                if (ct.IsCancellationRequested) return;
                ds.GetAllRecipients();
            }
            catch (Exception ex)
            {
                LogException("Ошибка инициализации", ex);
            }
        }

        private void RegisterGlobalExceptionHandlers()
        {
            this.DispatcherUnhandledException += (s, ev) =>
            {
                ev.Handled = true;
                ShowFatalError("Необработанное исключение UI-потока", ev.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                var ex = ev.ExceptionObject as Exception;
                ShowFatalError("Критическая ошибка", ex);
            };
            TaskScheduler.UnobservedTaskException += (s, ev) =>
            {
                ev.SetObserved();
                LogException("Необработанное исключение фоновой задачи", ev.Exception);
            };
        }

        private void ShowFatalError(string message, Exception ex)
        {
            try
            {
                LogException(message, ex);
                // Ensure running on UI thread
                if (this.Dispatcher.CheckAccess())
                {
                    var dlg = new ErrorDialog(message, ex, _logFilePath) { Owner = Current?.MainWindow };
                    dlg.ShowDialog();
                }
                else
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        var dlg = new ErrorDialog(message, ex, _logFilePath) { Owner = Current?.MainWindow };
                        dlg.ShowDialog();
                    });
                }
            }
            catch { }
        }

        private void LogException(string message, Exception ex)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var folder = Path.Combine(appData, "EnvelopePrinter", "logs");
                Directory.CreateDirectory(folder);
                if (string.IsNullOrEmpty(_logFilePath))
                {
                    _logFilePath = Path.Combine(folder, $"app-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                }
                var sb = new StringBuilder();
                sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
                if (ex != null) sb.AppendLine(ex.ToString());
                sb.AppendLine(new string('-', 80));
                File.AppendAllText(_logFilePath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private void TryRegisterAppLogoResource()
        {
            const string key = "AppLogo";
            // Do not replace if already a real ImageSource was put
            if (Resources.Contains(key) && Resources[key] is ImageSource existing && existing is not DrawingImage)
                return;

            ImageSource img = null;
            //1) Try resource stream relative to this assembly
            img = TryLoadFromResourceStream("Resources/AppLogo.png");

            //2) Try pack URI with detected assembly short name
            if (img == null)
            {
                try
                {
                    var asmName = typeof(App).Assembly.GetName().Name;
                    var packUri = new Uri($"pack://application:,,,/{asmName};component/Resources/AppLogo.png", UriKind.Absolute);
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = packUri;
                    bi.EndInit();
                    bi.Freeze();
                    img = bi;
                }
                catch { }
            }

            //3) Fallback to root pack URI
            if (img == null)
            {
                try
                {
                    var packUri = new Uri("pack://application:,,,/Resources/AppLogo.png", UriKind.Absolute);
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = packUri;
                    bi.EndInit();
                    bi.Freeze();
                    img = bi;
                }
                catch { }
            }

            if (img != null)
            {
                Resources[key] = img;
            }
        }

        private ImageSource TryLoadFromResourceStream(string relativePath)
        {
            try
            {
                var uri = new Uri(relativePath, UriKind.Relative);
                var streamInfo = GetResourceStream(uri);
                if (streamInfo?.Stream == null) return null;
                using var ms = new MemoryStream();
                streamInfo.Stream.CopyTo(ms);
                ms.Position = 0;
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = ms;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch { return null; }
        }

        private static string GetUpdateOnExitMarkerPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "EnvelopePrinter", "update-on-exit.json");
        }

        private static bool NeedsElevation()
        {
            try
            {
                var dir = AppContext.BaseDirectory;
                var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                return dir.StartsWith(pf, StringComparison.OrdinalIgnoreCase) || dir.StartsWith(pf86, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static void RunUpdateOnExitIfRequested()
        {
            try
            {
                var markerPath = GetUpdateOnExitMarkerPath();
                if (!File.Exists(markerPath)) return;
                var json = File.ReadAllText(markerPath, Encoding.UTF8);
                var marker = JsonSerializer.Deserialize<UpdateOnExitInfo>(json);
                File.Delete(markerPath);
                if (marker == null) return;

                // MSI branch
                if (marker.IsMsi && File.Exists(marker.DownloadedFile))
                {
                    var psiMsi = new ProcessStartInfo
                    {
                        FileName = "msiexec",
                        Arguments = $"/i \"{marker.DownloadedFile}\"",
                        UseShellExecute = true,
                        Verb = NeedsElevation() ? "runas" : string.Empty
                    };
                    Process.Start(psiMsi);
                    return;
                }

                // ZIP branch
                var sourceDir = marker.SourceDir;
                if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir)) return;
                var targetDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                var appExe = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(targetDir, "Envelope printing.exe");
                WriteAndRunUpdaterScript(sourceDir, targetDir, appExe, marker.Restart);
            }
            catch { }
        }

        private static void WriteAndRunUpdaterScript(string sourceDir, string targetDir, string appExe, bool restart)
        {
            var temp = Path.Combine(Path.GetTempPath(), $"EnvelopeUpdater_{Guid.NewGuid():N}.cmd");
            var pid = Environment.ProcessId;
            var restartArg = restart ? "restart" : string.Empty;
            // robocopy returns codes <8 as success
            var script = $@"@echo off
setlocal enableextensions
set SRC=""{sourceDir}""
set DST=""{targetDir}""
set EXE=""{appExe}""
set PID={pid}

echo Waiting for process %PID% to exit...
:wait
for /f ""tokens=2 delims=,"" %%a in ('tasklist /FI ""PID eq %PID%"" /FO CSV /NH') do (
 if ""%%~a""=="""" goto cont
)
timeout /t1 /nobreak >nul
goto wait
:cont

robocopy %SRC% %DST% /E /R:2 /W:2 /NFL /NDL /NP /NJH /NJS
set RC=%ERRORLEVEL%
if %RC% GEQ8 (
 echo Robocopy failed with code %RC%
 exit /b %RC%
)

if ""{restartArg}""==""restart"" (
 start """" %EXE%
)
exit /b0
";
            File.WriteAllText(temp, script, Encoding.ASCII);
            var psi = new ProcessStartInfo
            {
                FileName = temp,
                UseShellExecute = true,
                Verb = NeedsElevation() ? "runas" : string.Empty,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi);
        }

        private void TryEnsureLogFile()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var folder = Path.Combine(appData, "EnvelopePrinter", "logs");
                Directory.CreateDirectory(folder);
                _logFilePath ??= Path.Combine(folder, $"app-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            }
            catch { }
        }
        private void Log(string line)
        {
            try
            {
                if (string.IsNullOrEmpty(_logFilePath)) return;
                File.AppendAllText(_logFilePath, $"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}", Encoding.UTF8);
            }
            catch { }
        }

        private sealed class UpdateOnExitInfo
        {
            public string SourceDir { get; set; }
            public string DownloadedFile { get; set; }
            public bool Restart { get; set; }
            public bool IsMsi { get; set; }
        }
    }
}

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

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Setup global handlers first
            RegisterGlobalExceptionHandlers();

            // Single-instance guard
            bool created;
            _singleInstanceMutex = new Mutex(true, "EnvelopePrinter.SingleInstance", out created);
            if (!created)
            {
                Shutdown();
                return;
            }

            base.OnStartup(e);
            TryRegisterAppLogoResource();

            try
            {
                // Show splash immediately
                _splash = new SplashWindow();
                _splash.Show();

                // Do heavy initialization on background thread (with own guard)
                await Task.Run(() => HeavyInitializeSafe());

                // Create and show main window on UI thread
                var main = new MainWindow
                {
                    Width = 900,
                    Height = 600
                };
                main.Show();
            }
            catch (Exception ex)
            {
                ShowFatalError("Ошибка при запуске приложения", ex);
                // do not rethrow to avoid crash without dialog
            }
            finally
            {
                try { _splash?.Close(); } catch { }
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

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                RunUpdateOnExitIfRequested();
            }
            catch { }
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            base.OnExit(e);
        }

        private void HeavyInitializeSafe()
        {
            try
            {
                // Place any long-running startup tasks here. Example: warm-up DB services.
                var ds = new EnvelopePrinter.Core.DataService();
                ds.GetAllTemplates();
                ds.GetAllRecipients();
            }
            catch (Exception ex)
            {
                LogException("Ошибка инициализации", ex);
            }
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

        private sealed class UpdateOnExitInfo
        {
            public string SourceDir { get; set; }
            public string DownloadedFile { get; set; }
            public bool Restart { get; set; }
            public bool IsMsi { get; set; }
        }
    }
}

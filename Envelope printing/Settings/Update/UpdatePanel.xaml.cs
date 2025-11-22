using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Envelope_printing
{
    public partial class UpdatePanel : UserControl
    {
        private const string Owner = "AnatherSecondAcount";
        private const string Repo = "Envelope-printing";

        private CancellationTokenSource _cts;
        private string _downloadedFile;
        private bool _downloadedIsZip;
        private string _latestTag;
        private string _lastOpenFolder;
        private string _extractedDir;
        private string _releaseBody;
        private readonly HttpClient _http;
        private bool _versionInitialized;
        private bool _suppressAutoCheck;

        public UpdatePanel()
        {
            InitializeComponent();
            this.Loaded += UpdatePanel_Loaded; // defer heavy / fragile logic

            var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
            _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("EnvelopePrinter", "0")); // real version set after init
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitHubCopilot", "1.0"));
            _http.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
            var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrWhiteSpace(token))
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        private void UpdatePanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (_versionInitialized) return;
            _versionInitialized = true;
            try
            {
                string? pv = null;
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(exePath)) exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
                {
                    try { pv = FileVersionInfo.GetVersionInfo(exePath).ProductVersion; } catch { }
                }
                if (string.IsNullOrWhiteSpace(pv))
                {
                    var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                    pv = asm?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                       ?? asm?.GetName().Version?.ToString();
                }
                var displayPv = SanitizeDisplayVersion(pv) ?? "-";
                CurrentVersionText.Text = displayPv;
                // update UA header version value
                try
                {
                    _http.DefaultRequestHeaders.UserAgent.Remove(_http.DefaultRequestHeaders.UserAgent.First(h => h.Product.Name == "EnvelopePrinter"));
                    _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("EnvelopePrinter", displayPv));
                }
                catch { }
            }
            catch { CurrentVersionText.Text = "-"; }
            finally
            {
                this.Loaded -= UpdatePanel_Loaded;
            }
        }

        // публичный запуск проверки
        public Task RunCheckAsync()
        {
            if (_suppressAutoCheck) return Task.CompletedTask;
            return PerformCheckAsync();
        }

        private static string SanitizeDisplayVersion(string pv)
        {
            if (string.IsNullOrWhiteSpace(pv)) return null;
            // Отрезаем метаданные "+..."
            int plus = pv.IndexOf('+');
            if (plus > 0) pv = pv.Substring(0, plus);
            // Без суффиксов -ci/-githash и т.п.
            return pv.Trim();
        }

        private async Task<HttpResponseMessage> GetWithAuthFallbackAsync(string url)
        {
            var resp = await _http.GetAsync(url);
            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                try { resp.Dispose(); } catch { }
                _http.DefaultRequestHeaders.Authorization = null;
                resp = await _http.GetAsync(url);
            }
            return resp;
        }

        private async Task<string> GetTextWithAuthFallbackAsync(string url)
        {
            using var resp = await GetWithAuthFallbackAsync(url);
            resp.EnsureSuccessStatusCode();
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            // попытка угадать кодировку; GitHub отдаёт UTF-8
            var enc = Encoding.UTF8;
            return enc.GetString(bytes);
        }

        private async void CheckButton_Click(object sender, RoutedEventArgs e)
        {
            _suppressAutoCheck = true; // пользователь сам инициировал
            await PerformCheckAsync();
        }

        private async Task PerformCheckAsync()
        {
            try
            {
                CheckButton.IsEnabled = false;
                StatusText.Text = "Поиск обновлений...";
                var release = await GetLatestReleaseDetailsAsync();
                _latestTag = release.Tag;

                if (string.IsNullOrEmpty(release.DownloadUrl))
                {
                    StatusText.Text = "Нет релизов для загрузки.";
                    DownloadButton.Visibility = Visibility.Collapsed;
                    HideUpdateInfo();
                    return;
                }

                // сравниваем по имени/тегу
                var cur = TryParseVersionFromText(CurrentVersionText.Text);
                var latestComparable = TryParseVersionFromText(release.Name) ?? TryParseVersionFromText(release.Tag);
                if (cur != null && latestComparable != null && latestComparable <= cur)
                {
                    StatusText.Text = "У вас последняя версия.";
                    DownloadButton.Visibility = Visibility.Collapsed;
                    ShowUpdateInfo(release, isNewer: false);
                }
                else
                {
                    StatusText.Text = "Доступно обновление.";
                    DownloadButton.Visibility = Visibility.Visible;
                    ShowUpdateInfo(release, isNewer: true);
                }
            }
            catch (HttpRequestException httpEx)
            {
                StatusText.Text = httpEx.StatusCode == HttpStatusCode.NotFound ? "Релиз не найден" : "Ошибка при запросе";
                MessageBox.Show(httpEx.Message, "Обновление", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка проверки";
                MessageBox.Show(ex.Message, "Обновление", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { CheckButton.IsEnabled = true; }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DownloadButton.IsEnabled = false;
                _cts = new CancellationTokenSource();
                StatusText.Text = "Загрузка...";
                DownloadProgress.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Visible;
                var release = await GetLatestReleaseAsync();
                if (string.IsNullOrEmpty(release.downloadUrl)) { StatusText.Text = "Файл не найден"; return; }
                var updateDir = GetUpdateFolder();
                Directory.CreateDirectory(updateDir);
                var targetPath = Path.Combine(updateDir, release.assetName ?? ($"asset-{DateTimeOffset.Now:yyyyMMddHHmmss}"));
                _downloadedFile = targetPath;
                _downloadedIsZip = release.assetName?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true;
                _lastOpenFolder = updateDir;
                await DownloadWithProgressAsync(release.downloadUrl, targetPath, _cts.Token);
                DownloadProgress.Value = 100;
                StatusText.Text = "Завершено";
                if (_downloadedIsZip)
                {
                    try
                    {
                        var extractDirName = !string.IsNullOrWhiteSpace(_latestTag) ? _latestTag.TrimStart('v', 'V') : Path.GetFileNameWithoutExtension(targetPath);
                        var extractDir = Path.Combine(updateDir, "Extracted", extractDirName);
                        Directory.CreateDirectory(extractDir);
                        ZipFile.ExtractToDirectory(targetPath, extractDir, overwriteFiles: true);
                        _extractedDir = extractDir;
                        _lastOpenFolder = extractDir;
                        StatusText.Text = $"Распаковано в {extractDir}";
                    }
                    catch (Exception unzipEx)
                    {
                        StatusText.Text = $"Ошибка распаковки (можно открыть вручную: {unzipEx.Message})";
                        _extractedDir = null;
                    }
                }
                else
                {
                    _extractedDir = null;
                    _lastOpenFolder = Path.GetDirectoryName(targetPath);
                }

                PostDownloadButtons.Visibility = Visibility.Visible;
                OpenFolderButton.Visibility = Visibility.Visible;
            }
            catch (OperationCanceledException)
            {
                // Пользователь отменил — скрываем прогресс и чистим частичный файл
                try { if (!string.IsNullOrEmpty(_downloadedFile) && File.Exists(_downloadedFile)) File.Delete(_downloadedFile); } catch { }
                StatusText.Text = "Отменено";
                DownloadProgress.Value = 0;
                DownloadProgress.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Collapsed;
                PostDownloadButtons.Visibility = Visibility.Collapsed;
                OpenFolderButton.Visibility = Visibility.Collapsed;
            }
            catch (HttpRequestException httpEx)
            {
                StatusText.Text = httpEx.StatusCode == HttpStatusCode.NotFound ? "Файл не найден" : "Ошибка загрузки";
                MessageBox.Show(httpEx.Message, "Обновление", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка загрузки";
                MessageBox.Show(ex.Message, "Обновление", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { DownloadButton.IsEnabled = true; CancelButton.Visibility = Visibility.Collapsed; _cts?.Dispose(); _cts = null; }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            // Мгновенно скрываем индикатор (окончательная очистка произойдет в catch)
            DownloadProgress.Visibility = Visibility.Collapsed;
            CancelButton.Visibility = Visibility.Collapsed;
        }
        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dir = !string.IsNullOrWhiteSpace(_lastOpenFolder) && Directory.Exists(_lastOpenFolder) ? _lastOpenFolder : GetUpdateFolder();
                if (Directory.Exists(dir)) Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
            }
            catch { }
        }
        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_downloadedFile) || !File.Exists(_downloadedFile)) { StatusText.Text = "Файл отсутствует"; return; }
                if (Path.GetExtension(_downloadedFile).Equals(".msi", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(new ProcessStartInfo { FileName = "msiexec", Arguments = $"/i \"{_downloadedFile}\"", UseShellExecute = true });
                }
                else
                {
                    var folder = !string.IsNullOrWhiteSpace(_lastOpenFolder) && Directory.Exists(_lastOpenFolder) ? _lastOpenFolder : Path.GetDirectoryName(_downloadedFile);
                    Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Установка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnUpdateNow_Click(object sender, RoutedEventArgs e) => TryRunUpdater(restart: false);
        private void BtnUpdateAndReopen_Click(object sender, RoutedEventArgs e) => TryRunUpdater(restart: true);
        private void BtnUpdateOnExit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!EnsureDownloaded()) return;
                var marker = new UpdateOnExitInfo
                {
                    SourceDir = _extractedDir,
                    DownloadedFile = _downloadedFile,
                    Restart = true,
                    IsMsi = string.Equals(Path.GetExtension(_downloadedFile), ".msi", StringComparison.OrdinalIgnoreCase)
                };
                var markerPath = GetUpdateOnExitMarkerPath();
                Directory.CreateDirectory(Path.GetDirectoryName(markerPath)!);
                File.WriteAllText(markerPath, JsonSerializer.Serialize(marker));
                StatusText.Text = "Обновление будет установлено при закрытии приложения.";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Обновление при выходе", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void BtnSkip_Click(object sender, RoutedEventArgs e) => PostDownloadButtons.Visibility = Visibility.Collapsed;

        private bool EnsureDownloaded()
        {
            if (string.IsNullOrEmpty(_downloadedFile) || !File.Exists(_downloadedFile))
            {
                MessageBox.Show("Сначала скачайте обновление.", "Обновление", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            return true;
        }

        private void TryRunUpdater(bool restart)
        {
            try
            {
                if (!EnsureDownloaded()) return;

                if (string.Equals(Path.GetExtension(_downloadedFile), ".msi", StringComparison.OrdinalIgnoreCase))
                {
                    var psiMsi = new ProcessStartInfo
                    {
                        FileName = "msiexec",
                        Arguments = $"/i \"{_downloadedFile}\"",
                        UseShellExecute = true,
                        Verb = NeedsElevation() ? "runas" : string.Empty
                    };
                    Process.Start(psiMsi);
                    Application.Current.Shutdown();
                    return;
                }

                var sourceDir = _extractedDir;
                if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
                {
                    OpenFolderButton_Click(this, new RoutedEventArgs());
                    return;
                }
                var targetDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
                var appExe = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(targetDir, "Envelope printing.exe");
                WriteAndRunUpdaterScript(sourceDir, targetDir, appExe, restart);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Запуск обновления", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void WriteAndRunUpdaterScript(string sourceDir, string targetDir, string appExe, bool restart)
        {
            var temp = Path.Combine(Path.GetTempPath(), $"EnvelopeUpdater_{Guid.NewGuid():N}.cmd");
            var pid = Environment.ProcessId;
            var restartArg = restart ? "restart" : string.Empty;
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

        private string GetUpdateFolder()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "EnvelopePrinter", "Updates");
        }
        private static string GetUpdateOnExitMarkerPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "EnvelopePrinter", "update-on-exit.json");
        }

        private static Version TryParseVersionFromText(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var clean = s.Trim().TrimStart('v', 'V');
            if (Version.TryParse(clean, out var ver)) return ver;
            if (Version.TryParse(clean + ".0", out ver)) return ver;
            return null;
        }

        private async Task<(string tag, string assetName, string downloadUrl)> GetLatestReleaseAsync()
        {
            var apiUrl = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            using var resp = await GetWithAuthFallbackAsync(apiUrl);
            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                return (null, null, null);
            }
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                var msg = await resp.Content.ReadAsStringAsync();
                throw new HttpRequestException($"GitHub API403: {msg}", null, HttpStatusCode.Forbidden);
            }
            resp.EnsureSuccessStatusCode();
            await using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;
            string tagName = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;
            string assetName = null; string downloadUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                string msiUrl = null, msiName = null; string zipUrl = null, zipName = null; string anyUrl = null, anyName = null;
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var nEl) ? nEl.GetString() : null;
                    var url = a.TryGetProperty("browser_download_url", out var uEl) ? uEl.GetString() : null;
                    if (string.IsNullOrEmpty(url)) continue;
                    anyName ??= name; anyUrl ??= url;
                    if (name?.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) == true) { msiName = name; msiUrl = url; }
                    else if (name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true) { zipName = name; zipUrl = url; }
                }
                if (!string.IsNullOrEmpty(msiUrl)) { assetName = msiName; downloadUrl = msiUrl; }
                else if (!string.IsNullOrEmpty(zipUrl)) { assetName = zipName; downloadUrl = zipUrl; }
                else if (!string.IsNullOrEmpty(anyUrl)) { assetName = anyName; downloadUrl = anyUrl; }
            }
            return (tagName, assetName, downloadUrl);
        }

        private static bool LooksLikeChangelog(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            var n = name.ToLowerInvariant();
            if (!(n.EndsWith(".txt") || n.EndsWith(".md") || n.EndsWith(".markdown") || n.EndsWith(".htm") || n.EndsWith(".html"))) return false;
            return n.Contains("changelog") || n.Contains("changes") || n.Contains("release-notes") || n.Contains("releasenotes") || n.Contains("what's new") || n.Contains("whatsnew") || n.Contains("readme");
        }

        private async Task<ReleaseInfo> GetLatestReleaseDetailsAsync()
        {
            var apiUrl = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            using var resp = await GetWithAuthFallbackAsync(apiUrl);
            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                return new ReleaseInfo();
            }
            if (resp.StatusCode == HttpStatusCode.Forbidden)
            {
                var msg = await resp.Content.ReadAsStringAsync();
                throw new HttpRequestException($"GitHub API403: {msg}", null, HttpStatusCode.Forbidden);
            }
            resp.EnsureSuccessStatusCode();
            await using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;
            var info = new ReleaseInfo
            {
                Tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null,
                Name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null,
                Body = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() : null
            };
            string changelogUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                string msiUrl = null, msiName = null; string zipUrl = null, zipName = null; string anyUrl = null, anyName = null;
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var nEl) ? nEl.GetString() : null;
                    var url = a.TryGetProperty("browser_download_url", out var uEl) ? uEl.GetString() : null;
                    if (string.IsNullOrEmpty(url)) continue;
                    anyName ??= name; anyUrl ??= url;
                    if (LooksLikeChangelog(name)) changelogUrl ??= url;
                    if (name?.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) == true) { msiName = name; msiUrl = url; }
                    else if (name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true) { zipName = name; zipUrl = url; }
                }
                if (!string.IsNullOrEmpty(msiUrl)) { info.AssetName = msiName; info.DownloadUrl = msiUrl; }
                else if (!string.IsNullOrEmpty(zipUrl)) { info.AssetName = zipName; info.DownloadUrl = zipUrl; }
                else if (!string.IsNullOrEmpty(anyUrl)) { info.AssetName = anyName; info.DownloadUrl = anyUrl; }
            }

            // если есть соседний файл-лог, используем его содержимое вместо Body
            if (!string.IsNullOrEmpty(changelogUrl))
            {
                try
                {
                    var text = await GetTextWithAuthFallbackAsync(changelogUrl);
                    // простая очистка HTML, если это html – оставим как есть, TextBlock сам покажет текст
                    info.Body = string.IsNullOrWhiteSpace(text) ? info.Body : text.Trim();
                }
                catch { /*fallback к Body*/ }
            }
            return info;
        }

        private async void ShowUpdateInfo(ReleaseInfo release, bool isNewer)
        {
            try
            {
                var versionLabel = !string.IsNullOrWhiteSpace(release.Name) ? release.Name : release.Tag;
                HeadlineText.Text = isNewer ? "Доступно обновление" : "Последняя версия";
                HeadlineVersionText.Text = string.IsNullOrWhiteSpace(versionLabel) ? string.Empty : versionLabel;
                _releaseBody = release.Body;
                ChangelogText.Text = string.IsNullOrWhiteSpace(_releaseBody) ? "" : _releaseBody.Trim();

                UpdateInfoPanel.Visibility = Visibility.Visible;
                var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250)) { EasingFunction = new QuadraticEase() };
                UpdateInfoPanel.BeginAnimation(OpacityProperty, anim);
            }
            catch { }
        }

        private void HideUpdateInfo()
        {
            UpdateInfoPanel.Visibility = Visibility.Collapsed;
            UpdateInfoPanel.Opacity = 0;
            HeadlineText.Text = "";
            HeadlineVersionText.Text = "";
            ChangelogText.Text = string.Empty;
        }

        private async Task<ImageSource?> TryLoadHeroImageAsync(string tag)
        {
            // Опционально — сейчас не обязательно, баннер отрисовывается градиентом
            return await Task.FromResult<ImageSource?>(null);
        }

        private async Task DownloadWithProgressAsync(string url, string filePath, CancellationToken token)
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? -1L;
            await using var input = await resp.Content.ReadAsStreamAsync(token);
            await using var output = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920];
            long read = 0; int n;
            while ((n = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, n), token);
                read += n;
                if (total > 0)
                {
                    var percent = (int)(read * 100 / total);
                    DownloadProgress.Value = percent;
                }
            }
            DownloadProgress.Value = 100;
        }

        private sealed class ReleaseInfo
        {
            public string Tag { get; set; }
            public string Name { get; set; }
            public string AssetName { get; set; }
            public string DownloadUrl { get; set; }
            public string Body { get; set; }
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

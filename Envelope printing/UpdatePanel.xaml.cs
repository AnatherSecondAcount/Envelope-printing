using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

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
        private readonly HttpClient _http;

        public UpdatePanel()
        {
            InitializeComponent();

            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "-";
            CurrentVersionText.Text = currentVersion;

            var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
            _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("EnvelopePrinter", currentVersion));
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GitHubCopilot", "1.0"));
            var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrWhiteSpace(token))
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        private async void CheckButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CheckButton.IsEnabled = false;
                StatusText.Text = "Проверка релизов...";
                var release = await GetLatestReleaseAsync();
                _latestTag = release.tag;
                LatestVersionText.Text = release.tag ?? "-";
                if (string.IsNullOrEmpty(release.downloadUrl))
                {
                    StatusText.Text = "В релизе нет файлов для скачивания (только исходники).";
                    DownloadButton.Visibility = Visibility.Collapsed;
                    return;
                }
                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                var latestComparable = TryParseVersionFromTag(release.tag);
                if (currentVersion != null && latestComparable != null && latestComparable <= currentVersion)
                {
                    StatusText.Text = "У вас последняя версия";
                    DownloadButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    var type = release.assetName?.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) == true ? "MSI" : (release.assetName?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true ? "ZIP" : "файл");
                    StatusText.Text = $"Доступно обновление ({type})";
                    DownloadButton.Visibility = Visibility.Visible;
                }
            }
            catch (HttpRequestException httpEx)
            {
                // Common:404 when there are no releases yet
                StatusText.Text = httpEx.StatusCode == HttpStatusCode.NotFound ? "Релизы отсутствуют" : "Ошибка при проверке";
                MessageBox.Show(httpEx.Message, "Обновление", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка при проверке";
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
                DownloadProgress.Value =100;
                StatusText.Text = "Загружено";
                if (_downloadedIsZip)
                {
                    try
                    {
                        var extractDirName = !string.IsNullOrWhiteSpace(_latestTag) ? _latestTag.TrimStart('v', 'V') : Path.GetFileNameWithoutExtension(targetPath);
                        var extractDir = Path.Combine(updateDir, "Extracted", extractDirName);
                        Directory.CreateDirectory(extractDir);
                        ZipFile.ExtractToDirectory(targetPath, extractDir, overwriteFiles: true);
                        _lastOpenFolder = extractDir;
                        StatusText.Text = $"Распаковано в {extractDir}";
                        InstallButton.Visibility = Visibility.Collapsed;
                        OpenFolderButton.Visibility = Visibility.Visible;
                    }
                    catch (Exception unzipEx)
                    {
                        StatusText.Text = $"Архив загружен (ошибка распаковки: {unzipEx.Message})";
                        InstallButton.Visibility = Visibility.Collapsed;
                        OpenFolderButton.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    _lastOpenFolder = Path.GetDirectoryName(targetPath);
                    InstallButton.Visibility = Visibility.Visible;
                    OpenFolderButton.Visibility = Visibility.Visible;
                }
            }
            catch (OperationCanceledException) { StatusText.Text = "Отменено"; }
            catch (HttpRequestException httpEx)
            {
                StatusText.Text = httpEx.StatusCode == HttpStatusCode.NotFound ? "Релиз не найден" : "Ошибка загрузки";
                MessageBox.Show(httpEx.Message, "Обновление", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка загрузки";
                MessageBox.Show(ex.Message, "Обновление", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { DownloadButton.IsEnabled = true; CancelButton.Visibility = Visibility.Collapsed; }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();
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

        private string GetUpdateFolder()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "EnvelopePrinter", "Updates");
        }
        private static Version TryParseVersionFromTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            var clean = tag.Trim().TrimStart('v', 'V');
            if (Version.TryParse(clean, out var ver)) return ver;
            if (Version.TryParse(clean + ".0", out ver)) return ver;
            return null;
        }

        private async Task<(string tag, string assetName, string downloadUrl)> GetLatestReleaseAsync()
        {
            var apiUrl = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            using var resp = await _http.GetAsync(apiUrl);
            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                // No releases yet
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
                string msiUrl = null, msiName = null; string zipUrl = null, zipName = null;
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var nEl) ? nEl.GetString() : null;
                    var url = a.TryGetProperty("browser_download_url", out var uEl) ? uEl.GetString() : null;
                    if (string.IsNullOrEmpty(url)) continue;
                    if (name?.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) == true) { msiName = name; msiUrl = url; }
                    else if (name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true) { zipName = name; zipUrl = url; }
                }
                if (!string.IsNullOrEmpty(msiUrl)) { assetName = msiName; downloadUrl = msiUrl; }
                else if (!string.IsNullOrEmpty(zipUrl)) { assetName = zipName; downloadUrl = zipUrl; }
                else
                {
                    foreach (var a in assets.EnumerateArray())
                    {
                        var name = a.TryGetProperty("name", out var nEl) ? nEl.GetString() : null;
                        var url = a.TryGetProperty("browser_download_url", out var uEl) ? uEl.GetString() : null;
                        if (!string.IsNullOrEmpty(url)) { assetName = name; downloadUrl = url; break; }
                    }
                }
            }
            return (tagName, assetName, downloadUrl);
        }

        private async Task DownloadWithProgressAsync(string url, string filePath, CancellationToken token)
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? -1L;
            await using var input = await resp.Content.ReadAsStreamAsync(token);
            await using var output = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920];
            long read =0; int n;
            while ((n = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), token)) >0)
            {
                await output.WriteAsync(buffer.AsMemory(0, n), token);
                read += n;
                if (total >0)
                {
                    var percent = (int)(read *100 / total);
                    DownloadProgress.Value = percent;
                }
            }
            DownloadProgress.Value =100;
        }
    }
}

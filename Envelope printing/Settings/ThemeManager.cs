using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;

namespace Envelope_printing
{
    public enum AppThemePreference
    {
        System,
        Light,
        Dark
    }

    internal static class ThemeManager
    {
        private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EnvelopePrinter", "settings.json");

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, uint attr, ref int attrValue, int attrSize);
        private const uint DWMWA_USE_IMMERSIVE_DARK_MODE = 20; // Windows101809+

        public static AppThemePreference LoadPreference()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var obj = JsonSerializer.Deserialize<SettingsDto>(json);
                    if (Enum.TryParse<AppThemePreference>(obj?.Theme, true, out var pref))
                        return pref;
                }
            }
            catch { }
            return AppThemePreference.System;
        }

        public static void SavePreference(AppThemePreference pref)
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(new SettingsDto { Theme = pref.ToString() }, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }

        public static void ApplyTheme(AppThemePreference pref)
        {
            bool dark = IsDarkEffective(pref);
            var app = Application.Current;
            if (app?.Resources == null) return;
            var merged = app.Resources.MergedDictionaries;
            app.Dispatcher.Invoke(() =>
            {
                // Ensure base light at [0]
                if (merged.Count == 0 || merged[0].Source == null || !merged[0].Source.OriginalString.Contains("MinimalisticStyles"))
                {
                    if (merged.Count == 0) merged.Add(new ResourceDictionary());
                    merged[0] = new ResourceDictionary { Source = new Uri("pack://application:,,,/Styles/MinimalisticStyles.xaml", UriKind.Absolute) };
                }
                while (merged.Count < 2) merged.Add(new ResourceDictionary());
                // Replace slot[1] entirely
                var newDict = dark
                    ? new ResourceDictionary { Source = new Uri("pack://application:,,,/Styles/DarkTheme.xaml", UriKind.Absolute) }
                    : new ResourceDictionary();
                merged[1] = newDict;
                // bump stamp
                app.Resources["__ThemeStamp"] = DateTime.Now.Ticks;
                foreach (Window w in app.Windows)
                {
                    try { w.Resources["__WindowThemeStamp"] = DateTime.Now.Ticks; } catch { }
                }
                try
                {
                    var win = app.MainWindow;
                    if (win != null) TryApplySystemTitleBarTheme(win, dark);
                }
                catch { }
            });
        }

        public static bool IsDarkEffective(AppThemePreference pref)
        {
            return pref switch
            {
                AppThemePreference.Dark => true,
                AppThemePreference.Light => false,
                _ => !DetectSystemAppsUseLightTheme()
            };
        }

        public static void ApplySystemTitleBarForCurrentPreference(Window window)
        {
            if (window == null) return;
            var dark = IsDarkEffective(LoadPreference());
            TryApplySystemTitleBarTheme(window, dark);
        }

        private static void TryApplySystemTitleBarTheme(Window window, bool dark)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;
                int useDark = dark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
            }
            catch { }
        }

        private static bool DetectSystemAppsUseLightTheme()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
                if (key == null) return true;
                var val = key.GetValue("AppsUseLightTheme");
                if (val is int i) return i != 0;
            }
            catch { }
            return true; // default to light
        }

        private sealed class SettingsDto
        {
            public string Theme { get; set; }
        }
    }
}

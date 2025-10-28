using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Envelope_printing
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            TryRegisterAppLogoResource();
            // Show splash immediately
            var splash = new SplashWindow();
            splash.Show();

            // Do heavy initialization on background thread
            await Task.Run(() => HeavyInitialize());

            // Create and show main window on UI thread
            var main = new MainWindow();
            // Optionally set initial size before show
            main.Width = 900;
            main.Height = 600;
            main.Show();
            // Close splash
            splash.Close();
        }

        private void HeavyInitialize()
        {
            // Place any long-running startup tasks here. Example: warm-up DB services.
            try
            {
                // Force load of services to JIT them in background
                var ds = new EnvelopePrinter.Core.DataService();
                ds.GetAllTemplates();
                ds.GetAllRecipients();
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
    }
}

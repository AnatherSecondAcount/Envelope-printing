using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace Envelope_printing
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Simulate/init heavy startup: create VM, load initial data
            await Task.Delay(100); // tiny delay to show splash
            var vm = new ShellViewModel();
            DataContext = vm;

            // Enlarge window to working size with animation and show app layer
            var toWidth = 900.0;
            var toHeight = 600.0;
            var duration = new Duration(System.TimeSpan.FromMilliseconds(260));
            var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var widthAnim = new DoubleAnimation { To = toWidth, Duration = duration, EasingFunction = easing };
            var heightAnim = new DoubleAnimation { To = toHeight, Duration = duration, EasingFunction = easing };
            BeginAnimation(Window.WidthProperty, widthAnim);
            BeginAnimation(Window.HeightProperty, heightAnim);

            // Fade splash out, show app
            var fadeOut = new DoubleAnimation(1, 0, System.TimeSpan.FromMilliseconds(200)) { EasingFunction = easing };
            fadeOut.Completed += (_, __) => { SplashGrid.Visibility = Visibility.Collapsed; AppRoot.Visibility = Visibility.Visible; };
            SplashGrid.BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}
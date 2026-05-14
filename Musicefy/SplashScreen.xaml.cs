using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Musicefy
{
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
            Loaded += StartSpotifyStyleLoading;
        }

        private async void StartSpotifyStyleLoading(object sender, RoutedEventArgs e)
        {
            // 1. Smooth Fade In
            var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(800)));
            BeginAnimation(Window.OpacityProperty, fadeIn);

            // 2. Rotate the minimal loader
            var rotateAnim = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(TimeSpan.FromSeconds(1)),
                RepeatBehavior = RepeatBehavior.Forever
            };
            LoaderRotate.BeginAnimation(RotateTransform.AngleProperty, rotateAnim);

            // 3. Animate Progress Bar (0 to 100)
            var progressAnim = new DoubleAnimation(0, 100, new Duration(TimeSpan.FromSeconds(3.5)))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut }
            };
            BottomProgress.BeginAnimation(System.Windows.Controls.ProgressBar.ValueProperty, progressAnim);

            // Wait for "Loading" to finish
            await Task.Delay(4000);

            // 4. Spotify-style Exit (Scale up and Fade out)
            var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(500)));
            
            fadeOut.Completed += (s, ev) =>
            {
                var main = new MainWindow();
                main.Show();
                this.Close();
            };

            BeginAnimation(Window.OpacityProperty, fadeOut);
        }
    }
}

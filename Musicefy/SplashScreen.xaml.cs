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
            Loaded += StartCinematicLoadingSequence;
        }

        /// <summary>
        /// Premium App Initialization Pipeline
        /// Orchestrates layered acceleration curves for an immersive entry and exit sequence.
        /// </summary>
        private async void StartCinematicLoadingSequence(object sender, RoutedEventArgs e)
        {
            // 1. Cinematic Entry Transformation Stage (Fade In + Spatial Zoom Tracking Combination)
            var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(750)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var scaleIn = new DoubleAnimation(0.95, 1.0, new Duration(TimeSpan.FromMilliseconds(1000)))
            {
                EasingFunction = new QuinticEase { EasingMode = EasingMode.EaseOut }
            };

            BeginAnimation(Window.OpacityProperty, fadeIn);
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn);

            // 2. Continuous Organic Logo Accent Pulse Behavior Definition Loop
            var pulseAnim = new DoubleAnimation(1.0, 1.04, new Duration(TimeSpan.FromMilliseconds(1400)))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            LogoScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulseAnim);
            LogoScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulseAnim);

            // 3. Fluid Progress Bar Simulation Load Pipeline Execution Curve
            var progressAnim = new DoubleAnimation(0, 100, new Duration(TimeSpan.FromSeconds(2.8)))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut }
            };
            BottomProgress.BeginAnimation(System.Windows.Controls.ProgressBar.ValueProperty, progressAnim);

            // Maintain spatial layout pause to simulate underlying system resource indexing
            await Task.Delay(3200);

            // 4. Elegant Window Exit Phase (Scale Outwards while expanding transparency vectors)
            var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(450)))
            {
                EasingFunction = new PowerEase { Power = 3, EasingMode = EasingMode.EaseIn }
            };
            var scaleOut = new DoubleAnimation(1.0, 1.03, new Duration(TimeSpan.FromMilliseconds(450)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, ev) =>
            {
                var main = new MainWindow();
                main.Show();
                this.Close();
            };

            BeginAnimation(Window.OpacityProperty, fadeOut);
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOut);
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleOut);
        }
    }
}

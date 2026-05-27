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
            ApplySavedThemeToSplashScreen();
            Loaded += StartCinematicLoadingSequence;
        }

        /// <summary>
        /// Reads user preferences file directly on boot initialization and updates window colors to match.
        /// </summary>
        private void ApplySavedThemeToSplashScreen()
        {
            try
            {
                // 1. Fetch preferences directly from configuration file layers
                string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";
                bool isPureBlack = Musicefy.Properties.Settings.Default.PureBlackMode;

                var parts = savedTheme.Split('|');
                string mode = parts.Length > 0 ? parts[0] : "Dark";
                string palette = parts.Length > 1 ? parts[1] : "Default";

                // 2. Resolve target background card brush states
                Color backgroundCardColor = Color.FromRgb(18, 18, 18); // Default Dark fallback comfort gray
                Color primaryTextColor = Colors.White;
                Color secondaryTextColor = Color.FromRgb(106, 106, 106);
                Color trackRailColor = Color.FromRgb(30, 30, 30);

                if (mode.Equals("Light", StringComparison.OrdinalIgnoreCase))
                {
                    backgroundCardColor = Color.FromRgb(245, 245, 245); // Pure soft light grey card surface
                    primaryTextColor = Color.FromRgb(20, 20, 20); // Deep graphite text focus
                    secondaryTextColor = Color.FromRgb(140, 140, 140);
                    trackRailColor = Color.FromRgb(220, 220, 220);
                }
                else if (isPureBlack || mode.Equals("DarkPure", StringComparison.OrdinalIgnoreCase))
                {
                    backgroundCardColor = Colors.Black; // Absolute OLED deep ink black frame canvas
                    trackRailColor = Color.FromRgb(20, 20, 20);
                }

                // Apply surface colors
                MainRootBorder.Background = new SolidColorBrush(backgroundCardColor);
                TxtBrandTitle.Foreground = new SolidColorBrush(primaryTextColor);
                TxtSubMarker.Foreground = new SolidColorBrush(secondaryTextColor);
                ProgressBarTrack.Background = new SolidColorBrush(trackRailColor);

                // 3. Resolve dynamic accent colors matching user palettes
                Color accentThemeColor = Color.FromRgb(29, 185, 84); // Default Spotify Green fallback token

                switch (palette.ToLower())
                {
                    case "catppuccin":
                        accentThemeColor = Color.FromRgb(245, 194, 231); // Catppuccin Pink
                        break;
                    case "greenapple":
                        accentThemeColor = Color.FromRgb(139, 195, 74); // Vibrant Lime Green
                        break;
                    case "lavender":
                        accentThemeColor = Color.FromRgb(183, 189, 248); // Soothing Lavender Blue
                        break;
                }

                // Inject dynamic values straight into vector glows and progress bar indicators
                LogoGlow.Color = accentThemeColor;
                BottomProgress.Foreground = new SolidColorBrush(accentThemeColor);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SplashScreen] Theme application failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Premium App Initialization Pipeline Animation Sequences
        /// </summary>
        private async void StartCinematicLoadingSequence(object sender, RoutedEventArgs e)
        {
            try
            {
            // Cinematic Entry Fade + Spatial Zoom Transitions
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

            // Continuous Organic Logo Accent Pulse loop
            var pulseAnim = new DoubleAnimation(1.0, 1.04, new Duration(TimeSpan.FromMilliseconds(1400)))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            LogoScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulseAnim);
            LogoScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulseAnim);

            // Progress Bar simulation sweep curve 
            var progressAnim = new DoubleAnimation(0, 100, new Duration(TimeSpan.FromSeconds(2.8)))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseInOut }
            };
            BottomProgress.BeginAnimation(System.Windows.Controls.ProgressBar.ValueProperty, progressAnim);

            await Task.Delay(3200);

            // Elegant Window Exit Scale Outwards
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
                var main = new Musicefy.MainWindow();
                main.Show();
                this.Close();
            };

            BeginAnimation(Window.OpacityProperty, fadeOut);
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOut);
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleOut);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SplashScreen] Animation error: {ex.Message}");
                var main = new Musicefy.MainWindow();
                main.Show();
                this.Close();
            }
        }
    }
}

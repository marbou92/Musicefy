using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Musicefy.Core.Theme;

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
        /// Reads user preferences directly on boot and updates splash screen colors
        /// using the new Aniyomi-style AppTheme + ThemeMode model.
        /// </summary>
        private void ApplySavedThemeToSplashScreen()
        {
            try
            {
                // Load preferences using the new model
                var (appTheme, themeMode) = Musicefy.Services.ThemeManager.LoadPreferences();

                bool isDark = themeMode switch
                {
                    ThemeMode.Light  => false,
                    ThemeMode.Dark   => true,
                    ThemeMode.Amoled => true,
                    _                => Musicefy.Services.ThemeManager.IsSystemDarkMode(),
                };

                bool isAmoled = themeMode == ThemeMode.Amoled;

                // Resolve target background card brush states
                Color backgroundCardColor = Color.FromRgb(18, 18, 18); // Default Dark fallback
                Color primaryTextColor = Colors.White;
                Color secondaryTextColor = Color.FromRgb(106, 106, 106);
                Color trackRailColor = Color.FromRgb(30, 30, 30);

                if (!isDark)
                {
                    backgroundCardColor = Color.FromRgb(245, 245, 245);
                    primaryTextColor = Color.FromRgb(20, 20, 20);
                    secondaryTextColor = Color.FromRgb(140, 140, 140);
                    trackRailColor = Color.FromRgb(220, 220, 220);
                }
                else if (isAmoled)
                {
                    backgroundCardColor = Colors.Black;
                    trackRailColor = Color.FromRgb(20, 20, 20);
                }

                // Apply surface colors
                MainRootBorder.Background = new SolidColorBrush(backgroundCardColor);
                TxtBrandTitle.Foreground = new SolidColorBrush(primaryTextColor);
                TxtSubMarker.Foreground = new SolidColorBrush(secondaryTextColor);
                ProgressBarTrack.Background = new SolidColorBrush(trackRailColor);

                // Resolve accent color from the AppTheme's palette
                var scheme = AppThemeColorSchemes.GetColorScheme(appTheme, themeMode,
                    Musicefy.Services.ThemeManager.IsSystemDarkMode());
                Color accentThemeColor = scheme.Primary;

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
                // Must set MainWindow BEFORE closing the splash screen.
                // Otherwise ShutdownMode="OnMainWindowClose" sees the
                // splash close and shuts down the app instantly.
                Application.Current.MainWindow = main;
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
                Application.Current.MainWindow = main;
                main.Show();
                this.Close();
            }
        }
    }
}

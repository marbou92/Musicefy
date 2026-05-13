using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects; // <-- Needed for DropShadowEffect
using System.Windows.Shapes;        // <-- For Rectangle

namespace Musicefy
{
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
            Loaded += async (s, e) =>
            {
                // Fade in
                var fadeIn = new DoubleAnimation(0, 1, new Duration(System.TimeSpan.FromSeconds(1)));
                BeginAnimation(Window.OpacityProperty, fadeIn);

                // Animate equalizer bars
                AnimateBar(Bar1, 0.5);
                AnimateBar(Bar2, 0.7);
                AnimateBar(Bar3, 0.6);
                AnimateBar(Bar4, 0.8);
                AnimateBar(Bar5, 0.5);

                // Animate gradient background
                AnimateGradient(GradientStop1, "#1E1E1E", "#3B82F6", 6);
                AnimateGradient(GradientStop2, "#00D4FF", "#9333EA", 6);

                // Animate progress ring
                AnimateRing();
                AnimateRingGlow();

                // Simulate loading
                await Task.Delay(4000);

                // Fade out
                var fadeOut = new DoubleAnimation(1, 0, new Duration(System.TimeSpan.FromSeconds(1)));
                fadeOut.Completed += (s2, e2) =>
                {
                    var main = new MainWindow();
                    main.Show();
                    Close();
                };
                BeginAnimation(Window.OpacityProperty, fadeOut);
            };
        }

        private void AnimateBar(Rectangle bar, double speed)
        {
            var anim = new DoubleAnimationUsingKeyFrames
            {
                RepeatBehavior = RepeatBehavior.Forever,
                AutoReverse = true
            };
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(20, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(0))));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(80, KeyTime.FromTimeSpan(System.TimeSpan.FromSeconds(speed))));
            bar.BeginAnimation(Rectangle.HeightProperty, anim);
        }

        private void AnimateGradient(GradientStop stop, string fromColor, string toColor, double duration)
        {
            var anim = new ColorAnimation
            {
                From = (Color)ColorConverter.ConvertFromString(fromColor),
                To = (Color)ColorConverter.ConvertFromString(toColor),
                Duration = new Duration(System.TimeSpan.FromSeconds(duration)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            stop.BeginAnimation(GradientStop.ColorProperty, anim);
        }

        private void AnimateRing()
        {
            var rotateAnim = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(System.TimeSpan.FromSeconds(2)),
                RepeatBehavior = RepeatBehavior.Forever
            };
            RingRotate.BeginAnimation(RotateTransform.AngleProperty, rotateAnim);
        }

        private void AnimateRingGlow()
        {
            var glowAnim = new DoubleAnimation
            {
                From = 0.3,
                To = 0.8,
                Duration = new Duration(System.TimeSpan.FromSeconds(1.5)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };

            if (Ring.Effect is DropShadowEffect shadow)
            {
                shadow.BeginAnimation(DropShadowEffect.OpacityProperty, glowAnim);
            }
        }
    }
}

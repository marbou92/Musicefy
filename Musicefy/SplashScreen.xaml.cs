using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

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

                // Simulate loading
                await Task.Delay(2000);

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
    }
}

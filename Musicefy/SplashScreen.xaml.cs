using System.Threading.Tasks;
using System.Windows;

namespace Musicefy
{
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
            Loaded += async (s, e) =>
            {
                await Task.Delay(2000); // simulate loading
                var main = new MainWindow();
                main.Show();
                Close();
            };
        }
    }
}

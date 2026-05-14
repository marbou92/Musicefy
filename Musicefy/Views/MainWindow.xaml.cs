using System.Windows;
using Musicefy.Views;

namespace Musicefy
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Default landing page
            MainContent.Content = new HomeControl();
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new HomeControl();
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new SearchControl();
        }

        private void Library_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new LibraryControl();
        }

        private void NowPlaying_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new NowPlayingControl();
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow { Owner = this };
            win.ShowDialog();
        }
    }
}

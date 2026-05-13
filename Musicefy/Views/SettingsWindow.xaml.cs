using System.Windows;

namespace Musicefy.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            ShowAppearance(); // default view
        }

        private void AppearanceButton_Click(object sender, RoutedEventArgs e)
        {
            ShowAppearance();
        }

        private void DownloadsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowDownloads();
        }

        private void ShowAppearance()
        {
            SettingsContent.Content = new AppearanceSettingsControl();
        }

        private void ShowDownloads()
        {
            SettingsContent.Content = new DownloadsSettingsControl();
        }
    }
}

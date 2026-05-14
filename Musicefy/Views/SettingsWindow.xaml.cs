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
            // Show theme selector group
            SettingsContent.Content = new Musicefy.Controls.ThemeSelectorGroup
            {
                DataContext = new Musicefy.ViewModels.AppearanceSettingsViewModel()
            };
        }

        private void ShowDownloads()
        {
            SettingsContent.Content = new DownloadsSettingsControl();
        }
    }
}

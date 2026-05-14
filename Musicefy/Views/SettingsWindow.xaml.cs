using System.Windows;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class SettingsWindow : Window
    {
        private AppearanceSettingsViewModel _appearanceVM;

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
            _appearanceVM = new AppearanceSettingsViewModel();
            SettingsContent.Content = new AppearanceSettingsControl
            {
                DataContext = _appearanceVM
            };
            SectionTitle.Text = "Appearance Settings";
        }

        private void ShowDownloads()
        {
            SettingsContent.Content = new DownloadsSettingsControl();
            SectionTitle.Text = "Downloads Settings";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsContent.Content is AppearanceSettingsControl && _appearanceVM != null)
            {
                _appearanceVM.Save();
                MessageBox.Show("Appearance settings saved.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (SettingsContent.Content is DownloadsSettingsControl downloadsControl)
            {
                // trigger save logic in DownloadsSettingsControl
                downloadsControl.GetType().GetMethod("Save_Click")?
                    .Invoke(downloadsControl, new object[] { sender, e });
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsContent.Content is AppearanceSettingsControl && _appearanceVM != null)
            {
                _appearanceVM.Cancel();
                MessageBox.Show("Appearance changes reverted.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (SettingsContent.Content is DownloadsSettingsControl downloadsControl)
            {
                downloadsControl.GetType().GetMethod("Cancel_Click")?
                    .Invoke(downloadsControl, new object[] { sender, e });
            }
        }
    }
}

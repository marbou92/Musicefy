using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
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
            AppearanceButton.IsChecked = true;
            DownloadsButton.IsChecked = false;
            ShowAppearance();
        }

        private void DownloadsButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadsButton.IsChecked = true;
            AppearanceButton.IsChecked = false;
            ShowDownloads();
        }

        private void ShowAppearance()
        {
            _appearanceVM = new AppearanceSettingsViewModel();
            var control = new AppearanceSettingsControl
            {
                DataContext = _appearanceVM
            };

            AnimateContentChange(control, "Appearance Settings");
        }

        private void ShowDownloads()
        {
            var control = new DownloadsSettingsControl();
            AnimateContentChange(control, "Downloads Settings");
        }

        /// <summary>
        /// Handles fade animation when swapping content.
        /// </summary>
        private void AnimateContentChange(UserControl newContent, string title)
        {
            // Fade out current content
            var fadeOut = new DoubleAnimation(1, 0, new Duration(System.TimeSpan.FromMilliseconds(200)));
            fadeOut.Completed += (s, e) =>
            {
                SettingsContent.Content = newContent;
                SectionTitle.Text = title;

                // Fade in new content
                var fadeIn = new DoubleAnimation(0, 1, new Duration(System.TimeSpan.FromMilliseconds(200)));
                newContent.BeginAnimation(OpacityProperty, fadeIn);
            };

            if (SettingsContent.Content is FrameworkElement currentContent)
            {
                currentContent.BeginAnimation(OpacityProperty, fadeOut);
            }
            else
            {
                SettingsContent.Content = newContent;
                SectionTitle.Text = title;
                var fadeIn = new DoubleAnimation(0, 1, new Duration(System.TimeSpan.FromMilliseconds(200)));
                newContent.BeginAnimation(OpacityProperty, fadeIn);
            }
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

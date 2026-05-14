using System;
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

            AnimateContentChange(control, "Appearance Settings", fromRight: false);
        }

        private void ShowDownloads()
        {
            var control = new DownloadsSettingsControl();
            AnimateContentChange(control, "Downloads Settings", fromRight: true);
        }

        /// <summary>
        /// Handles fade + slide animation when swapping content.
        /// </summary>
        private void AnimateContentChange(UserControl newContent, string title, bool fromRight)
        {
            if (SettingsContent.Content is FrameworkElement currentContent)
            {
                // Fade out + slide left
                var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(200)));
                var slideOut = new ThicknessAnimation
                {
                    From = new Thickness(0),
                    To = new Thickness(fromRight ? -50 : 50, 0, 0, 0),
                    Duration = new Duration(TimeSpan.FromMilliseconds(200))
                };

                fadeOut.Completed += (s, e) =>
                {
                    SettingsContent.Content = newContent;
                    SectionTitle.Text = title;

                    // Fade in + slide in
                    newContent.Opacity = 0;
                    newContent.Margin = new Thickness(fromRight ? 50 : -50, 0, 0, 0);

                    var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200)));
                    var slideIn = new ThicknessAnimation
                    {
                        From = newContent.Margin,
                        To = new Thickness(0),
                        Duration = new Duration(TimeSpan.FromMilliseconds(200))
                    };

                    newContent.BeginAnimation(OpacityProperty, fadeIn);
                    newContent.BeginAnimation(MarginProperty, slideIn);
                };

                currentContent.BeginAnimation(OpacityProperty, fadeOut);
                currentContent.BeginAnimation(MarginProperty, slideOut);
            }
            else
            {
                SettingsContent.Content = newContent;
                SectionTitle.Text = title;

                // Initial fade/slide in
                newContent.Opacity = 0;
                newContent.Margin = new Thickness(fromRight ? 50 : -50, 0, 0, 0);

                var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200)));
                var slideIn = new ThicknessAnimation
                {
                    From = newContent.Margin,
                    To = new Thickness(0),
                    Duration = new Duration(TimeSpan.FromMilliseconds(200))
                };

                newContent.BeginAnimation(OpacityProperty, fadeIn);
                newContent.BeginAnimation(MarginProperty, slideIn);
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

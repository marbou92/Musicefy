using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Musicefy.ViewModels;
using Musicefy.Core.Interfaces;

namespace Musicefy.Views
{
    public partial class SettingsWindow : Window
    {
        private AppearanceSettingsViewModel _appearanceVM;

        public SettingsWindow()
        {
            InitializeComponent();
            ShowAppearance(); // Mount primary surface view context on initialization pass
        }

        private void AppearanceButton_Click(object sender, RoutedEventArgs e)
        {
            ShowAppearance();
        }

        private void DownloadsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowDownloads();
        }

        private void SourcesButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSources();
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

        private void ShowSources()
        {
            var control = new SourcesSettingsControl();
            AnimateContentChange(control, "Sources Settings", fromRight: true);
        }

        private void AnimateContentChange(UserControl newContent, string title, bool fromRight)
        {
            if (SettingsContent.Content is FrameworkElement currentContent)
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
                var slideOut = new ThicknessAnimation
                {
                    From = new Thickness(0),
                    To = new Thickness(fromRight ? -40 : 40, 0, 0, 0),
                    Duration = TimeSpan.FromMilliseconds(180)
                };

                fadeOut.Completed += (s, e) =>
                {
                    SettingsContent.Content = newContent;
                    SectionTitle.Text = title;

                    newContent.Opacity = 0;
                    newContent.Margin = new Thickness(fromRight ? 40 : -40, 0, 0, 0);

                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
                    var slideIn = new ThicknessAnimation
                    {
                        From = newContent.Margin,
                        To = new Thickness(0),
                        Duration = TimeSpan.FromMilliseconds(180)
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

                newContent.Opacity = 0;
                newContent.Margin = new Thickness(fromRight ? 40 : -40, 0, 0, 0);

                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
                var slideIn = new ThicknessAnimation
                {
                    From = newContent.Margin,
                    To = new Thickness(0),
                    Duration = TimeSpan.FromMilliseconds(180)
                };

                newContent.BeginAnimation(OpacityProperty, fadeIn);
                newContent.BeginAnimation(MarginProperty, slideIn);
            }
        }

        // FIXED: Using type checking contract interfaces instead of slow reflection lookups
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsContent.Content is ISettingsControl settingsControl)
            {
                settingsControl.Save();
                MessageBox.Show("Settings configuration modified safely.", "Musicefy Core Engine", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (SettingsContent.Content is AppearanceSettingsControl && _appearanceVM != null)
            {
                _appearanceVM.Save();
                MessageBox.Show("Appearance settings saved.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsContent.Content is ISettingsControl settingsControl)
            {
                settingsControl.Cancel();
                MessageBox.Show("Pending adjustment vectors abandoned.", "Musicefy Core Engine", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else if (SettingsContent.Content is AppearanceSettingsControl && _appearanceVM != null)
            {
                _appearanceVM.Cancel();
                MessageBox.Show("Appearance changes reverted.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}

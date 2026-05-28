using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class SettingsPage : UserControl
    {
        private AppearanceSettingsViewModel _appearanceVM;

        public SettingsPage()
        {
            InitializeComponent();
            ShowAppearance();
        }

        private void AppearanceButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (AppearanceButton.IsChecked == true) ShowAppearance();
        }

        private void DownloadsButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DownloadsButton.IsChecked == true) ShowDownloads();
        }

        private void SourcesButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (SourcesButton.IsChecked == true) ShowSources();
        }

        private void RepositoriesButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (RepositoriesButton.IsChecked == true) ShowRepositories();
        }

        private void DiscoverButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DiscoverButton.IsChecked == true) ShowDiscover();
        }

        private void ExtensionsButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (ExtensionsButton.IsChecked == true) ShowExtensions();
        }

        private void ShowAppearance()
        {
            _appearanceVM = App.Services.GetService<AppearanceSettingsViewModel>();
            AnimateContentChange(new AppearanceSettingsControl { DataContext = _appearanceVM }, "Appearance Settings", fromRight: false);
        }

        private void ShowDownloads()
        {
            var vm = App.Services.GetService<DownloadsSettingsViewModel>();
            AnimateContentChange(new DownloadsSettingsControl { DataContext = vm }, "Downloads Settings", fromRight: true);
        }

        private void ShowSources()
        {
            AnimateContentChange(new SourcesSettingsControl(), "Sources Settings", fromRight: true);
        }

        private void ShowDiscover()
        {
            AnimateContentChange(new DiscoverSettingsControl(), "Discover", fromRight: true);
        }

        private void ShowRepositories()
        {
            var vm = App.Services.GetService<RepositoriesSettingsViewModel>();
            AnimateContentChange(new RepositoriesSettingsControl { DataContext = vm }, "Extension Repositories", fromRight: true);
        }

        private void ShowExtensions()
        {
            var vm = App.Services.GetService<ExtensionsSettingsViewModel>();
            AnimateContentChange(new ExtensionsSettingsControl { DataContext = vm }, "Extensions", fromRight: true);
        }

        private void AnimateContentChange(UserControl newContent, string title, bool fromRight)
        {
            if (SettingsContent == null || SectionTitle == null)
                return;

            void BeginInwardAnimation()
            {
                SettingsContent.Content = newContent;
                SectionTitle.Text = title;

                newContent.Opacity = 0;
                newContent.Margin = new System.Windows.Thickness(fromRight ? 40 : -40, 0, 0, 0);

                var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(180)));
                var slideIn = new ThicknessAnimation
                {
                    From = newContent.Margin,
                    To = new System.Windows.Thickness(0),
                    Duration = TimeSpan.FromMilliseconds(180)
                };

                fadeIn.Completed += (sender, ev) => this.UpdateLayout();

                newContent.BeginAnimation(OpacityProperty, fadeIn);
                newContent.BeginAnimation(MarginProperty, slideIn);
            }

            if (SettingsContent.Content is FrameworkElement currentContent)
            {
                var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(180)));
                var slideOut = new ThicknessAnimation
                {
                    From = new System.Windows.Thickness(0),
                    To = new System.Windows.Thickness(fromRight ? -40 : 40, 0, 0, 0),
                    Duration = TimeSpan.FromMilliseconds(180)
                };

                fadeOut.Completed += (s, e) => BeginInwardAnimation();

                currentContent.BeginAnimation(OpacityProperty, fadeOut);
                currentContent.BeginAnimation(MarginProperty, slideOut);
            }
            else
            {
                BeginInwardAnimation();
            }
        }
    }
}

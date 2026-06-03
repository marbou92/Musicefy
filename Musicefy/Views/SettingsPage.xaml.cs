using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Interfaces;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class SettingsPage : UserControl
    {
        private AppearanceSettingsViewModel _appearanceVM;
        private bool _initialLoadPending = true;

        public SettingsPage()
        {
            InitializeComponent();
            this.Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_initialLoadPending)
            {
                _initialLoadPending = false;
                // FIX: Only show content AFTER the control is in the visual tree.
                // Previously, ShowAppearance() was called in the constructor, which set
                // Opacity=0 on the content and started a fade-in animation. But animations
                // don't run on elements that aren't in the visual tree yet, so the content
                // stayed permanently invisible (Opacity=0). The Loaded event fires after
                // the element is added to the visual tree, so animations work here.
                ShowAppearance();
            }
        }

        private void AppearanceButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (AppearanceButton.IsChecked == true && SettingsContent != null)
            {
                ShowAppearance();
            }
        }

        private void DownloadsButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DownloadsButton.IsChecked == true && SettingsContent != null)
            {
                ShowDownloads();
            }
        }

        private void SourcesButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (SourcesButton.IsChecked == true && SettingsContent != null)
            {
                ShowSources();
            }
        }

        private void RepositoriesButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (RepositoriesButton.IsChecked == true && SettingsContent != null)
            {
                ShowRepositories();
            }
        }

        private void DiscoverButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DiscoverButton.IsChecked == true && SettingsContent != null)
            {
                ShowDiscover();
            }
        }

        private void ExtensionsButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (ExtensionsButton.IsChecked == true && SettingsContent != null)
            {
                ShowExtensions();
            }
        }

        private void ShowAppearance()
        {
            try
            {
                _appearanceVM = App.Services.GetService<AppearanceSettingsViewModel>();
                var control = new AppearanceSettingsControl();
                if (_appearanceVM != null)
                    control.DataContext = _appearanceVM;
                AnimateContentChange(control, "Appearance Settings", fromRight: false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] ShowAppearance failed: {ex.Message}");
                ShowErrorContent("Appearance Settings", ex.Message);
            }
        }

        private void ShowDownloads()
        {
            try
            {
                var vm = App.Services.GetService<DownloadsSettingsViewModel>();
                var control = new DownloadsSettingsControl();
                if (vm != null)
                    control.DataContext = vm;
                AnimateContentChange(control, "Downloads Settings", fromRight: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] ShowDownloads failed: {ex.Message}");
                ShowErrorContent("Downloads Settings", ex.Message);
            }
        }

        private void ShowSources()
        {
            // FIX: Previously, SourcesSettingsControl was created without a ViewModel,
            // leaving DataContext null and the Sources list empty. Now we resolve the
            // SourcesSettingsViewModel from DI and call Initialize() to wire up the
            // data bindings (Sources list, PropertyChanged, empty state logic).
            try
            {
                var vm = App.Services.GetService<SourcesSettingsViewModel>();
                var control = new SourcesSettingsControl();
                if (vm != null)
                    control.Initialize(vm);
                AnimateContentChange(control, "Sources Settings", fromRight: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] ShowSources failed: {ex.Message}");
                ShowErrorContent("Sources Settings", ex.Message);
            }
        }

        private void ShowDiscover()
        {
            try
            {
                var control = new DiscoverSettingsControl();
                AnimateContentChange(control, "Discover", fromRight: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] ShowDiscover failed: {ex.Message}");
                ShowErrorContent("Discover", ex.Message);
            }
        }

        private void ShowRepositories()
        {
            try
            {
                var vm = App.Services.GetService<RepositoriesSettingsViewModel>();
                var control = new RepositoriesSettingsControl();
                if (vm != null)
                    control.DataContext = vm;
                AnimateContentChange(control, "Extension Repositories", fromRight: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] ShowRepositories failed: {ex.Message}");
                ShowErrorContent("Extension Repositories", ex.Message);
            }
        }

        private void ShowExtensions()
        {
            try
            {
                var control = new ExtensionsSettingsControl();
                AnimateContentChange(control, "Extensions", fromRight: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] ShowExtensions failed: {ex.Message}");
                ShowErrorContent("Extensions", ex.Message);
            }
        }

        /// <summary>
        /// Fallback error content when a settings section fails to load.
        /// Ensures the user always sees something instead of a blank page.
        /// </summary>
        private void ShowErrorContent(string title, string errorMessage)
        {
            if (SettingsContent == null || SectionTitle == null) return;

            SectionTitle.Text = title;
            var errorPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var errorText = new TextBlock
            {
                Text = $"Failed to load settings section.\n{errorMessage}",
                FontSize = 14,
                Foreground = System.Windows.Media.Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            errorPanel.Children.Add(errorText);
            SettingsContent.Content = errorPanel;
        }

        private void AnimateContentChange(UserControl newContent, string title, bool fromRight)
        {
            // Persist whatever the outgoing panel last set
            if (SettingsContent?.Content is ISettingsControl outgoing)
                outgoing.Save();

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

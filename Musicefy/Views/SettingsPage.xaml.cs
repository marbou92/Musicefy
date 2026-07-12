using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;          // ← ADD THIS
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

            // Initialize the default content IMMEDIATELY in the constructor
            // instead of waiting for the Loaded event. This ensures content
            // is visible even if Loaded fires late or not at all.
            try
            {
                ShowAppearance();
                _initialLoadPending = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Constructor init failed: {ex.Message}");
                // Fallback: set a placeholder so the page isn't blank
                ShowFallbackContent($"Error loading Appearance: {ex.Message}");
            }
        }

        private void SettingsPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // Only show appearance if the constructor didn't already do it
            if (_initialLoadPending)
            {
                _initialLoadPending = false;
                try
                {
                    ShowAppearance();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Loaded init failed: {ex.Message}");
                    ShowFallbackContent($"Error loading Appearance: {ex.Message}");
                }
            }
        }

        private void AppearanceButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_initialLoadPending) return;
            if (AppearanceButton.IsChecked == true)
            {
                try { ShowAppearance(); }
                catch (Exception ex) { ShowFallbackContent($"Error: {ex.Message}"); }
            }
        }

        private void DownloadsButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DownloadsButton.IsChecked == true)
            {
                try { ShowDownloads(); }
                catch (Exception ex) { ShowFallbackContent($"Error: {ex.Message}"); }
            }
        }

        private void SourcesButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (SourcesButton.IsChecked == true)
            {
                try { ShowSources(); }
                catch (Exception ex) { ShowFallbackContent($"Error: {ex.Message}"); }
            }
        }

        private void RepositoriesButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (RepositoriesButton.IsChecked == true)
            {
                try { ShowRepositories(); }
                catch (Exception ex) { ShowFallbackContent($"Error: {ex.Message}"); }
            }
        }

        private void DiscoverButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DiscoverButton.IsChecked == true)
            {
                try { ShowDiscover(); }
                catch (Exception ex) { ShowFallbackContent($"Error: {ex.Message}"); }
            }
        }

        private void ExtensionsButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (ExtensionsButton.IsChecked == true)
            {
                try { ShowExtensions(); }
                catch (Exception ex) { ShowFallbackContent($"Error: {ex.Message}"); }
            }
        }

        private void ShowAppearance()
        {
            _appearanceVM = App.Services.GetService<AppearanceSettingsViewModel>();
            var control = new AppearanceSettingsControl();
            if (_appearanceVM != null)
                control.DataContext = _appearanceVM;
            AnimateContentChange(control, "Appearance Settings", fromRight: false);
        }

        private void ShowDownloads()
        {
            var vm = App.Services.GetService<DownloadsSettingsViewModel>();
            var control = new DownloadsSettingsControl();
            if (vm != null)
                control.DataContext = vm;
            AnimateContentChange(control, "Downloads Settings", fromRight: true);
        }

        private void ShowSources()
        {
            var vm = App.Services.GetService<SourcesSettingsViewModel>();
            var control = new SourcesSettingsControl();
            if (vm != null)
            {
                // Re-load providers each time the Sources tab is shown so the dialog
                // reflects the latest install/uninstall state of extensions.
                vm.RefreshProviders();
                control.Initialize(vm);
            }
            AnimateContentChange(control, "Sources Settings", fromRight: true);
        }

        private void ShowDiscover()
        {
            AnimateContentChange(new DiscoverSettingsControl(), "Discover", fromRight: true);
        }

        private void ShowRepositories()
        {
            var vm = App.Services.GetService<RepositoriesSettingsViewModel>();
            var control = new RepositoriesSettingsControl();
            if (vm != null)
                control.DataContext = vm;
            AnimateContentChange(control, "Extension Repositories", fromRight: true);
        }

        private void ShowExtensions()
        {
            var vm = App.Services.GetService<ExtensionsSettingsViewModel>();
            var control = new ExtensionsSettingsControl();
            if (vm != null)
                control.DataContext = vm;
            AnimateContentChange(control, "Extensions", fromRight: true);
        }

        private void ShowFallbackContent(string message)
        {
            if (SettingsContent == null || SectionTitle == null) return;
            SectionTitle.Text = "Settings";
            SettingsContent.Content = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Colors.Red),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 20, 0, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
        }

        private void AnimateContentChange(UserControl newContent, string title, bool fromRight)
        {
            // Persist whatever the outgoing panel last set
            if (SettingsContent?.Content is ISettingsControl outgoing)
            {
                try { outgoing.Save(); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Save error: {ex.Message}");
                }
            }

            if (SettingsContent == null || SectionTitle == null)
                return;

            // Instant swap — no animation. Animation was causing lag.
            SettingsContent.Content = newContent;
            SectionTitle.Text = title;
        }
    }
}

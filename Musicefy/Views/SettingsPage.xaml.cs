using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

            try
            {
                ShowAppearance();
                _initialLoadPending = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Constructor init failed: {ex.Message}");
                ShowFallbackContent($"Error loading Appearance: {ex.Message}");
            }
        }

        private void SettingsPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
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

        private void ShowAppearance()
        {
            _appearanceVM = App.Services.GetService<AppearanceSettingsViewModel>();
            var control = new AppearanceSettingsControl();
            if (_appearanceVM != null)
                control.DataContext = _appearanceVM;
            AnimateContentChange(control, "Appearance Settings");
        }

        private void ShowDownloads()
        {
            var vm = App.Services.GetService<DownloadsSettingsViewModel>();
            var control = new DownloadsSettingsControl();
            if (vm != null)
                control.DataContext = vm;
            AnimateContentChange(control, "Downloads Settings");
        }

        private void ShowSources()
        {
            var vm = App.Services.GetService<SourcesSettingsViewModel>();
            var control = new SourcesSettingsControl();
            if (vm != null)
                control.Initialize(vm);
            AnimateContentChange(control, "Sources");
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

        private void AnimateContentChange(UserControl newContent, string title)
        {
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

            SettingsContent.Content = newContent;
            SectionTitle.Text = title;
        }
    }
}

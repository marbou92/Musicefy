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

            // After InitializeComponent completes, all named elements (including
            // SettingsContent and SectionTitle) are fully created. Set up the
            // default content here instead of relying solely on the Loaded event.
            //
            // NOTE: The AppearanceButton_Checked event fires DURING InitializeComponent
            // (because IsChecked="True" is set in XAML), but at that point
            // SettingsContent hasn't been created yet (it's defined later in the
            // XAML tree). The null check in AnimateContentChange silently skips
            // that early call. We set up the initial content here, after all
            // elements are guaranteed to exist.
            ShowAppearance();

            this.Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // Safety net: if for any reason the initial ShowAppearance() call
            // in the constructor didn't stick (e.g. SettingsContent was somehow
            // still null), try again on Loaded.
            if (_initialLoadPending)
            {
                _initialLoadPending = false;
                if (SettingsContent?.Content == null)
                {
                    ShowAppearance();
                }
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
            // FIX: Previously, SourcesSettingsControl was created without a ViewModel,
            // leaving DataContext null and the Sources list empty. Now we resolve the
            // SourcesSettingsViewModel from DI and call Initialize() to wire up the
            // data bindings (Sources list, PropertyChanged, empty state logic).
            var vm = App.Services.GetService<SourcesSettingsViewModel>();
            var control = new SourcesSettingsControl();
            if (vm != null)
                control.Initialize(vm);
            AnimateContentChange(control, "Sources Settings", fromRight: true);
        }

        private void ShowDiscover()
        {
            // DiscoverSettingsControl self-creates its ViewModel in its constructor,
            // so it works without DI injection. Just create it directly.
            var control = new DiscoverSettingsControl();
            AnimateContentChange(control, "Discover", fromRight: true);
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
            // FIX: ExtensionsSettingsControl self-creates its ViewModel in constructor,
            // so we don't need to inject from DI. However, we should not overwrite
            // its DataContext if it already has one.
            var control = new ExtensionsSettingsControl();
            AnimateContentChange(control, "Extensions", fromRight: true);
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

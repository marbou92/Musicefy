using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
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
            
            // Wire up SystemCommands bindings for high-compatibility chrome management
            InitializeWindowChromeCommands();
            
            ShowAppearance(); 
        }

        /// <summary>
        /// Binds the native WPF SystemCommands infrastructure to this window.
        /// </summary>
        private void InitializeWindowChromeCommands()
        {
            this.CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, OnMinimizeWindowCommand));
            this.CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, OnMaximizeWindowCommand));
            this.CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, OnRestoreWindowCommand));
            this.CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, OnCloseWindowCommand));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (GetTemplateChild("BtnShellMinimize") is Button btnMinimize)
            {
                btnMinimize.Click -= MinimizeHandler;
                btnMinimize.Click += MinimizeHandler;
            }

            if (GetTemplateChild("BtnShellMaximize") is Button btnMaximize)
            {
                btnMaximize.Click -= MaximizeHandler;
                btnMaximize.Click += MaximizeHandler;
            }

            if (GetTemplateChild("BtnShellClose") is Button btnClose)
            {
                btnClose.Click -= CloseHandler;
                btnClose.Click += CloseHandler;
            }
        }

        #region Native Window Chrome Mechanics

        private void MinimizeHandler(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
        private void CloseHandler(object sender, RoutedEventArgs e) => this.Close();
        private void MaximizeHandler(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
                SystemCommands.RestoreWindow(this);
            else
                SystemCommands.MaximizeWindow(this);
        }

        private void OnMinimizeWindowCommand(object target, ExecutedRoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
        private void OnMaximizeWindowCommand(object target, ExecutedRoutedEventArgs e) => SystemCommands.MaximizeWindow(this);
        private void OnRestoreWindowCommand(object target, ExecutedRoutedEventArgs e) => SystemCommands.RestoreWindow(this);
        private void OnCloseWindowCommand(object target, ExecutedRoutedEventArgs e) => this.Close();

        #endregion

        private void AppearanceButton_Click(object sender, RoutedEventArgs e) => ShowAppearance();
        private void DownloadsButton_Click(object sender, RoutedEventArgs e) => ShowDownloads();
        private void SourcesButton_Click(object sender, RoutedEventArgs e) => ShowSources();
        private void DiscoverButton_Click(object sender, RoutedEventArgs e) => ShowDiscover();
        private void RepositoriesButton_Click(object sender, RoutedEventArgs e) => ShowRepositories();
        private void ExtensionsButton_Click(object sender, RoutedEventArgs e) => ShowExtensions();

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
            void BeginInwardAnimation()
            {
                SettingsContent.Content = newContent;
                SectionTitle.Text = title;

                newContent.Opacity = 0;
                newContent.Margin = new Thickness(fromRight ? 40 : -40, 0, 0, 0);

                var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(180)));
                var slideIn = new ThicknessAnimation
                {
                    From = newContent.Margin,
                    To = new Thickness(0),
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
                    From = new Thickness(0),
                    To = new Thickness(fromRight ? -40 : 40, 0, 0, 0),
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

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsContent.Content is ISettingsControl settingsControl)
            {
                settingsControl.Save();
                MessageBox.Show("Settings saved.", "Musicefy", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsContent.Content is ISettingsControl settingsControl)
            {
                settingsControl.Cancel();
                MessageBox.Show("Settings cancelled.", "Musicefy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}

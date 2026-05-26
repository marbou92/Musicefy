using System;
using System.Windows;
using System.Windows.Controls;
using Musicefy.Core.Interfaces;
using Musicefy.Services;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class DownloadsSettingsControl : UserControl, ISettingsControl
    {
        private DownloadsSettingsViewModel ViewModel => DataContext as DownloadsSettingsViewModel;

        public void Save()
        {
            ViewModel?.Save();
        }

        public void Cancel()
        {
            ViewModel?.Cancel();
        }

        public DownloadsSettingsControl()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                if (DataContext is DownloadsSettingsViewModel vm)
                    Application.Current.Exit += (_, _) => vm.OnAppExit();
            };
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.BrowseFolder();
        }

        private void ClearNow_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ClearCache();
        }

        private void TestDownload_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.StartTestDownload();
        }

        private void PauseDownload_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.PauseDownload();
        }

        private void ResumeDownload_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ResumeDownload();
        }

        private void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.CancelDownload();
        }
    }
}

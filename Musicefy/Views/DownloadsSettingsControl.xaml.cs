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
        private EventHandler _exitHandler;

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
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is DownloadsSettingsViewModel vm)
            {
                _exitHandler = (_, _) => vm.OnAppExit();
                Application.Current.Exit += _exitHandler;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_exitHandler != null)
            {
                Application.Current.Exit -= _exitHandler;
                _exitHandler = null;
            }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.BrowseFolder();
        }

        private void ClearNow_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ClearCache();
        }

        private async void TestDownload_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) await ViewModel.StartTestDownload();
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

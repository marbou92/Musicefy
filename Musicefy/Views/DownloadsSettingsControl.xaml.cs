using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
        private ExitEventHandler _exitHandler;
        private ObservableCollection<string> _folders = new ObservableCollection<string>();

        public void Save()
        {
            ViewModel?.Save();
            ViewModel?.SaveLocalFolders(_folders.ToList());
        }

        public void Cancel()
        {
            ViewModel?.Cancel();
            // Reload folders from settings
            _folders.Clear();
            foreach (var f in ViewModel?.GetLocalFolders() ?? Enumerable.Empty<string>())
                _folders.Add(f);
            FoldersList.ItemsSource = _folders;
            UpdateNoFoldersText();
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

                // Load local folders into the list
                _folders.Clear();
                foreach (var f in vm.GetLocalFolders())
                    _folders.Add(f);
                FoldersList.ItemsSource = _folders;
                UpdateNoFoldersText();

                // Sync the checkboxes with the ViewModel
                AutoClearCacheBox.IsChecked = vm.AutoClearCache;
                LimitDownloadSizeBox.IsChecked = vm.LimitDownloadSize;
                DownloadPathBox.Text = vm.DownloadsPath;

                YouTubeEnabledBox.IsChecked = vm.YouTubeEnabled;
                YouTubeApiKeyBox.Text = vm.YouTubeApiKey;
                YouTubeCookieBox.Text = vm.YouTubeCookie;
                AudioQualityCombo.SelectedIndex = vm.YouTubeAudioQualityIndex;

                SponsorBlockEnabledBox.IsChecked = vm.SponsorBlockEnabled;
                LyricsEnabledBox.IsChecked = vm.LyricsEnabled;
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

        // ── Local Folders ────────────────────────────────────────────────────

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select a folder containing music files",
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var path = dialog.SelectedPath;
                if (!_folders.Contains(path))
                {
                    _folders.Add(path);
                    UpdateNoFoldersText();
                    // Save immediately so the Local source can be updated
                    ViewModel?.SaveLocalFolders(_folders.ToList());
                }
            }
        }

        private void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path)
            {
                _folders.Remove(path);
                UpdateNoFoldersText();
                ViewModel?.SaveLocalFolders(_folders.ToList());
            }
        }

        private void UpdateNoFoldersText()
        {
            NoFoldersText.Visibility = _folders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Existing download handlers ───────────────────────────────────────

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.BrowseFolder();
            DownloadPathBox.Text = ViewModel?.DownloadsPath ?? "";
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

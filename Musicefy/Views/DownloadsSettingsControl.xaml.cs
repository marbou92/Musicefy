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
        private ObservableCollection<string> _folders = new ObservableCollection<string>();

        public void Save()
        {
            ViewModel?.Save();
            ViewModel?.SaveLocalFolders(_folders.ToList());
        }

        public void Cancel()
        {
            ViewModel?.Cancel();
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
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is DownloadsSettingsViewModel vm)
            {
                _folders.Clear();
                foreach (var f in vm.GetLocalFolders())
                    _folders.Add(f);
                FoldersList.ItemsSource = _folders;
                UpdateNoFoldersText();

                AutoClearCacheBox.IsChecked = vm.AutoClearCache;
                LimitDownloadSizeBox.IsChecked = vm.LimitDownloadSize;
                DownloadPathBox.Text = vm.DownloadsPath;
                CacheStatusLabel.Text = vm.CacheStatusText;
                CacheProgressBar.Value = vm.CacheProgressPercent;
            }
        }

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

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.BrowseFolder();
            DownloadPathBox.Text = ViewModel?.DownloadsPath ?? "";
        }

        private void ClearNow_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ClearCache();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Save();
        }
    }
}

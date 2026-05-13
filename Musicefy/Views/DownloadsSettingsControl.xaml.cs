using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Musicefy.Views
{
    public partial class DownloadsSettingsControl : UserControl
    {
        private string _downloadsPath;

        public DownloadsSettingsControl()
        {
            InitializeComponent();
            LoadSettings();

            // Hook into application exit for auto-clear
            Application.Current.Exit += OnAppExit;
        }

        private void LoadSettings()
        {
            // Default path: %LOCALAPPDATA%\Musicefy\Downloads
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _downloadsPath = Musicefy.Properties.Settings.Default.DownloadsPath ??
                             Path.Combine(appData, "Musicefy", "Downloads");

            DownloadPathBox.Text = _downloadsPath;
            AutoClearCacheBox.IsChecked = Musicefy.Properties.Settings.Default.AutoClearCache;
            LimitDownloadSizeBox.IsChecked = Musicefy.Properties.Settings.Default.LimitDownloadSize;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select download folder",
                SelectedPath = _downloadsPath
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _downloadsPath = dialog.SelectedPath;
                DownloadPathBox.Text = _downloadsPath;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Musicefy.Properties.Settings.Default.DownloadsPath = _downloadsPath;
            Musicefy.Properties.Settings.Default.AutoClearCache = AutoClearCacheBox.IsChecked ?? false;
            Musicefy.Properties.Settings.Default.LimitDownloadSize = LimitDownloadSizeBox.IsChecked ?? false;
            Musicefy.Properties.Settings.Default.Save();

            MessageBox.Show("Download settings saved.", "Musicefy", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            LoadSettings();
        }

        private void ClearNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ClearCache(_downloadsPath);
                MessageBox.Show("Downloads cache cleared.", "Musicefy", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to clear cache: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnAppExit(object sender, ExitEventArgs e)
        {
            if (Musicefy.Properties.Settings.Default.AutoClearCache)
            {
                ClearCache(_downloadsPath);
            }
        }

        private void ClearCache(string path)
        {
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    File.Delete(file);
                }
                foreach (var dir in Directory.GetDirectories(path))
                {
                    Directory.Delete(dir, true);
                }
            }
        }
    }
}

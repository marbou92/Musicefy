using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Musicefy.Views
{
    public partial class DownloadsSettingsControl : UserControl
    {
        private string _downloadsPath;

        public DownloadsSettingsControl()
        {
            InitializeComponent();
            LoadSettings();
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
    }
}

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

            UpdateCacheStatus();
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
                UpdateCacheStatus();
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
            var result = MessageBox.Show("Are you sure you want to clear all downloads? This action cannot be undone.",
                                         "Confirm Clear",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    ClearCache(_downloadsPath);
                    UpdateCacheStatus();
                    MessageBox.Show("Downloads cache cleared.", "Musicefy", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to clear cache: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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

        private void UpdateCacheStatus()
        {
            long size = GetDirectorySize(_downloadsPath);
            double sizeMB = size / (1024.0 * 1024.0);
            CacheStatusLabel.Text = $"Cache size: {sizeMB:F2} MB";

            // Update progress bar (max 2000 MB = 2 GB)
            CacheProgressBar.Value = Math.Min(sizeMB, 2000);

            // Tooltip with exact size
            CacheProgressBar.ToolTip = $"Cache size: {sizeMB:F2} MB ({size / (1024.0 * 1024.0 * 1024.0):F2} GB)";

            // Dynamic color gradient
            if (sizeMB < 100)
            {
                CacheProgressBar.Foreground = new SolidColorBrush(Colors.LimeGreen);
            }
            else if (sizeMB < 300)
            {
                CacheProgressBar.Foreground = new SolidColorBrush(Colors.Gold);
            }
            else
            {
                CacheProgressBar.Foreground = new SolidColorBrush(Colors.OrangeRed);
            }

            // Warning popup if cache exceeds 400 MB
            if (sizeMB > 400 && sizeMB < 2000)
            {
                MessageBox.Show("Warning: Cache size exceeds 400 MB. Consider clearing to free space.",
                                "Cache Warning",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
            }

            // Hard limit at 2 GB
            if (sizeMB >= 2000)
            {
                MessageBox.Show("Cache limit reached (2 GB). Downloads may be blocked until you clear space.",
                                "Cache Limit",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private long GetDirectorySize(string path)
        {
            if (!Directory.Exists(path)) return 0;

            long size = 0;
            try
            {
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    size += new FileInfo(file).Length;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating cache size: {ex.Message}");
            }
            return size;
        }
    }
}

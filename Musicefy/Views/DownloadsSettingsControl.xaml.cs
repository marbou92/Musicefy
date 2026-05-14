using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Musicefy.Services;

namespace Musicefy.Views
{
    public partial class DownloadsSettingsControl : UserControl
    {
        private string _downloadsPath;
        private DispatcherTimer _cacheMonitorTimer;
        private CancellationTokenSource _downloadCts;

        public DownloadsSettingsControl()
        {
            InitializeComponent();
            LoadSettings();

            Application.Current.Exit += OnAppExit;

            _cacheMonitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _cacheMonitorTimer.Tick += (s, e) => UpdateCacheStatus();
            _cacheMonitorTimer.Start();
        }

        private void LoadSettings()
        {
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

            ToastService.ShowToast("✅ Download settings saved.", Brushes.ForestGreen);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            ToastService.ShowToast("↩ Changes reverted.", Brushes.Gray);
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
                    ToastService.ShowToast("🗑 Downloads cache cleared.", Brushes.ForestGreen);
                }
                catch (Exception ex)
                {
                    ToastService.ShowToast($"❌ Failed to clear cache: {ex.Message}", Brushes.OrangeRed);
                }
            }
        }

        private async void TestDownload_Click(object sender, RoutedEventArgs e)
        {
            string testUrl = "https://speed.hetzner.de/100MB.bin"; // sample test file
            string fileName = "TestDownload.bin";

            DownloadStatusLabel.Text = "Starting download...";
            CacheProgressBar.Value = 0;

            _downloadCts = new CancellationTokenSource();

            bool success = await DownloadManager.DownloadFileAsync(testUrl, fileName, (percent, bytes) =>
            {
                Dispatcher.Invoke(() =>
                {
                    CacheProgressBar.Value = percent;
                    DownloadStatusLabel.Text = $"Downloading... {percent}% ({bytes / (1024.0 * 1024.0):F2} MB)";
                });
            }, _downloadCts.Token);

            if (success)
            {
                DownloadStatusLabel.Text = "Download complete.";
                UpdateCacheStatus();
            }
            else
            {
                if (_downloadCts.IsCancellationRequested)
                    DownloadStatusLabel.Text = "Download cancelled.";
                else
                    DownloadStatusLabel.Text = "Download failed.";
            }
        }

        private void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_downloadCts != null && !_downloadCts.IsCancellationRequested)
            {
                _downloadCts.Cancel();
                ToastService.ShowToast("⚠ Download cancelled by user.", Brushes.Goldenrod);
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
                    File.Delete(file);

                foreach (var dir in Directory.GetDirectories(path))
                    Directory.Delete(dir, true);
            }
        }

        private void UpdateCacheStatus()
        {
            long size = GetDirectorySize(_downloadsPath);
            double sizeMB = size / (1024.0 * 1024.0);
            CacheStatusLabel.Text = $"Cache size: {sizeMB:F2} MB";

            CacheProgressBar.ToolTip = $"Cache size: {sizeMB:F2} MB ({size / (1024.0 * 1024.0 * 1024.0):F2} GB)";

            if (sizeMB < 100)
                CacheProgressBar.Foreground = new SolidColorBrush(Colors.LimeGreen);
            else if (sizeMB < 300)
                CacheProgressBar.Foreground = new SolidColorBrush(Colors.Gold);
            else
                CacheProgressBar.Foreground = new SolidColorBrush(Colors.OrangeRed);

            if (sizeMB > 400 && sizeMB < 2000)
            {
                ToastService.ShowToast("⚠ Cache size exceeds 400 MB. Consider clearing to free space.", Brushes.Goldenrod);
            }

            if (sizeMB >= 2000)
            {
                ToastService.ShowToast("❌ Cache limit reached (2 GB). Downloads may be blocked until you clear space.", Brushes.OrangeRed);
            }
        }

        private long GetDirectorySize(string path)
        {
            if (!Directory.Exists(path)) return 0;

            long size = 0;
            try
            {
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    size += new FileInfo(file).Length;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating cache size: {ex.Message}");
            }
            return size;
        }
    }
}

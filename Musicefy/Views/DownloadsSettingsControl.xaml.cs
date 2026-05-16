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
        private bool _resumeMode;

        public DownloadsSettingsControl()
        {
            InitializeComponent();
            LoadSettings();

            Application.Current.Exit += OnAppExit;

            _cacheMonitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _cacheMonitorTimer.Tick += (s, e) => UpdateCacheStatus();
            _cacheMonitorTimer.Start();

            SetIdleState();
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
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select download folder";
                dialog.SelectedPath = _downloadsPath;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _downloadsPath = dialog.SelectedPath;
                    DownloadPathBox.Text = _downloadsPath;
                    UpdateCacheStatus();
                }
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
            // FIXED: Changed Brushes.Workspace typo to Brushes.Gray
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
            string testUrl = "https://speed.hetzner.de/100MB.bin"; 
            string fileName = "TestDownload.bin";

            DownloadStatusLabel.Text = _resumeMode ? "Resuming..." : "Starting...";
            if (!_resumeMode) CacheProgressBar.Value = 0;

            _downloadCts = new CancellationTokenSource();
            SetDownloadingState();

            bool success = await DownloadManager.DownloadFileAsync(testUrl, fileName, (percent, bytes) =>
            {
                Dispatcher.Invoke(() =>
                {
                    CacheProgressBar.Value = percent;
                    DownloadStatusLabel.Text = $"Downloading... {percent}% ({bytes / (1024.0 * 1024.0):F2} MB)";
                });
            }, _downloadCts.Token, _resumeMode);

            if (success)
            {
                DownloadStatusLabel.Text = "Complete";
                _resumeMode = false;
                UpdateCacheStatus();
                SetIdleState();
            }
            else
            {
                if (_downloadCts.IsCancellationRequested && _resumeMode)
                {
                    DownloadStatusLabel.Text = "Paused";
                    SetPausedState();
                }
                else if (_downloadCts.IsCancellationRequested && !_resumeMode)
                {
                    DownloadStatusLabel.Text = "Cancelled";
                    CacheProgressBar.Value = 0;
                    
                    try
                    {
                        string partialFile = Path.Combine(_downloadsPath, fileName);
                        if (File.Exists(partialFile)) File.Delete(partialFile);
                    }
                    catch { }

                    SetIdleState();
                }
                else
                {
                    DownloadStatusLabel.Text = "Failed";
                    _resumeMode = false;
                    SetIdleState();
                }
            }
        }

        private void PauseDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_downloadCts != null && !_downloadCts.IsCancellationRequested)
            {
                _resumeMode = true;
                _downloadCts.Cancel();
                ToastService.ShowToast("⏸ Download paused.", Brushes.Goldenrod);
                SetPausedState();
            }
        }

        private void ResumeDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_resumeMode)
            {
                TestDownload_Click(sender, e);
            }
        }

        private void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_downloadCts != null && !_downloadCts.IsCancellationRequested)
            {
                _resumeMode = false; 
                _downloadCts.Cancel();
                ToastService.ShowToast("⚠ Download cancelled by user.", Brushes.OrangeRed);
                SetIdleState();
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

            long warningThresholdBytes = Musicefy.Properties.Settings.Default.CacheWarningThreshold;
            long globalLimitBytes = Musicefy.Properties.Settings.Default.GlobalCacheLimit;

            CacheProgressBar.ToolTip = $"Cache size: {sizeMB:F2} MB ({size / (1024.0 * 1024.0 * 1024.0):F2} GB)";

            if (size < warningThresholdBytes)
                CacheProgressBar.Foreground = Brushes.LimeGreen;
            else if (size < globalLimitBytes)
                CacheProgressBar.Foreground = Brushes.Gold;
            else
                CacheProgressBar.Foreground = Brushes.OrangeRed;
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

        private void SetIdleState()
        {
            TestDownloadButton.IsEnabled = true;
            PauseDownloadButton.IsEnabled = false;
            ResumeDownloadButton.IsEnabled = false;
            CancelDownloadButton.IsEnabled = false;
            DownloadStatusLabel.Foreground = Brushes.Gray;
        }

        private void SetDownloadingState()
        {
            TestDownloadButton.IsEnabled = false;
            PauseDownloadButton.IsEnabled = true;
            ResumeDownloadButton.IsEnabled = false;
            CancelDownloadButton.IsEnabled = true;
            DownloadStatusLabel.Foreground = Brushes.ForestGreen;
        }

        private void SetPausedState()
        {
            TestDownloadButton.IsEnabled = false;
            PauseDownloadButton.IsEnabled = false;
            ResumeDownloadButton.IsEnabled = true;
            CancelDownloadButton.IsEnabled = true;
            DownloadStatusLabel.Foreground = Brushes.Goldenrod;
        }
    }
}

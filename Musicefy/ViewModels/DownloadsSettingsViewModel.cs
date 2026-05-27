using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Musicefy.Core.Interfaces;
using Musicefy.Services;

namespace Musicefy.ViewModels
{
    public class DownloadsSettingsViewModel : ViewModelBase, IDisposable
    {
        private readonly IDownloadService _downloadService;
        private string _downloadsPath;

        private string _cacheStatusText = "Cache size: 0.00 MB";
        public string CacheStatusText
        {
            get => _cacheStatusText;
            set => SetProperty(ref _cacheStatusText, value);
        }

        private double _cacheProgressPercent;
        public double CacheProgressPercent
        {
            get => _cacheProgressPercent;
            set => SetProperty(ref _cacheProgressPercent, value);
        }

        private string _downloadStatusText = "Ready";
        public string DownloadStatusText
        {
            get => _downloadStatusText;
            set => SetProperty(ref _downloadStatusText, value);
        }

        private Brush _downloadStatusForeground = Brushes.Gray;
        public Brush DownloadStatusForeground
        {
            get => _downloadStatusForeground;
            set => SetProperty(ref _downloadStatusForeground, value);
        }

        private bool _isIdle = true;
        public bool IsIdle
        {
            get => _isIdle;
            set => SetProperty(ref _isIdle, value);
        }

        private bool _isDownloading;
        public bool IsDownloading
        {
            get => _isDownloading;
            set => SetProperty(ref _isDownloading, value);
        }

        private bool _isPaused;
        public bool IsPaused
        {
            get => _isPaused;
            set => SetProperty(ref _isPaused, value);
        }

        private CancellationTokenSource _downloadCts;
        private bool _resumeMode;
        private DispatcherTimer _cacheMonitorTimer;

        public DownloadsSettingsViewModel(IDownloadService downloadService)
        {
            _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _downloadsPath = Musicefy.Properties.Settings.Default.DownloadsPath ??
                             Path.Combine(appData, "Musicefy", "Downloads");

            _cacheMonitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _cacheMonitorTimer.Tick += (s, e) => UpdateCacheStatus();
            _cacheMonitorTimer.Start();

            UpdateCacheStatus();
            SetIdleState();
        }

        public string DownloadsPath
        {
            get => _downloadsPath;
            set
            {
                _downloadsPath = value;
                OnPropertyChanged();
            }
        }

        public bool AutoClearCache
        {
            get => Musicefy.Properties.Settings.Default.AutoClearCache;
            set => Musicefy.Properties.Settings.Default.AutoClearCache = value;
        }

        public bool LimitDownloadSize
        {
            get => Musicefy.Properties.Settings.Default.LimitDownloadSize;
            set => Musicefy.Properties.Settings.Default.LimitDownloadSize = value;
        }

        public void Save()
        {
            Musicefy.Properties.Settings.Default.DownloadsPath = _downloadsPath;
            Musicefy.Properties.Settings.Default.AutoClearCache = AutoClearCache;
            Musicefy.Properties.Settings.Default.LimitDownloadSize = LimitDownloadSize;
            Musicefy.Properties.Settings.Default.Save();
            ToastService.ShowToast("Download settings saved.", Brushes.ForestGreen);
        }

        public void Cancel()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _downloadsPath = Musicefy.Properties.Settings.Default.DownloadsPath ??
                             Path.Combine(appData, "Musicefy", "Downloads");
            OnPropertyChanged(nameof(DownloadsPath));
            OnPropertyChanged(nameof(AutoClearCache));
            OnPropertyChanged(nameof(LimitDownloadSize));
            UpdateCacheStatus();
            ToastService.ShowToast("Changes reverted.", Brushes.Gray);
        }

        public void BrowseFolder()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select download folder",
                SelectedPath = _downloadsPath
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DownloadsPath = dialog.SelectedPath;
                UpdateCacheStatus();
            }
        }

        public void ClearCache()
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear all downloads? This action cannot be undone.",
                "Confirm Clear", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                ClearCacheDirectory(_downloadsPath);
                UpdateCacheStatus();
                ToastService.ShowToast("Downloads cache cleared.", Brushes.ForestGreen);
            }
            catch (Exception ex)
            {
                ToastService.ShowToast($"Failed to clear cache: {ex.Message}", Brushes.OrangeRed);
            }
        }

        public async Task StartTestDownload()
        {
            string testUrl = "https://speed.hetzner.de/100MB.bin";
            string fileName = "TestDownload.bin";

            DownloadStatusText = _resumeMode ? "Resuming..." : "Starting...";
            if (!_resumeMode) CacheProgressPercent = 0;

            _downloadCts = new CancellationTokenSource();
            SetDownloadingState();

            bool success = await _downloadService.DownloadFileAsync(testUrl, fileName, (percent, bytes) =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    CacheProgressPercent = percent;
                    DownloadStatusText = $"Downloading... {percent}% ({bytes / (1024.0 * 1024.0):F2} MB)";
                });
            }, _downloadCts.Token, _resumeMode);

            if (success)
            {
                DownloadStatusText = "Complete";
                _resumeMode = false;
                UpdateCacheStatus();
                SetIdleState();
            }
            else
            {
                if (_downloadCts.IsCancellationRequested && _resumeMode)
                {
                    DownloadStatusText = "Paused";
                    SetPausedState();
                }
                else if (_downloadCts.IsCancellationRequested && !_resumeMode)
                {
                    DownloadStatusText = "Cancelled";
                    CacheProgressPercent = 0;
                    try
                    {
                        string partialFile = Path.Combine(_downloadsPath, fileName);
                        if (File.Exists(partialFile)) File.Delete(partialFile);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DownloadsSettings] Failed to delete partial file: {ex.Message}");
                    }
                    SetIdleState();
                }
                else
                {
                    DownloadStatusText = "Failed";
                    _resumeMode = false;
                    SetIdleState();
                }
            }
        }

        public void PauseDownload()
        {
            if (_downloadCts != null && !_downloadCts.IsCancellationRequested)
            {
                _resumeMode = true;
                _downloadCts.Cancel();
                ToastService.ShowToast("Download paused.", Brushes.Goldenrod);
                SetPausedState();
            }
        }

        public void ResumeDownload()
        {
            if (_resumeMode)
                _ = StartTestDownload();
        }

        public void CancelDownload()
        {
            if (_downloadCts != null && !_downloadCts.IsCancellationRequested)
            {
                _resumeMode = false;
                _downloadCts.Cancel();
                ToastService.ShowToast("Download cancelled.", Brushes.OrangeRed);
                SetIdleState();
            }
        }

        public void OnAppExit()
        {
            if (Musicefy.Properties.Settings.Default.AutoClearCache)
                ClearCacheDirectory(_downloadsPath);
        }

        private void UpdateCacheStatus()
        {
            long size = GetDirectorySize(_downloadsPath);
            double sizeMB = size / (1024.0 * 1024.0);
            CacheStatusText = $"Cache size: {sizeMB:F2} MB";

            long warningThresholdBytes = Musicefy.Properties.Settings.Default.CacheWarningThreshold;
            long globalLimitBytes = Musicefy.Properties.Settings.Default.GlobalCacheLimit;

            if (size < warningThresholdBytes)
                CacheProgressPercent = (double)size / warningThresholdBytes * 50;
            else if (size < globalLimitBytes)
                CacheProgressPercent = 50 + (double)(size - warningThresholdBytes) / (globalLimitBytes - warningThresholdBytes) * 50;
            else
                CacheProgressPercent = 100;
        }

        private static long GetDirectorySize(string path)
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
                System.Diagnostics.Debug.WriteLine($"[DownloadsSettings] GetDirectorySize failed: {ex.Message}");
            }
            return size;
        }

        private static void ClearCacheDirectory(string path)
        {
            if (!Directory.Exists(path)) return;
            foreach (var file in Directory.GetFiles(path))
                File.Delete(file);
            foreach (var dir in Directory.GetDirectories(path))
                Directory.Delete(dir, true);
        }

        private void SetIdleState()
        {
            IsIdle = true;
            IsDownloading = false;
            IsPaused = false;
            DownloadStatusForeground = Brushes.Gray;
        }

        private void SetDownloadingState()
        {
            IsIdle = false;
            IsDownloading = true;
            IsPaused = false;
            DownloadStatusForeground = Brushes.ForestGreen;
        }

        private void SetPausedState()
        {
            IsIdle = false;
            IsDownloading = false;
            IsPaused = true;
            DownloadStatusForeground = Brushes.Goldenrod;
        }

        public void Dispose()
        {
            _cacheMonitorTimer?.Stop();
            try { _downloadCts?.Cancel(); } catch (ObjectDisposedException) { }
            _downloadCts?.Dispose();
        }
    }
}

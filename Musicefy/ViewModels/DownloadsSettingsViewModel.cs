using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        // ── Sprint 4: YouTube settings ───────────────────────────────────────
        public bool YouTubeEnabled
        {
            get => Musicefy.Properties.Settings.Default.YouTubeEnabled;
            set { Musicefy.Properties.Settings.Default.YouTubeEnabled = value; OnPropertyChanged(); }
        }

        public string YouTubeApiKey
        {
            get => Musicefy.Properties.Settings.Default.YouTubeApiKey ?? "";
            set { Musicefy.Properties.Settings.Default.YouTubeApiKey = value; OnPropertyChanged(); }
        }

        public string YouTubeCookie
        {
            get => Musicefy.Properties.Settings.Default.YouTubeCookie ?? "";
            set { Musicefy.Properties.Settings.Default.YouTubeCookie = value; OnPropertyChanged(); }
        }

        public int YouTubeAudioQualityIndex
        {
            get => string.Equals(Musicefy.Properties.Settings.Default.YouTubeAudioQuality, "aac", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            set
            {
                var newQuality = value == 1 ? "aac" : "opus";
                Musicefy.Properties.Settings.Default.YouTubeAudioQuality = newQuality;
                OnPropertyChanged();

                // Sprint 5: Also update the YouTube source's audioQuality config
                // so the change takes effect on the next track (no restart needed).
                try
                {
                    var sourceManager = App.Services?.GetService(typeof(Musicefy.Core.Interfaces.IStreamingSourceManager))
                                         as Musicefy.Core.Interfaces.IStreamingSourceManager;
                    if (sourceManager != null)
                    {
                        var ytSource = sourceManager.Sources.FirstOrDefault(
                            s => string.Equals(s.Type, Musicefy.Core.SourceTypes.YouTube, StringComparison.OrdinalIgnoreCase));
                        if (ytSource != null)
                        {
                            ytSource.Configuration["audioQuality"] = newQuality;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DownloadsSettings] Failed to update YouTube audio quality: {ex.Message}");
                }
            }
        }

        // ── Sprint 5: Skip Silence settings ─────────────────────────────────
        public bool SkipSilenceEnabled
        {
            get => Musicefy.Properties.Settings.Default.SkipSilenceEnabled;
            set { Musicefy.Properties.Settings.Default.SkipSilenceEnabled = value; OnPropertyChanged(); }
        }

        public int SkipSilenceThresholdDb
        {
            get => Musicefy.Properties.Settings.Default.SkipSilenceThresholdDb;
            set { Musicefy.Properties.Settings.Default.SkipSilenceThresholdDb = value; OnPropertyChanged(); }
        }

        // ── Sprint 5: Crossfade settings ────────────────────────────────────
        public bool CrossfadeEnabled
        {
            get => Musicefy.Properties.Settings.Default.CrossfadeEnabled;
            set { Musicefy.Properties.Settings.Default.CrossfadeEnabled = value; OnPropertyChanged(); }
        }

        public double CrossfadeDurationSeconds
        {
            get => Musicefy.Properties.Settings.Default.CrossfadeDurationSeconds;
            set { Musicefy.Properties.Settings.Default.CrossfadeDurationSeconds = value; OnPropertyChanged(); }
        }

        // ── Sprint 4: SponsorBlock settings ──────────────────────────────────
        public bool SponsorBlockEnabled
        {
            get => Musicefy.Properties.Settings.Default.SponsorBlockEnabled;
            set { Musicefy.Properties.Settings.Default.SponsorBlockEnabled = value; OnPropertyChanged(); }
        }

        public bool SponsorBlockSkipSponsor
        {
            get => Musicefy.Properties.Settings.Default.SponsorBlockSkipSponsor;
            set { Musicefy.Properties.Settings.Default.SponsorBlockSkipSponsor = value; OnPropertyChanged(); }
        }

        public bool SponsorBlockSkipIntro
        {
            get => Musicefy.Properties.Settings.Default.SponsorBlockSkipIntro;
            set { Musicefy.Properties.Settings.Default.SponsorBlockSkipIntro = value; OnPropertyChanged(); }
        }

        public bool SponsorBlockSkipOutro
        {
            get => Musicefy.Properties.Settings.Default.SponsorBlockSkipOutro;
            set { Musicefy.Properties.Settings.Default.SponsorBlockSkipOutro = value; OnPropertyChanged(); }
        }

        public bool SponsorBlockSkipSelfPromo
        {
            get => Musicefy.Properties.Settings.Default.SponsorBlockSkipSelfPromo;
            set { Musicefy.Properties.Settings.Default.SponsorBlockSkipSelfPromo = value; OnPropertyChanged(); }
        }

        public bool SponsorBlockSkipInteraction
        {
            get => Musicefy.Properties.Settings.Default.SponsorBlockSkipInteraction;
            set { Musicefy.Properties.Settings.Default.SponsorBlockSkipInteraction = value; OnPropertyChanged(); }
        }

        // ── Sprint 4: Lyrics settings ────────────────────────────────────────
        public bool LyricsEnabled
        {
            get => Musicefy.Properties.Settings.Default.LyricsEnabled;
            set { Musicefy.Properties.Settings.Default.LyricsEnabled = value; OnPropertyChanged(); }
        }

        public string LyricsProvider
        {
            get => Musicefy.Properties.Settings.Default.LyricsProvider ?? "LrcLib";
            set { Musicefy.Properties.Settings.Default.LyricsProvider = value; OnPropertyChanged(); }
        }

        // ── Sprint 4: Home screen visibility (absorbed from Discover) ────────
        public bool ShowLocalOnHome
        {
            get => Musicefy.Properties.Settings.Default.DiscoverLibrary;
            set { Musicefy.Properties.Settings.Default.DiscoverLibrary = value; OnPropertyChanged(); }
        }

        public bool ShowYouTubeOnHome
        {
            get => Musicefy.Properties.Settings.Default.DiscoverYouTube;
            set { Musicefy.Properties.Settings.Default.DiscoverYouTube = value; OnPropertyChanged(); }
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

        // ── Sprint 4: Local music folders management ─────────────────────────

        /// <summary>
        /// Returns the list of local music folders from Settings (semicolon-delimited).
        /// </summary>
        public List<string> GetLocalFolders()
        {
            var raw = Musicefy.Properties.Settings.Default.LocalMusicFolders ?? "";
            return raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(f => f.Trim())
                      .Where(f => !string.IsNullOrEmpty(f))
                      .ToList();
        }

        /// <summary>
        /// Persists the local folders list to Settings and updates the
        /// auto-provisioned Local source's folderPath.
        /// </summary>
        public void SaveLocalFolders(List<string> folders)
        {
            Musicefy.Properties.Settings.Default.LocalMusicFolders =
                string.Join(";", folders ?? new List<string>());
            Musicefy.Properties.Settings.Default.Save();

            // Update the Local source's folderPath if it exists
            try
            {
                var sourceManager = App.Services?.GetService(typeof(IStreamingSourceManager)) as IStreamingSourceManager;
                if (sourceManager != null)
                {
                    var localSource = sourceManager.Sources.FirstOrDefault(
                        s => string.Equals(s.Type, Core.SourceTypes.Local, StringComparison.OrdinalIgnoreCase));
                    if (localSource != null && folders != null && folders.Count > 0)
                    {
                        localSource.Configuration["folderPath"] = folders[0];
                        localSource.Url = folders[0];
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DownloadsSettings] SaveLocalFolders failed to update source: {ex.Message}");
            }
        }

        public void Save()
        {
            Musicefy.Properties.Settings.Default.DownloadsPath = _downloadsPath;
            Musicefy.Properties.Settings.Default.AutoClearCache = AutoClearCache;
            Musicefy.Properties.Settings.Default.LimitDownloadSize = LimitDownloadSize;
            Musicefy.Properties.Settings.Default.Save();
            ToastService.ShowToast("Settings saved.", Brushes.ForestGreen);
        }

        public void Cancel()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _downloadsPath = Musicefy.Properties.Settings.Default.DownloadsPath ??
                             Path.Combine(appData, "Musicefy", "Downloads");
            OnPropertyChanged(nameof(DownloadsPath));
            OnPropertyChanged(nameof(AutoClearCache));
            OnPropertyChanged(nameof(LimitDownloadSize));
            OnPropertyChanged(nameof(YouTubeEnabled));
            OnPropertyChanged(nameof(YouTubeApiKey));
            OnPropertyChanged(nameof(YouTubeCookie));
            OnPropertyChanged(nameof(YouTubeAudioQualityIndex));
            OnPropertyChanged(nameof(SponsorBlockEnabled));
            OnPropertyChanged(nameof(LyricsEnabled));
            OnPropertyChanged(nameof(ShowLocalOnHome));
            OnPropertyChanged(nameof(ShowYouTubeOnHome));
            OnPropertyChanged(nameof(SkipSilenceEnabled));
            OnPropertyChanged(nameof(SkipSilenceThresholdDb));
            OnPropertyChanged(nameof(CrossfadeEnabled));
            OnPropertyChanged(nameof(CrossfadeDurationSeconds));
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

            if (warningThresholdBytes <= 0 || globalLimitBytes <= 0)
            {
                CacheProgressPercent = 0;
                return;
            }

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

using System;
using System.IO;
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

        public DownloadsSettingsControl()
        {
            InitializeComponent();
            LoadSettings();

            Application.Current.Exit += OnAppExit;

            _cacheMonitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _cacheMonitorTimer.Tick += (s, e) => UpdateCacheStatus();
            _cacheMonitorTimer.Start();
        }

        // ... other methods unchanged ...

        private void UpdateCacheStatus()
        {
            long size = GetDirectorySize(_downloadsPath);
            double sizeMB = size / (1024.0 * 1024.0);
            CacheStatusLabel.Text = $"Cache size: {sizeMB:F2} MB";

            CacheProgressBar.Value = Math.Min(sizeMB, 2000);
            CacheProgressBar.ToolTip = $"Cache size: {sizeMB:F2} MB ({size / (1024.0 * 1024.0 * 1024.0):F2} GB)";

            if (sizeMB < 100)
                CacheProgressBar.Foreground = new SolidColorBrush(Colors.LimeGreen);
            else if (sizeMB < 300)
                CacheProgressBar.Foreground = new SolidColorBrush(Colors.Gold);
            else
                CacheProgressBar.Foreground = new SolidColorBrush(Colors.OrangeRed);

            // Toast notifications instead of blocking MessageBox
            if (sizeMB > 400 && sizeMB < 2000)
            {
                ToastService.ShowToast("⚠ Cache size exceeds 400 MB. Consider clearing to free space.",
                                       Brushes.Goldenrod);
            }

            if (sizeMB >= 2000)
            {
                ToastService.ShowToast("❌ Cache limit reached (2 GB). Downloads may be blocked until you clear space.",
                                       Brushes.OrangeRed);
            }
        }

        // ... rest unchanged ...
    }
}

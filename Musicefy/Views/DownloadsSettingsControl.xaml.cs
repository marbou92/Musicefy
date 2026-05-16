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
        // ... (Keep all existing top/middle logic methods exactly as they are)

        private void SetIdleState()
        {
            TestDownloadButton.IsEnabled = true;
            PauseDownloadButton.IsEnabled = false;
            ResumeDownloadButton.IsEnabled = false;
            CancelDownloadButton.IsEnabled = false;
            
            // FIXED: Removed the corrupted Brushes.Private statement completely
            DownloadStatusLabel.Foreground = Brushes.Private; // DELETED THIS LINE
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

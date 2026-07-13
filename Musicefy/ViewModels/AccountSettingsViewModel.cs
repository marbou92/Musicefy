using System;
using System.Linq;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Interfaces;
using Musicefy.Services;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// Sprint 8: ViewModel for the Account settings tab.
    /// Handles YouTube Music login (cookie), API key, and audio quality.
    /// </summary>
    public class AccountSettingsViewModel : ViewModelBase
    {
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

                // Also update the YouTube source's audioQuality config live
                try
                {
                    var sourceManager = App.Services?.GetService(typeof(IStreamingSourceManager))
                                         as IStreamingSourceManager;
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
                    System.Diagnostics.Debug.WriteLine($"[AccountSettings] Failed to update YouTube audio quality: {ex.Message}");
                }
            }
        }

        public ICommand SaveCommand { get; }

        public AccountSettingsViewModel()
        {
            SaveCommand = new RelayCommand(_ => Save());
        }

        private void Save()
        {
            Musicefy.Properties.Settings.Default.Save();

            // Update the YouTube source's cookie + apiKey live
            try
            {
                var sourceManager = App.Services?.GetService(typeof(IStreamingSourceManager))
                                     as IStreamingSourceManager;
                if (sourceManager != null)
                {
                    var ytSource = sourceManager.Sources.FirstOrDefault(
                        s => string.Equals(s.Type, Musicefy.Core.SourceTypes.YouTube, StringComparison.OrdinalIgnoreCase));
                    if (ytSource != null)
                    {
                        ytSource.Configuration["apiKey"] = YouTubeApiKey ?? "";
                        ytSource.Configuration["cookie"] = YouTubeCookie ?? "";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountSettings] Failed to update YouTube source: {ex.Message}");
            }

            ToastService.ShowToast("Account settings saved.", System.Windows.Media.Brushes.ForestGreen);
        }
    }
}

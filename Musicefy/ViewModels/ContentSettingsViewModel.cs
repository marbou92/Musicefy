using System.Windows.Input;
using Musicefy.Services;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// Sprint 8: ViewModel for the Content settings tab.
    /// Handles Lyrics and Home Screen visibility settings.
    /// </summary>
    public class ContentSettingsViewModel : ViewModelBase
    {
        // ── Lyrics ───────────────────────────────────────────────────────────
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

        // ── Home Screen ──────────────────────────────────────────────────────
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

        public ICommand SaveCommand { get; }

        public ContentSettingsViewModel()
        {
            SaveCommand = new RelayCommand(_ => Save());
        }

        private void Save()
        {
            Musicefy.Properties.Settings.Default.Save();
            ToastService.ShowToast("Content settings saved.", System.Windows.Media.Brushes.ForestGreen);
        }
    }
}

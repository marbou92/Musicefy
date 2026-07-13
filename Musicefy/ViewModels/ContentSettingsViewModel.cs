using System.Windows.Input;
using Musicefy.Services;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// Sprint 8.5: ViewModel for the Content settings tab.
    /// All settings auto-save on change — no Save button.
    /// </summary>
    public class ContentSettingsViewModel : ViewModelBase
    {
        public bool LyricsEnabled
        {
            get => Musicefy.Properties.Settings.Default.LyricsEnabled;
            set { Musicefy.Properties.Settings.Default.LyricsEnabled = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public string LyricsProvider
        {
            get => Musicefy.Properties.Settings.Default.LyricsProvider ?? "LrcLib";
            set { Musicefy.Properties.Settings.Default.LyricsProvider = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public bool ShowLocalOnHome
        {
            get => Musicefy.Properties.Settings.Default.DiscoverLibrary;
            set { Musicefy.Properties.Settings.Default.DiscoverLibrary = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public bool ShowYouTubeOnHome
        {
            get => Musicefy.Properties.Settings.Default.DiscoverYouTube;
            set { Musicefy.Properties.Settings.Default.DiscoverYouTube = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public ContentSettingsViewModel() { }
    }
}

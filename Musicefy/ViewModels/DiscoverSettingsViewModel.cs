using System.Windows.Input;
using Musicefy.Core.Interfaces;
using Musicefy.Properties;

namespace Musicefy.ViewModels
{
    public class DiscoverSettingsViewModel : ViewModelBase
    {
        private bool _showLibrary;
        private bool _showYouTube;
        private bool _showSubsonic;

        public bool ShowLibrary
        {
            get => _showLibrary;
            set { SetProperty(ref _showLibrary, value); }
        }

        public bool ShowYouTube
        {
            get => _showYouTube;
            set { SetProperty(ref _showYouTube, value); }
        }

        public bool ShowSubsonic
        {
            get => _showSubsonic;
            set { SetProperty(ref _showSubsonic, value); }
        }

        public ICommand SaveCommand { get; }
        public ICommand ResetCommand { get; }

        public DiscoverSettingsViewModel()
        {
            Load();
            SaveCommand = new RelayCommand(_ => Save());
            ResetCommand = new RelayCommand(_ => ResetDefaults());
        }

        public void Load()
        {
            ShowLibrary = Settings.Default.DiscoverLibrary;
            ShowYouTube = Settings.Default.DiscoverYouTube;
            ShowSubsonic = Settings.Default.DiscoverSubsonic;
        }

        public void Save()
        {
            Settings.Default.DiscoverLibrary = ShowLibrary;
            Settings.Default.DiscoverYouTube = ShowYouTube;
            Settings.Default.DiscoverSubsonic = ShowSubsonic;
            Settings.Default.Save();
        }

        private void ResetDefaults()
        {
            ShowLibrary = true;
            ShowYouTube = true;
            ShowSubsonic = true;
        }

        public void Cancel()
        {
            Load();
        }
    }
}

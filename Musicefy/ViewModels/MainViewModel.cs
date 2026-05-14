using System.Collections.ObjectModel;

namespace Musicefy.ViewModels
{
    public class MainViewModel
    {
        public ObservableCollection<string> Favourites { get; set; }
        public ObservableCollection<string> Downloads { get; set; }
        public ObservableCollection<string> History { get; set; }

        public string NowPlayingTitle { get; set; }
        public string NowPlayingArtist { get; set; }

        public MainViewModel()
        {
            // Example data
            Favourites = new ObservableCollection<string>();
            Downloads = new ObservableCollection<string>();
            History = new ObservableCollection<string>
            {
                "Sahiba - Aditya Rikhari",
                "Ishqa Ve",
                "Nee Singam Dhan",
                "Pal Pal (with Talwinder)"
            };

            NowPlayingTitle = "Sahiba";
            NowPlayingArtist = "Aditya Rikhari";
        }
    }
}

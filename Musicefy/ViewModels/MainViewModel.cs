using System.Collections.ObjectModel;
using Musicefy.Core.Models;

namespace Musicefy.ViewModels
{
    public class MainViewModel
    {
        // Collections of full MusicFile objects
        public ObservableCollection<MusicFile> Favourites { get; set; }
        public ObservableCollection<MusicFile> Downloads { get; set; }
        public ObservableCollection<MusicFile> History { get; set; }

        // Now Playing track
        public MusicFile NowPlaying { get; set; }

        public MainViewModel()
        {
            // Example data
            Favourites = new ObservableCollection<MusicFile>
            {
                new MusicFile("Sahiba", "Aditya Rikhari", "Single", 2024, genre: "Indie", duration: System.TimeSpan.FromMinutes(3.5)),
                new MusicFile("Ishqa Ve", "Talwinder", "Single", 2023, genre: "Pop", duration: System.TimeSpan.FromMinutes(3))
            };

            Downloads = new ObservableCollection<MusicFile>
            {
                new MusicFile("Nee Singam Dhan", "Artist X", "Album Y", 2022, genre: "Soundtrack", duration: System.TimeSpan.FromMinutes(4)),
                new MusicFile("Pal Pal", "Talwinder", "Collab Album", 2023, genre: "Pop", duration: System.TimeSpan.FromMinutes(3.2))
            };

            History = new ObservableCollection<MusicFile>
            {
                new MusicFile("Sahiba", "Aditya Rikhari"),
                new MusicFile("Ishqa Ve", "Talwinder"),
                new MusicFile("Nee Singam Dhan", "Artist X"),
                new MusicFile("Pal Pal (with Talwinder)", "Artist Y")
            };

            // Set Now Playing
            NowPlaying = new MusicFile("Sahiba", "Aditya Rikhari", "Single", 2024, genre: "Indie", duration: System.TimeSpan.FromMinutes(3.5));
            NowPlaying.MarkPlayed(); // increment play count
        }
    }
}

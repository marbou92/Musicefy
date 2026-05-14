using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using Musicefy.Core.Models;

namespace Musicefy.ViewModels
{
    // New models based on the UI design from Desktop-1.jpg
    public class CategoryItem
    {
        public string Name { get; set; }
    }

    public class ChartCard
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public BitmapImage Cover { get; set; }
    }

    public class TrackCard
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public BitmapImage Cover { get; set; }
    }

    public class VideoCard
    {
        public string Title { get; set; }
        public string Channel { get; set; }
        public BitmapImage Cover { get; set; }
    }

    public class MainViewModel
    {
        // Existing collections of full MusicFile objects
        public ObservableCollection<MusicFile> Favourites { get; set; }
        public ObservableCollection<MusicFile> Downloads { get; set; }
        public ObservableCollection<MusicFile> History { get; set; }

        // Now Playing track
        public MusicFile NowPlaying { get; set; }

        // New properties for Home screen, holding view-specific data
        public ObservableCollection<CategoryItem> HeaderCategories { get; set; }
        public ObservableCollection<ChartCard> BrowseCharts { get; set; }
        public ObservableCollection<TrackCard> QuickPicks { get; set; }
        public ObservableCollection<VideoCard> TopMusicVideos { get; set; }

        public MainViewModel()
        {
            // Initializing existing data
            Favourites = new ObservableCollection<MusicFile>
            {
                new MusicFile("Untitled Track", EnsureArtist(""), EnsureAlbum(""), 0, genre: EnsureGenre(""), duration: TimeSpan.Zero),
                new MusicFile("Untitled Track", EnsureArtist(""), EnsureAlbum(""), 0, genre: EnsureGenre(""), duration: TimeSpan.Zero)
            };

            Downloads = new ObservableCollection<MusicFile>
            {
                new MusicFile("Untitled Track", EnsureArtist(""), EnsureAlbum(""), 0, genre: EnsureGenre(""), duration: TimeSpan.Zero),
                new MusicFile("Untitled Track", EnsureArtist(""), EnsureAlbum(""), 0, genre: EnsureGenre(""), duration: TimeSpan.Zero)
            };

            History = new ObservableCollection<MusicFile>
            {
                new MusicFile("Untitled Track", EnsureArtist(""), EnsureAlbum("")),
                new MusicFile("Untitled Track", EnsureArtist(""), EnsureAlbum("")),
                new MusicFile("Untitled Track", EnsureArtist(""), EnsureAlbum("")),
                new MusicFile("Untitled Track", EnsureArtist(""), EnsureAlbum(""))
            };

            // Set Now Playing
            NowPlaying = new MusicFile("Untitled Track", EnsureArtist(""), EnsureAlbum(""), 0, genre: EnsureGenre(""), duration: TimeSpan.Zero);
            NowPlaying.MarkPlayed(); // increment play count

            // Populate sample data for the new sections, pulled directly from Desktop-1.jpg
            var defaultCoverUri = new Uri("pack://application:,,,/Assets/default_cover.png");
            var defaultCover = new BitmapImage(defaultCoverUri);

            HeaderCategories = new ObservableCollection<CategoryItem>
            {
                new CategoryItem { Name = "Podcasts" },
                new CategoryItem { Name = "Work out" },
                new CategoryItem { Name = "Feel good" },
                new CategoryItem { Name = "Romance" },
                new CategoryItem { Name = "Party" },
                new CategoryItem { Name = "Energise" },
                new CategoryItem { Name = "Relax" },
                new CategoryItem { Name = "Commute" },
                new CategoryItem { Name = "Sad" },
                new CategoryItem { Name = "Focus" },
                new CategoryItem { Name = "Sleep" }
            };

            BrowseCharts = new ObservableCollection<ChartCard>
            {
                new ChartCard { Title = "Spotify Top 50 Global", Subtitle = "Billboard Chart", Cover = defaultCover },
                new ChartCard { Title = "Hot 100", Subtitle = "Billboard Chart", Cover = defaultCover },
                new ChartCard { Title = "Billboard 200", Subtitle = "Billboard Chart", Cover = defaultCover },
                new ChartCard { Title = "Global 200", Subtitle = "Billboard Chart", Cover = defaultCover },
                new ChartCard { Title = "Artist 100", Subtitle = "Billboard Chart", Cover = defaultCover },
                new ChartCard { Title = "Streaming Songs", Subtitle = "Billboard Chart", Cover = defaultCover },
                new ChartCard { Title = "Radio Songs", Subtitle = "Billboard Chart", Cover = defaultCover }
            };

            QuickPicks = new ObservableCollection<TrackCard>
            {
                new TrackCard { Title = "Sahiba", Artist = "Sahiba", Cover = defaultCover },
                new TrackCard { Title = "Pal Pal", Artist = "Pal Pal (with Tajwinder)", Cover = defaultCover },
                new TrackCard { Title = "Gata Only", Artist = "El Alfa & Floyy Menor", Cover = defaultCover },
                new TrackCard { Title = "Sundari", Artist = "Sundari", Cover = defaultCover },
                new TrackCard { Title = "Ishqa Ve", Artist = "Ishqa Ve", Cover = defaultCover },
                new TrackCard { Title = "Arz Kiya Hai | Coke Studio", Artist = "Arz Kiya Hai | Coke Studio Bha...", Cover = defaultCover },
                new TrackCard { Title = "One Love", Artist = "One Love", Cover = defaultCover },
                new TrackCard { Title = "Railin Oliqai (From \"Blue Star\")", Artist = "Railin Oliqai (From \"Blue Star\")", Cover = defaultCover },
                new TrackCard { Title = "Oorum Blood (From \"Dude\")", Artist = "Oorum Blood (From \"Dude\")", Cover = defaultCover },
                new TrackCard { Title = "For A Reason", Artist = "K-POP CULTURE", Cover = defaultCover },
                new TrackCard { Title = "Jaalakaari (From \"Ratti\")", Artist = "Jaalakaari (From \"Ratti\")", Cover = defaultCover },
                new TrackCard { Title = "Finding Her", Artist = "Finding Her", Cover = defaultCover },
                new TrackCard { Title = "Nee Singam Dhan", Artist = "Pathu Thala (Original Motion P...", Cover = defaultCover },
                new TrackCard { Title = "KALYANI", Artist = "KALYANI", Cover = defaultCover },
                new TrackCard { Title = "Vazhithunaiye (From \"Dragon\")", Artist = "Vazhithunaiye (From \"Dragon\")", Cover = defaultCover },
                new TrackCard { Title = "Water", Artist = "Water", Cover = defaultCover }
            };

            TopMusicVideos = new ObservableCollection<VideoCard>
            {
                new VideoCard { Title = "Shararat", Channel = "Madhubanti Bagchi - Jasmine...", Cover = defaultCover },
                new VideoCard { Title = "RANU BOMBAI KI RANU", Channel = "Ranu Bathok - Singer Prabha", Cover = defaultCover },
                new VideoCard { Title = "Ghar Kab Aaoge", Channel = "2 Jan 2026", Cover = defaultCover },
                new VideoCard { Title = "Seet Lehar (Dekh Le)", Channel = "Flimy - Riyaaz", Cover = defaultCover },
                new VideoCard { Title = "Teri Yaadon Ki Chadar", Channel = "Shobhi Sarwan", Cover = defaultCover },
                new VideoCard { Title = "Bayilone Ballipalike", Channel = "Mangli", Cover = defaultCover },
                new VideoCard { Title = "Pal Pal", Channel = "Atosic - AllSoomraMusic", Cover = defaultCover },
                new VideoCard { Title = "Shaky", Channel = "Sanju Rathod - G-SPARK", Cover = defaultCover }
            };
        }

        // Helper methods for ensuring non-null strings
        private string EnsureArtist(string artist) { return string.IsNullOrWhiteSpace(artist) ? "Unknown" : artist; }
        private string EnsureAlbum(string album) { return string.IsNullOrWhiteSpace(album) ? "Unknown Album" : album; }
        private string EnsureGenre(string genre) { return string.IsNullOrWhiteSpace(genre) ? "Unknown Genre" : genre; }
    }
}

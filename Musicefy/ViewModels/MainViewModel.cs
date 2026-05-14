using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using Musicefy.Core.Models;

namespace Musicefy.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<MusicFile> Favourites { get; set; }
        public ObservableCollection<MusicFile> Downloads { get; set; }
        public ObservableCollection<MusicFile> History { get; set; }

        private MusicFile _nowPlaying;
        public MusicFile NowPlaying 
        { 
            get => _nowPlaying; 
            set { _nowPlaying = value; OnPropertyChanged(); } 
        }

        public ObservableCollection<CategoryItem> HeaderCategories { get; set; }
        public ObservableCollection<ChartCard> BrowseCharts { get; set; }
        public ObservableCollection<TrackCard> QuickPicks { get; set; }
        public ObservableCollection<VideoCard> TopMusicVideos { get; set; }

        public MainViewModel()
        {
            InitializeData();
        }

        private void InitializeData()
        {
            var defaultCover = new BitmapImage(new Uri("pack://application:,,,/Assets/default_cover.png"));

            // 1. Categories
            HeaderCategories = new ObservableCollection<CategoryItem>
            {
                new CategoryItem { Name = "Podcasts" }, new CategoryItem { Name = "Work out" },
                new CategoryItem { Name = "Feel good" }, new CategoryItem { Name = "Romance" },
                new CategoryItem { Name = "Relax" }, new CategoryItem { Name = "Focus" }
            };

            // 2. Mock Charts
            BrowseCharts = new ObservableCollection<ChartCard>
            {
                new ChartCard { Title = "Global Top 50", Subtitle = "Daily updates", Cover = defaultCover },
                new ChartCard { Title = "Viral 50", Subtitle = "Trending now", Cover = defaultCover }
            };

            // 3. Quick Picks
            QuickPicks = new ObservableCollection<TrackCard>
            {
                new TrackCard { Title = "Sahiba", Artist = "Sahiba", Cover = defaultCover },
                new TrackCard { Title = "Pal Pal", Artist = "Tajwinder", Cover = defaultCover }
            };

            // 4. Videos
            TopMusicVideos = new ObservableCollection<VideoCard>
            {
                new VideoCard { Title = "New Release", Channel = "Official Vevo", Cover = defaultCover }
            };

            // Existing placeholder logic
            Favourites = new ObservableCollection<MusicFile>();
            History = new ObservableCollection<MusicFile>();
            Downloads = new ObservableCollection<MusicFile>();
        }

        private string EnsureArtist(string artist) => string.IsNullOrWhiteSpace(artist) ? "Unknown" : artist;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    // Support Models
    public class CategoryItem { public string Name { get; set; } }
    public class ChartCard { public string Title { get; set; } public string Subtitle { get; set; } public BitmapImage Cover { get; set; } }
    public class TrackCard { public string Title { get; set; } public string Artist { get; set; } public BitmapImage Cover { get; set; } }
    public class VideoCard { public string Title { get; set; } public string Channel { get; set; } public BitmapImage Cover { get; set; } }
}

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using Musicefy.Core.Models;

namespace Musicefy.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<CategoryItem> HeaderCategories { get; set; }
        public ObservableCollection<ChartCard> BrowseCharts { get; set; }
        public ObservableCollection<TrackCard> QuickPicks { get; set; }
        
        // FIXED: Added the missing collection for the VideosList
        public ObservableCollection<VideoCard> TopMusicVideos { get; set; }

        private MusicFile _nowPlaying;
        public MusicFile NowPlaying 
        { 
            get => _nowPlaying; 
            set { _nowPlaying = value; OnPropertyChanged(); } 
        }

        public MainViewModel()
        {
            var placeholder = new BitmapImage(new Uri("pack://application:,,,/Assets/default_cover.png"));

            BrowseCharts = new ObservableCollection<ChartCard> {
                new ChartCard { Title = "Global Top 50", Subtitle = "Your daily update", Cover = placeholder },
                new ChartCard { Title = "Viral Hits", Subtitle = "Trending on Musicefy", Cover = placeholder }
            };

            QuickPicks = new ObservableCollection<TrackCard> {
                new TrackCard { Title = "Moonlight", Artist = "Lofi Girl", Cover = placeholder },
                new TrackCard { Title = "Midnight City", Artist = "M83", Cover = placeholder }
            };

            // FIXED: Initialized the Video collection
            TopMusicVideos = new ObservableCollection<VideoCard> {
                new VideoCard { Title = "Lofi Hip Hop Radio", Channel = "Lofi Girl", Cover = placeholder },
                new VideoCard { Title = "Synthwave Mix 2026", Channel = "Echo Beats", Cover = placeholder }
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) 
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // Model Classes
    public class CategoryItem { public string Name { get; set; } }
    public class ChartCard { public string Title { get; set; } public string Subtitle { get; set; } public BitmapImage Cover { get; set; } }
    public class TrackCard { public string Title { get; set; } public string Artist { get; set; } public BitmapImage Cover { get; set; } }
    
    // FIXED: Added VideoCard class to match the ViewModel property
    public class VideoCard { public string Title { get; set; } public string Channel { get; set; } public BitmapImage Cover { get; set; } }
}

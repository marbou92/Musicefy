using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Models;
using Musicefy.Core.Services;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// Sprint 7: ViewModel for the Browse (Mood/Genres/Charts) screen.
    /// Shows YouTube Music's trending, new releases, and mood/genre categories.
    /// </summary>
    public class BrowseViewModel : ViewModelBase
    {
        private readonly YouTubeBrowseService _browseService;
        private bool _isLoading;
        private ObservableCollection<MoodGenreCategory> _moodGenres = new ObservableCollection<MoodGenreCategory>();
        private ObservableCollection<MusicFile> _trending = new ObservableCollection<MusicFile>();
        private ObservableCollection<MusicFile> _newReleases = new ObservableCollection<MusicFile>();

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ObservableCollection<MoodGenreCategory> MoodGenres
        {
            get => _moodGenres;
            set => SetProperty(ref _moodGenres, value);
        }

        public ObservableCollection<MusicFile> Trending
        {
            get => _trending;
            set => SetProperty(ref _trending, value);
        }

        public ObservableCollection<MusicFile> NewReleases
        {
            get => _newReleases;
            set => SetProperty(ref _newReleases, value);
        }

        public ICommand RefreshCommand { get; }

        public BrowseViewModel(YouTubeBrowseService browseService)
        {
            _browseService = browseService ?? throw new ArgumentNullException(nameof(browseService));
            RefreshCommand = new RelayCommand(async _ => await LoadAsync());
            _ = LoadAsync();
        }

        public BrowseViewModel() : this(
            App.Services.GetService<YouTubeBrowseService>())
        {
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                // Load all sections in parallel
                var moodsTask = _browseService.GetMoodsAndGenresAsync();
                var trendingTask = _browseService.GetChartTracksAsync("top_songs", 20);
                var newReleasesTask = _browseService.GetNewReleasesAsync(20);

                await Task.WhenAll(moodsTask, trendingTask, newReleasesTask);

                MoodGenres = new ObservableCollection<MoodGenreCategory>(moodsTask.Result);
                Trending = new ObservableCollection<MusicFile>(trendingTask.Result);
                NewReleases = new ObservableCollection<MusicFile>(newReleasesTask.Result);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}

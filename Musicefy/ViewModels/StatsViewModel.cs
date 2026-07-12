using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Musicefy.Core.Services;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// Sprint 5: ViewModel for the Stats screen.
    /// Shows most-played tracks, artists, and albums with period filters.
    /// </summary>
    public class StatsViewModel : ViewModelBase
    {
        private readonly HistoryService _historyService;
        private bool _isLoading;
        private int _selectedPeriodIndex = 1; // 0=7d, 1=30d, 2=90d, 3=all
        private ObservableCollection<StatsEntry> _topTracks = new ObservableCollection<StatsEntry>();
        private ObservableCollection<StatsEntry> _topArtists = new ObservableCollection<StatsEntry>();
        private ObservableCollection<StatsEntry> _topAlbums = new ObservableCollection<StatsEntry>();
        private StatsSummary _summary;

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public int SelectedPeriodIndex
        {
            get => _selectedPeriodIndex;
            set
            {
                if (SetProperty(ref _selectedPeriodIndex, value))
                    _ = LoadAsync();
            }
        }

        public ObservableCollection<StatsEntry> TopTracks
        {
            get => _topTracks;
            set => SetProperty(ref _topTracks, value);
        }

        public ObservableCollection<StatsEntry> TopArtists
        {
            get => _topArtists;
            set => SetProperty(ref _topArtists, value);
        }

        public ObservableCollection<StatsEntry> TopAlbums
        {
            get => _topAlbums;
            set => SetProperty(ref _topAlbums, value);
        }

        public StatsSummary Summary
        {
            get => _summary;
            set => SetProperty(ref _summary, value);
        }

        public ICommand RefreshCommand { get; }

        public StatsViewModel(HistoryService historyService)
        {
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            RefreshCommand = new RelayCommand(async _ => await LoadAsync());
            _ = LoadAsync();
        }

        public StatsViewModel() : this(
            App.Services.GetService<HistoryService>())
        {
        }

        private int GetPeriodDays()
        {
            return _selectedPeriodIndex switch
            {
                0 => 7,
                1 => 30,
                2 => 90,
                3 => 36500, // "all time" ~ 100 years
                _ => 30
            };
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var days = GetPeriodDays();

                var tracks = await _historyService.GetTopTracksAsync(days, 50);
                var artists = await _historyService.GetTopArtistsAsync(days, 20);
                var albums = await _historyService.GetTopAlbumsAsync(days, 20);
                var summary = await _historyService.GetStatsSummaryAsync(days);

                TopTracks = new ObservableCollection<StatsEntry>(tracks);
                TopArtists = new ObservableCollection<StatsEntry>(artists);
                TopAlbums = new ObservableCollection<StatsEntry>(albums);
                Summary = summary;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}

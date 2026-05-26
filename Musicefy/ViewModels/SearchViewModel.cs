using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.Services;

namespace Musicefy.ViewModels
{
    public class SearchViewModel : ViewModelBase
    {
        private readonly PlaybackService _playback;
        private readonly ILibraryService _libraryService;
        private readonly IStreamingSourceManager _sourceManager;
        private string _searchQuery;
        private ObservableCollection<MusicFile> _searchResults = new ObservableCollection<MusicFile>();
        private CancellationTokenSource _debounceCts;
        private bool _hasSearchText;
        private bool _isEmptyStateVisible = true;
        private bool _isNoResultsVisible;
        private bool _isResultsVisible;

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    HasSearchText = !string.IsNullOrWhiteSpace(value);
                    DebounceSearch();
                }
            }
        }

        public ObservableCollection<MusicFile> SearchResults
        {
            get => _searchResults;
            set
            {
                if (SetProperty(ref _searchResults, value))
                    UpdateVisibility();
            }
        }

        public bool HasSearchText
        {
            get => _hasSearchText;
            set => SetProperty(ref _hasSearchText, value);
        }

        public bool IsEmptyStateVisible
        {
            get => _isEmptyStateVisible;
            set => SetProperty(ref _isEmptyStateVisible, value);
        }

        public bool IsNoResultsVisible
        {
            get => _isNoResultsVisible;
            set => SetProperty(ref _isNoResultsVisible, value);
        }

        public bool IsResultsVisible
        {
            get => _isResultsVisible;
            set => SetProperty(ref _isResultsVisible, value);
        }

        public ICommand PlayTrackCommand { get; }

        public SearchViewModel(PlaybackService playback, ILibraryService libraryService, IStreamingSourceManager sourceManager)
        {
            _playback = playback;
            _libraryService = libraryService;
            _sourceManager = sourceManager;
            PlayTrackCommand = new RelayCommand(ExecutePlayTrack);
        }

        private void DebounceSearch()
        {
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            Task.Delay(300, token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                    PerformSearch();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private async void PerformSearch()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                SearchResults.Clear();
                UpdateVisibility();
                return;
            }

            var localResults = await _libraryService.SearchAsync(SearchQuery);

            var sourceResults = await _sourceManager.SearchAllSourcesAsync(SearchQuery);

            var allResults = localResults.Concat(sourceResults).ToList();
            SearchResults = new ObservableCollection<MusicFile>(allResults);
        }

        private void UpdateVisibility()
        {
            bool hasQuery = !string.IsNullOrWhiteSpace(SearchQuery);
            HasSearchText = hasQuery;
            IsEmptyStateVisible = !hasQuery;
            bool hasResults = _searchResults.Count > 0;
            IsNoResultsVisible = hasQuery && !hasResults;
            IsResultsVisible = hasQuery && hasResults;
        }

        private MusicFile _selectedResult;
        public MusicFile SelectedResult
        {
            get => _selectedResult;
            set => SetProperty(ref _selectedResult, value);
        }

        private void ExecutePlayTrack(object parameter)
        {
            if (parameter is MusicFile track)
                _playback.PlayTrack(track);
        }
    }
}

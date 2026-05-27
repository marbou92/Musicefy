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
        private CancellationTokenSource _searchCts;
        private bool _hasSearchText;
        private bool _isEmptyStateVisible = true;
        private bool _isNoResultsVisible;
        private bool _isResultsVisible;
        private bool _isSearching;
        private int _searchCount;

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

        public bool IsSearching
        {
            get => _isSearching;
            set => SetProperty(ref _isSearching, value);
        }

        public string SearchStatus
        {
            get => _searchCount > 0 ? $"{_searchCount} results" : "Searching...";
        }

        public ICommand PlayTrackCommand { get; }

        public SearchViewModel(PlaybackService playback, ILibraryService libraryService, IStreamingSourceManager sourceManager)
        {
            _playback = playback;
            _libraryService = libraryService;
            _sourceManager = sourceManager;
            PlayTrackCommand = new RelayCommand(ExecutePlayTrack);
        }

        private async void DebounceSearch()
        {
            try
            {
                _debounceCts?.Cancel();
                _debounceCts = new CancellationTokenSource();
                var token = _debounceCts.Token;

                try
                {
                    await Task.Delay(200, token);
                    if (!token.IsCancellationRequested)
                        await PerformSearch();
                }
                catch (OperationCanceledException) { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Search debounce failed: {ex}");
            }
        }

        private async Task PerformSearch()
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                SearchResults.Clear();
                UpdateVisibility();
                return;
            }

            IsSearching = true;
            _searchCount = 0;
            SearchResults.Clear();

            try
            {
                var localTask = _libraryService.SearchAsync(SearchQuery, token);
                var sourceTask = _sourceManager.SearchAllSourcesAsync(SearchQuery, token);

                await Task.WhenAll(localTask, sourceTask);

                if (token.IsCancellationRequested) return;

                var allResults = localTask.Result.Concat(sourceTask.Result).ToList();

                // Deduplicate by FilePath
                var seen = new System.Collections.Generic.HashSet<string>();
                foreach (var item in allResults)
                {
                    if (token.IsCancellationRequested) return;
                    if (string.IsNullOrEmpty(item.FilePath) || seen.Add(item.FilePath))
                    {
                        SearchResults.Add(item);
                        _searchCount++;
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                IsSearching = false;
                OnPropertyChanged(nameof(SearchStatus));
                UpdateVisibility();
            }
        }

        private void UpdateVisibility()
        {
            bool hasQuery = !string.IsNullOrWhiteSpace(SearchQuery);
            HasSearchText = hasQuery;
            IsEmptyStateVisible = !hasQuery;
            bool hasResults = _searchResults.Count > 0;
            IsNoResultsVisible = hasQuery && !hasResults && !IsSearching;
            IsResultsVisible = hasQuery && (hasResults || IsSearching);
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

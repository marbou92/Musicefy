using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;

namespace Musicefy.ViewModels
{
    public class SearchViewModel : ViewModelBase, IDisposable
    {
        private readonly IAudioPlayer _playback;
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

        private static readonly ConcurrentDictionary<string, CachedSearchResult> _searchCache
            = new ConcurrentDictionary<string, CachedSearchResult>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromSeconds(30);

        private class CachedSearchResult
        {
            public List<MusicFile> Results { get; set; }
            public DateTime Timestamp { get; set; }
        }

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

        public SearchViewModel(IAudioPlayer playback, ILibraryService libraryService, IStreamingSourceManager sourceManager)
        {
            _playback = playback;
            _libraryService = libraryService;
            _sourceManager = sourceManager;
            PlayTrackCommand = new RelayCommand(ExecutePlayTrack);
        }

        public void SearchNow()
        {
            try { _debounceCts?.Cancel(); } catch (ObjectDisposedException) { }
            _debounceCts?.Dispose();
            _debounceCts = null;
            _ = PerformSearch();
        }

        private async void DebounceSearch()
        {
            try
            {
                try { _debounceCts?.Cancel(); } catch (ObjectDisposedException) { }
                _debounceCts?.Dispose();
                _debounceCts = new CancellationTokenSource();
                var token = _debounceCts.Token;

                try
                {
                    await Task.Delay(120, token);
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
            try { _searchCts?.Cancel(); } catch (ObjectDisposedException) { }
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            var query = SearchQuery?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(query))
            {
                SearchResults.Clear();
                UpdateVisibility();
                return;
            }

            // Check cache
            if (_searchCache.TryGetValue(query, out var cached) && (DateTime.UtcNow - cached.Timestamp) < SearchCacheTtl)
            {
                SearchResults.Clear();
                foreach (var item in cached.Results)
                    SearchResults.Add(item);
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

                var localResults = await localTask;
                var sourceResults = await sourceTask;

                if (token.IsCancellationRequested) return;

                var allResults = localResults.Concat(sourceResults).ToList();

                // Deduplicate by FilePath using batch
                var seen = new System.Collections.Generic.HashSet<string>();
                var batch = new List<MusicFile>(allResults.Count);
                foreach (var item in allResults)
                {
                    if (token.IsCancellationRequested) return;
                    if (string.IsNullOrEmpty(item.FilePath) || seen.Add(item.FilePath))
                    {
                        batch.Add(item);
                        _searchCount++;
                    }
                }

                SearchResults.Clear();
                foreach (var item in batch)
                    SearchResults.Add(item);

                // Store in cache
                _searchCache[query] = new CachedSearchResult
                {
                    Results = batch,
                    Timestamp = DateTime.UtcNow
                };
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

        public void Dispose()
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _searchCts?.Cancel();
            _searchCts?.Dispose();
        }

        private void ExecutePlayTrack(object parameter)
        {
            if (parameter is MusicFile track)
                _playback.PlayTrack(track);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.Models;
using Musicefy.Services;
using PlaybackService = Musicefy.Services.PlaybackService;

namespace Musicefy.ViewModels
{
    public class LibraryViewModel : ViewModelBase
    {
        private readonly IAudioPlayer _playback;
        private readonly ILibraryService _scanner;

        // ── Root cards ───────────────────────────────────────────────────
        public ObservableCollection<LibraryCardItem> RootCards { get; }

        // ── Special collection data ─────────────────────────────────────
        private List<MusicFile> _specialTracks = new List<MusicFile>();

        public ObservableCollection<MusicFile> SpecialTracks { get; } = new ObservableCollection<MusicFile>();

        public void SetSpecialTracks(IEnumerable<MusicFile> tracks)
        {
            SpecialTracks.Clear();
            foreach (var track in tracks)
                SpecialTracks.Add(track);
            OnPropertyChanged(nameof(TrackCountText));
            OnPropertyChanged(nameof(IsEmptyHintVisible));
        }

        private enum SpecialMode { None, Favourites, History, Downloads }
        private SpecialMode _currentMode = SpecialMode.None;

        // ── UI state ────────────────────────────────────────────────────
        private string _headerTitle = "Saved";
        public string HeaderTitle
        {
            get => _headerTitle;
            set { SetProperty(ref _headerTitle, value); }
        }

        private bool _isBackVisible;
        public bool IsBackVisible
        {
            get => _isBackVisible;
            set { SetProperty(ref _isBackVisible, value); }
        }

        private bool _isRefreshVisible;
        public bool IsRefreshVisible
        {
            get => _isRefreshVisible;
            set { SetProperty(ref _isRefreshVisible, value); }
        }

        public string TrackCountText => SpecialTracks.Count == 1
            ? "1 track"
            : $"{SpecialTracks.Count} tracks";

        public Visibility IsEmptyHintVisible => SpecialTracks.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        // Panel visibility
        private Visibility _cardsPanelVisibility = Visibility.Visible;
        public Visibility CardsPanelVisibility
        {
            get => _cardsPanelVisibility;
            set { SetProperty(ref _cardsPanelVisibility, value); }
        }

        private Visibility _specialPanelVisibility = Visibility.Collapsed;
        public Visibility SpecialPanelVisibility
        {
            get => _specialPanelVisibility;
            set { SetProperty(ref _specialPanelVisibility, value); }
        }

        private Visibility _folderPanelVisibility = Visibility.Collapsed;
        public Visibility FolderPanelVisibility
        {
            get => _folderPanelVisibility;
            set { SetProperty(ref _folderPanelVisibility, value); }
        }

        // ── Commands ────────────────────────────────────────────────────
        public ICommand CardClickCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand AddPlaylistCommand { get; }
        public ICommand PlaySelectedTrackCommand { get; }
        public ICommand ToggleFavouriteCommand { get; }
        public ICommand ContextQueueNextCommand { get; }
        public ICommand ContextToggleFavouriteCommand { get; }
        public ICommand ContextShowInExplorerCommand { get; }
        public ICommand DoubleClickTrackCommand { get; }

        public event Action<string> CreatePlaylistRequested;
        public event Action RequestFolderInit;
        public IAudioPlayer PlaybackService => _playback;

        private MusicFile _selectedTrack;
        public MusicFile SelectedTrack
        {
            get => _selectedTrack;
            set { SetProperty(ref _selectedTrack, value); }
        }

        public LibraryViewModel(IAudioPlayer playback, ILibraryService scanner)
        {
            _playback = playback;
            _scanner = scanner;

            RootCards = new ObservableCollection<LibraryCardItem>();
            BuildRootCards();

            CardClickCommand = new RelayCommand(ExecuteCardClick);
            BackCommand = new RelayCommand(ExecuteBack);
            RefreshCommand = new RelayCommand(async _ => await RefreshSpecialCollectionAsync());
            AddPlaylistCommand = new RelayCommand(ExecuteAddPlaylist);
            PlaySelectedTrackCommand = new RelayCommand(_ => PlaySelected());
            ToggleFavouriteCommand = new RelayCommand(async p => await ExecuteToggleFavourite(p));
            ContextQueueNextCommand = new RelayCommand(_ => ExecuteContextQueueNext());
            ContextToggleFavouriteCommand = new RelayCommand(async _ => await ExecuteContextToggleFav());
            ContextShowInExplorerCommand = new RelayCommand(_ => ExecuteShowInExplorer());
            DoubleClickTrackCommand = new RelayCommand(_ => PlaySelected());
        }

        private void BuildRootCards()
        {
            RootCards.Clear();
            RootCards.Add(new LibraryCardItem
            {
                Title = "Favourites", Subtitle = "Liked songs",
                IconData = "M12,21.35L10.55,20.03C5.4,15.36 2,12.27 2,8.5C2,5.41 4.42,3 7.5,3C9.24,3 10.91,3.81 12,5.08C13.09,3.81 14.76,3 16.5,3C19.58,3 22,5.41 22,8.5C22,12.27 18.6,15.36 13.45,20.03L12,21.35Z",
                TargetType = ItemTargetType.Favourites
            });
            RootCards.Add(new LibraryCardItem
            {
                Title = "History", Subtitle = "Recently played",
                IconData = "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4M12.5,7V12.25L17,14.92L16.25,16.15L11,13V7H12.5Z",
                TargetType = ItemTargetType.History
            });
            RootCards.Add(new LibraryCardItem
            {
                Title = "Downloads", Subtitle = "Offline audio",
                IconData = "M5,20H19V18H5V20M19,9H15V3H9V9H5L12,16L19,9Z",
                TargetType = ItemTargetType.Downloads
            });
            RootCards.Add(new LibraryCardItem
            {
                Title = "Folder", Subtitle = "Local directories",
                IconData = "M10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6H12L10,4Z",
                TargetType = ItemTargetType.FolderRoot
            });
        }

        private void ShowPanel(string panel)
        {
            CardsPanelVisibility = panel == "Cards" ? Visibility.Visible : Visibility.Collapsed;
            SpecialPanelVisibility = panel == "Special" ? Visibility.Visible : Visibility.Collapsed;
            FolderPanelVisibility = panel == "Folder" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ExecuteCardClick(object parameter)
        {
            if (!(parameter is LibraryCardItem card)) return;

            switch (card.TargetType)
            {
                case ItemTargetType.Favourites:
                    _currentMode = SpecialMode.Favourites;
                    SetHeaderState("Favourites", true, true);
                    ShowPanel("Special");
                    _ = RefreshSpecialCollectionAsync();
                    break;

                case ItemTargetType.History:
                    _currentMode = SpecialMode.History;
                    SetHeaderState("History", true, true);
                    ShowPanel("Special");
                    _ = RefreshSpecialCollectionAsync();
                    break;

                case ItemTargetType.Downloads:
                    _currentMode = SpecialMode.Downloads;
                    SetHeaderState("Downloads", true, true);
                    ShowPanel("Special");
                    _ = RefreshSpecialCollectionAsync();
                    break;

                case ItemTargetType.FolderRoot:
                    _currentMode = SpecialMode.None;
                    SetHeaderState("Folder Library", true, false);
                    ShowPanel("Folder");
                    RequestFolderInit?.Invoke();
                    break;
            }
        }

        private void SetHeaderState(string title, bool showBack, bool showRefresh)
        {
            HeaderTitle = title;
            IsBackVisible = showBack;
            IsRefreshVisible = showRefresh;
        }

        private async Task RefreshSpecialCollectionAsync()
        {
            List<MusicFile> tracks;
            switch (_currentMode)
            {
                case SpecialMode.Favourites:
                    tracks = await _scanner.GetFavouriteTracksAsync(); break;
                case SpecialMode.History:
                    tracks = await _scanner.GetHistoryTracksAsync(100); break;
                case SpecialMode.Downloads:
                    tracks = await GetDownloadedTracksAsync(); break;
                default:
                    return;
            }

            SetSpecialTracks(tracks);
        }

        private async Task<List<MusicFile>> GetDownloadedTracksAsync(CancellationToken cancellationToken = default)
        {
            string downloadsPath = Musicefy.Properties.Settings.Default.DownloadsPath;
            if (string.IsNullOrWhiteSpace(downloadsPath))
                downloadsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Musicefy", "Downloads");

            if (!Directory.Exists(downloadsPath))
                return new List<MusicFile>();

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return _scanner.ScanDirectory(downloadsPath)
                    .Where(f => f.SourceType == "FileItem").ToList();
            }, cancellationToken);
        }

        public void ResetToRoot()
        {
            _currentMode = SpecialMode.None;
            SetHeaderState("Saved", false, false);
            ShowPanel("Cards");
        }

        private void ExecuteBack()
        {
            ResetToRoot();
        }

        private void ExecuteAddPlaylist()
        {
            CreatePlaylistRequested?.Invoke(null);
        }

        public async Task OnPlaylistNameReceived(string playlistName)
        {
            if (string.IsNullOrWhiteSpace(playlistName)) return;
            try
            {
                await _scanner.CreatePlaylistAsync(playlistName);
                ToastService.ShowToast(
                    $"Playlist \"{playlistName}\" created.",
                    System.Windows.Media.Brushes.ForestGreen);
            }
            catch
            {
                ToastService.ShowToast(
                    "Failed to create playlist.",
                    System.Windows.Media.Brushes.Red);
            }
        }

        private void PlaySelected()
        {
            if (SelectedTrack != null)
            {
                _playback?.PlayTrack(SelectedTrack);
                _ = _scanner.RecordPlayAsync(SelectedTrack.FilePath);
            }
        }

        private async System.Threading.Tasks.Task ExecuteToggleFavourite(object parameter)
        {
            if (!(parameter is MusicFile track)) return;

            await _scanner.ToggleFavouriteAsync(track.FilePath);
            track.IsFavourite = !track.IsFavourite;

            OnPropertyChanged(nameof(SpecialTracks));

            if (_currentMode == SpecialMode.Favourites && !track.IsFavourite)
            {
                SpecialTracks.Remove(track);
                OnPropertyChanged(nameof(TrackCountText));
                OnPropertyChanged(nameof(IsEmptyHintVisible));
            }
        }

        private void ExecuteContextQueueNext()
        {
            if (SelectedTrack == null) return;
            _playback?.EnqueueTrack(SelectedTrack);
            ToastService.ShowToast($"\"{SelectedTrack.Title}\" added to queue.", System.Windows.Media.Brushes.DimGray);
        }

        private async System.Threading.Tasks.Task ExecuteContextToggleFav()
        {
            if (SelectedTrack == null) return;
            await _scanner.ToggleFavouriteAsync(SelectedTrack.FilePath);
            SelectedTrack.IsFavourite = !SelectedTrack.IsFavourite;
        }

        private void ExecuteShowInExplorer()
        {
            if (SelectedTrack?.FilePath != null && File.Exists(SelectedTrack.FilePath))
            {
                var fullPath = Path.GetFullPath(SelectedTrack.FilePath);
                if (fullPath.StartsWith("\\\\")) return;
                Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
            }
        }
    }
}

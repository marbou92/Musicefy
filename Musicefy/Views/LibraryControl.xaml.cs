using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.Services;
using Musicefy.ViewModels;
using PlaybackService = Musicefy.Services.PlaybackService;

namespace Musicefy.Views
{
    public partial class LibraryControl : UserControl
    {
        private LibraryViewModel ViewModel => DataContext as LibraryViewModel;
        private Action _folderInitHandler;
        private Action<string> _createPlaylistHandler;
        private Action<ArtistInfo> _artistNavigationHandler;
        private Action<AlbumInfo> _albumNavigationHandler;
        private Action<PlaylistInfo> _playlistNavigationHandler;
        private Action<MusicFile> _addToPlaylistHandler;

        public LibraryControl()
        {
            InitializeComponent();
            DataContext = App.Services.GetService<LibraryViewModel>();
            Loaded += LibraryControl_Loaded;
            Unloaded += LibraryControl_Unloaded;
        }

        private void LibraryControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.ResetToRoot();
                _folderInitHandler = () =>
                {
                    TrackListDisplayPanel.InitializeDataStream(
                        new System.Collections.Generic.List<MusicFile>(),
                        (PlaybackService)App.Services.GetService(typeof(PlaybackService)));
                };
                _createPlaylistHandler = name =>
                {
                    var dialog = new CreatePlaylistWindow { Owner = Window.GetWindow(this) };
                    if (dialog.ShowDialog() == true)
                        _ = ViewModel.OnPlaylistNameReceived(dialog.ResultPlaylistName);
                };
                _artistNavigationHandler = artist =>
                {
                    if (Window.GetWindow(this) is MainWindow mainWindow)
                        mainWindow.NavigateToArtist(artist);
                };
                _albumNavigationHandler = album =>
                {
                    if (Window.GetWindow(this) is MainWindow mainWindow)
                        mainWindow.NavigateToAlbum(album);
                };
                _playlistNavigationHandler = playlist =>
                {
                    if (Window.GetWindow(this) is MainWindow mainWindow)
                        mainWindow.NavigateToPlaylist(playlist);
                };

                // Phase 6: Add to Playlist handler
                _addToPlaylistHandler = track =>
                {
                    var scanner = App.Services.GetService<ILibraryService>();
                    var dialog = new PlaylistPickerDialog(scanner) { Owner = Window.GetWindow(this) };
                    if (dialog.ShowDialog() == true && dialog.SelectedPlaylist != null)
                    {
                        _ = AddTrackToPlaylistAsync(dialog.SelectedPlaylist.Id, track.FilePath);
                    }
                };

                ViewModel.RequestFolderInit += _folderInitHandler;
                ViewModel.CreatePlaylistRequested += _createPlaylistHandler;
                ViewModel.ArtistNavigationRequested += _artistNavigationHandler;
                ViewModel.AlbumNavigationRequested += _albumNavigationHandler;
                ViewModel.PlaylistNavigationRequested += _playlistNavigationHandler;
                ViewModel.AddToPlaylistRequested += _addToPlaylistHandler;
            }
        }

        private void LibraryControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                if (_folderInitHandler != null)
                    ViewModel.RequestFolderInit -= _folderInitHandler;
                if (_createPlaylistHandler != null)
                    ViewModel.CreatePlaylistRequested -= _createPlaylistHandler;
                if (_artistNavigationHandler != null)
                    ViewModel.ArtistNavigationRequested -= _artistNavigationHandler;
                if (_albumNavigationHandler != null)
                    ViewModel.AlbumNavigationRequested -= _albumNavigationHandler;
                if (_playlistNavigationHandler != null)
                    ViewModel.PlaylistNavigationRequested -= _playlistNavigationHandler;
                if (_addToPlaylistHandler != null)
                    ViewModel.AddToPlaylistRequested -= _addToPlaylistHandler;
            }
            _folderInitHandler = null;
            _createPlaylistHandler = null;
            _artistNavigationHandler = null;
            _albumNavigationHandler = null;
            _playlistNavigationHandler = null;
            _addToPlaylistHandler = null;
        }

        // ── Phase 3: Double-click navigation for artists ──────────────────

        private void ArtistsListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Only navigate if an artist is selected and the click was on an item
            if (ViewModel?.SelectedArtist != null)
            {
                var hitTest = VisualTreeHelper.HitTest(ArtistsListView, e.GetPosition(ArtistsListView));
                if (hitTest != null)
                {
                    ViewModel.NavigateToArtistCommand.Execute(ViewModel.SelectedArtist);
                }
            }
        }

        // ── Phase 3: Double-click navigation for albums ───────────────────

        private void AlbumsListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Only navigate if an album is selected and the click was on an item
            if (ViewModel?.SelectedAlbum != null)
            {
                var hitTest = VisualTreeHelper.HitTest(AlbumsListView, e.GetPosition(AlbumsListView));
                if (hitTest != null)
                {
                    ViewModel.NavigateToAlbumCommand.Execute(ViewModel.SelectedAlbum);
                }
            }
        }

        // ── Phase 5: Double-click navigation for playlists ──────────────────

        private void PlaylistsListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ViewModel?.SelectedPlaylist != null)
            {
                var hitTest = VisualTreeHelper.HitTest(PlaylistsListView, e.GetPosition(PlaylistsListView));
                if (hitTest != null)
                {
                    ViewModel.NavigateToPlaylistCommand.Execute(ViewModel.SelectedPlaylist);
                }
            }
        }

        // ── Phase 6: Add to Playlist helper ──────────────────────────────

        private async Task AddTrackToPlaylistAsync(string playlistId, string trackFilePath)
        {
            try
            {
                var scanner = App.Services.GetService<ILibraryService>();
                await scanner.AddTrackToPlaylistAsync(playlistId, trackFilePath);
                ToastService.ShowToast("Track added to playlist.", System.Windows.Media.Brushes.ForestGreen);
            }
            catch
            {
                ToastService.ShowToast("Failed to add track to playlist.", System.Windows.Media.Brushes.Red);
            }
        }

        // Panel fade transitions (visual-only, stays in code-behind)
        public static readonly DependencyProperty FadeTargetProperty =
            DependencyProperty.Register("FadeTarget", typeof(Visibility), typeof(LibraryControl),
                new PropertyMetadata(Visibility.Collapsed, OnFadeTargetChanged));

        private static void OnFadeTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LibraryControl control && e.NewValue is Visibility vis && vis == Visibility.Visible)
            {
                control.AnimateFadeIn();
            }
        }

        private void AnimateFadeIn()
        {
            var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, fadeIn);
        }
    }
}

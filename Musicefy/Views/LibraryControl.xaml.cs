using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Models;
using Musicefy.Services;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class LibraryControl : UserControl
    {
        private LibraryViewModel ViewModel => DataContext as LibraryViewModel;
        private Action _folderInitHandler;
        private Action<string> _createPlaylistHandler;
        private Action<ArtistInfo> _artistNavigationHandler;
        private Action<AlbumInfo> _albumNavigationHandler;

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

                ViewModel.RequestFolderInit += _folderInitHandler;
                ViewModel.CreatePlaylistRequested += _createPlaylistHandler;
                ViewModel.ArtistNavigationRequested += _artistNavigationHandler;
                ViewModel.AlbumNavigationRequested += _albumNavigationHandler;
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
            }
            _folderInitHandler = null;
            _createPlaylistHandler = null;
            _artistNavigationHandler = null;
            _albumNavigationHandler = null;
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

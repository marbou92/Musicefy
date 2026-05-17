using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Musicefy.Core.Models;
using Musicefy.Services;

namespace Musicefy.Controls
{
    public partial class FolderLibraryControl : UserControl
    {
        private PlaybackService _playbackService;
        private bool _isGridViewActive = false;

        public FolderLibraryControl()
        {
            InitializeComponent();
        }

        public void InitializeDataStream(IEnumerable<MusicFile> tracks, PlaybackService playbackService)
        {
            _playbackService = playbackService;
            
            // Sync the tracks over across both custom UI presentations panels smoothly
            FolderSongsListView.ItemsSource = tracks;
            FolderSongsItemsControl.ItemsSource = tracks;
        }

        private void BtnViewToggle_Click(object sender, RoutedEventArgs e)
        {
            _isGridViewActive = !_isGridViewActive;

            if (_isGridViewActive)
            {
                // Switch Viewport layers over to Card Matrix mode
                FolderSongsListView.Visibility = Visibility.Collapsed;
                FolderSongsGridScrollViewer.Visibility = Visibility.Visible;

                // Update vector shape tokens into rows format bars geometry format
                ToggleIconPath.Data = Geometry.Parse("M3,5H21V7H3V5M3,11H21V13H3V11M3,17H21V19H3V17Z");
            }
            else
            {
                // Restore standard timeline track listing structure view
                FolderSongsGridScrollViewer.Visibility = Visibility.Collapsed;
                FolderSongsListView.Visibility = Visibility.Visible;

                // Restore default grid vector definitions path 
                ToggleIconPath.Data = Geometry.Parse("M4,11H7V5H4V11M4,18H7V12H4V18M8,11H11V5H8V11M8,18H11V12H8V18M12,11H15V5H12V11M12,18H15V12H12V18M16,11H19V5H16V11M16,18H19V12H16V18Z");
            }
        }

        private void GridCardItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is MusicFile targetTrack)
            {
                ExecuteTrackStreamInPipeline(targetTrack, FolderSongsItemsControl.ItemsSource as IEnumerable<MusicFile>);
            }
        }

        private void OnSongDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            if (FolderSongsListView.SelectedItem is MusicFile clickedTrack)
            {
                ExecuteTrackStreamInPipeline(clickedTrack, FolderSongsListView.ItemsSource as IEnumerable<MusicFile>);
            }
        }

        private void ExecuteTrackStreamInPipeline(MusicFile track, IEnumerable<MusicFile> trackScopeList)
        {
            if (track != null && _playbackService != null)
            {
                _playbackService.Queue.Clear();

                if (trackScopeList != null)
                {
                    foreach (var item in trackScopeList)
                    {
                        _playbackService.EnqueueTrack(item);
                    }
                }

                _playbackService.PlayTrack(track);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Musicefy.Core.Models;
using Musicefy.Services;

namespace Musicefy.Controls
{
    public partial class FolderLibraryControl : UserControl
    {
        private PlaybackService _playbackService;

        public FolderLibraryControl()
        {
            InitializeComponent();
        }

        public void InitializeDataStream(IEnumerable<MusicFile> tracks, PlaybackService playbackService)
        {
            _playbackService = playbackService;
            FolderSongsListView.ItemsSource = tracks;
        }

        private void OnSongDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            if (FolderSongsListView.SelectedItem is MusicFile clickedTrack && _playbackService != null)
            {
                // Clear active running dynamic pipeline player queue layers 
                _playbackService.Queue.Clear();

                // Enqueue items seamlessly to support fluent skip / next transport calls later
                if (FolderSongsListView.ItemsSource is IEnumerable<MusicFile> activeList)
                {
                    foreach (var track in activeList)
                    {
                        _playbackService.EnqueueTrack(track);
                    }
                }

                // Dispatches audio file selection direct into NAudio stream outputs
                _playbackService.PlayTrack(clickedTrack);
            }
        }
    }
}

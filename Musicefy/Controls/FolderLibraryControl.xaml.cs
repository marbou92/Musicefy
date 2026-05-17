using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Musicefy.Core.Models;
using Musicefy.Services;

namespace Musicefy.Controls
{
    public partial class FolderLibraryControl : UserControl
    {
        private PlaybackService _playbackService;
        private bool _isGridViewActive = false;
        private string _lastActiveDirectoryPath = null;

        public FolderLibraryControl()
        {
            InitializeComponent();
        }

        public void InitializeDataStream(IEnumerable<MusicFile> tracks, PlaybackService playbackService)
        {
            _playbackService = playbackService;
            UpdateUiCollectionBindingStates(tracks);
        }

        private void UpdateUiCollectionBindingStates(IEnumerable<MusicFile> tracks)
        {
            var trackList = tracks?.ToList() ?? new List<MusicFile>();

            if (trackList.Count == 0)
            {
                EmptyLibraryStateContainer.Visibility = Visibility.Visible;
                ListViewContainer.Visibility = Visibility.Collapsed;
                GridViewContainer.Visibility = Visibility.Collapsed;
            }
            else
            {
                EmptyLibraryStateContainer.Visibility = Visibility.Collapsed;
                if (_isGridViewActive)
                {
                    ListViewContainer.Visibility = Visibility.Collapsed;
                    GridViewContainer.Visibility = Visibility.Visible;
                }
                else
                {
                    GridViewContainer.Visibility = Visibility.Collapsed;
                    ListViewContainer.Visibility = Visibility.Visible;
                }
            }

            FolderSongsListView.ItemsSource = trackList;
            FolderSongsItemsControl.ItemsSource = trackList;
        }

        private async void BtnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Music Library Target Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                string folderPath = Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                {
                    _lastActiveDirectoryPath = folderPath;
                    await ExecuteBackgroundFolderScanAsync(folderPath);
                }
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_lastActiveDirectoryPath) && Directory.Exists(_lastActiveDirectoryPath))
            {
                await ExecuteBackgroundFolderScanAsync(_lastActiveDirectoryPath);
            }
            else
            {
                TriggerEchoToastMessage("No target active folder linked to trigger refresh scanner.");
            }
        }

        /// <summary>
        /// Runs high-volume drive storage file system parsing routines inside isolated task worker threads.
        /// </summary>
        private async Task ExecuteBackgroundFolderScanAsync(string directoryPath)
        {
            string targetFolderName = Path.GetFileName(directoryPath);
            TriggerEchoToastMessage($"Scanning '{targetFolderName}' for songs...");

            // Run execution completely clear of the UI thread context
            List<MusicFile> scannedResults = await Task.Run(() =>
            {
                var scannedTracks = new List<MusicFile>();
                try
                {
                    string[] extensions = { "*.mp3", "*.wav", "*.flac", "*.m4a" };
                    var discoveredFiles = extensions.SelectMany(ext => Directory.GetFiles(directoryPath, ext)).ToList();

                    string artworkCacheFolder = Path.Combine(Path.GetTempPath(), "MusicefyArtworkCache");
                    if (!Directory.Exists(artworkCacheFolder)) Directory.CreateDirectory(artworkCacheFolder);

                    foreach (string file in discoveredFiles)
                    {
                        string fallbackName = Path.GetFileNameWithoutExtension(file);
                        string trackTitle = fallbackName;
                        string trackArtist = "Unknown Artist";
                        string trackAlbum = "Local Stream Layer";
                        string trackCoverImageReference = null;
                        TimeSpan trackDuration = TimeSpan.Zero;

                        try
                        {
                            using (var reader = new NAudio.Wave.AudioFileReader(file))
                            {
                                trackDuration = reader.TotalTime;
                            }

                            using (var tagContainer = TagLib.File.Create(file))
                            {
                                if (tagContainer.Tag != null)
                                {
                                    if (!string.IsNullOrEmpty(tagContainer.Tag.Title)) trackTitle = tagContainer.Tag.Title;
                                    if (!string.IsNullOrEmpty(tagContainer.Tag.FirstPerformer)) trackArtist = tagContainer.Tag.FirstPerformer;
                                    if (!string.IsNullOrEmpty(tagContainer.Tag.Album)) trackAlbum = tagContainer.Tag.Album;

                                    if (tagContainer.Tag.Pictures != null && tagContainer.Tag.Pictures.Length > 0)
                                    {
                                        string safeHashName = "cover_" + Math.Abs(file.GetHashCode()).ToString() + ".jpg";
                                        string writeImgPath = Path.Combine(artworkCacheFolder, safeHashName);

                                        if (!File.Exists(writeImgPath))
                                        {
                                            File.WriteAllBytes(writeImgPath, tagContainer.Tag.Pictures[0].Data.Data);
                                        }
                                        trackCoverImageReference = writeImgPath;
                                    }
                                }
                            }
                        }
                        catch { trackTitle = fallbackName; }

                        // Sanitize string references to strip Windows 7 text boxes
                        trackTitle = Regex.Replace(trackTitle, @"[\x00-\x1F\x7F-\x9F]", "").Trim();
                        trackArtist = Regex.Replace(trackArtist, @"[\x00-\x1F\x7F-\x9F]", "").Trim();

                        if (string.IsNullOrEmpty(trackTitle)) trackTitle = fallbackName;
                        if (string.IsNullOrEmpty(trackArtist)) trackArtist = "Unknown Artist";

                        scannedTracks.Add(new MusicFile
                        {
                            Title = trackTitle,
                            Artist = trackArtist,
                            Album = trackAlbum,
                            FilePath = file,
                            SourceUri = file,
                            SourceType = "Local",
                            Duration = trackDuration,
                            CoverPath = trackCoverImageReference
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Background indexing crashed context path: {ex.Message}");
                }

                return scannedTracks;
            });

            // Update UI collections cleanly on completion
            UpdateUiCollectionBindingStates(scannedResults);
            TriggerEchoToastMessage($"Successfully indexed {scannedResults.Count} tracks.");
        }

        /// <summary>
        /// Premium Dropdown Overlay Animator Panel Control Routine
        /// </summary>
        private void TriggerEchoToastMessage(string msg)
        {
            TxtToastMessage.Text = msg;
            ToastNotificationCard.Visibility = Visibility.Visible;

            // Fluid exponential drop-down acceleration curves
            var slideDown = new DoubleAnimation(-40, 12, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
            };

            var slideUp = new DoubleAnimation(12, -40, TimeSpan.FromMilliseconds(350))
            {
                BeginTime = TimeSpan.FromSeconds(3.0),
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseIn }
            };

            slideUp.Completed += (s, ev) =>
            {
                ToastNotificationCard.Visibility = Visibility.Collapsed;
            };

            ToastTranslation.BeginAnimation(TranslateTransform.YProperty, slideDown);
            // Queue up the slide-away collapse animation sequence link automatically
            ToastTranslation.BeginAnimation(TranslateTransform.YProperty, slideUp);
        }

        #region Nav View Conversions Controllers 
        private void BtnViewToggle_Click(object sender, RoutedEventArgs e)
        {
            _isGridViewActive = !_isGridViewActive;
            UpdateUiCollectionBindingStates(FolderSongsListView.ItemsSource as IEnumerable<MusicFile>);
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
                    foreach (var item in trackScopeList) _playbackService.EnqueueTrack(item);
                }
                _playbackService.PlayTrack(track);
            }
        }
        #endregion
    }
}

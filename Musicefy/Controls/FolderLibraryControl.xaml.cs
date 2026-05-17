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
        private bool _isGridViewActive = true;
        private string _lastActiveDirectoryPath = null;

        public FolderLibraryControl()
        {
            InitializeComponent();
            
            // PERSISTENCE ENGINE: Automatically reload the saved folder path when the user enters this tab
            Loaded += FolderLibraryControl_Loaded;
        }

        private async void FolderLibraryControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Pull the saved file path string directly out of application setting properties configuration
                string savedPath = LibraryControlSettings.Default.LastSelectedFolderPath;
                
                if (!string.IsNullOrEmpty(savedPath) && Directory.Exists(savedPath))
                {
                    _lastActiveDirectoryPath = savedPath;
                    await ExecuteBackgroundFolderScanAsync(savedPath);
                }
            }
            catch
            {
                // Fallback gracefully if configuration namespaces are still initializing on boot
            }
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
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select Music Library Target Folder (All nested inner subfolders will be safely included)";
                dialog.ShowNewFolderButton = false;
                dialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string folderPath = dialog.SelectedPath;
                    if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                    {
                        _lastActiveDirectoryPath = folderPath;

                        // PERSISTENCE ENGINE: Save the folder path to settings storage immediately
                        try
                        {
                            LibraryControlSettings.Default.LastSelectedFolderPath = folderPath;
                            LibraryControlSettings.Default.Save();
                        }
                        catch { }

                        await ExecuteBackgroundFolderScanAsync(folderPath);
                    }
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
                TriggerEchoToastMessage("No active folder linked to refresh scanner.");
            }
        }

        private async Task ExecuteBackgroundFolderScanAsync(string directoryPath)
        {
            string targetFolderName = Path.GetFileName(directoryPath);
            TriggerEchoToastMessage($"Scanning '{targetFolderName}' and subfolders safely...");

            List<MusicFile> scannedResults = await Task.Run(() =>
            {
                var scannedTracks = new List<MusicFile>();
                var filesToProcess = new List<string>();

                try
                {
                    // FIXED: Safe recursive directory tracking loop bypasses locked system files without crashing out the app loop
                    SafeRecursiveFileSearch(directoryPath, filesToProcess);

                    string artworkCacheFolder = Path.Combine(Path.GetTempPath(), "MusicefyArtworkCache");
                    if (!Directory.Exists(artworkCacheFolder)) Directory.CreateDirectory(artworkCacheFolder);

                    foreach (string file in filesToProcess)
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
                    System.Diagnostics.Debug.WriteLine($"Deep scanning failed: {ex.Message}");
                }

                return scannedTracks;
            });

            UpdateUiCollectionBindingStates(scannedResults);
            TriggerEchoToastMessage($"Successfully indexed {scannedResults.Count} tracks.");
        }

        /// <summary>
        /// FIXED: Safely crawls subdirectories, catching and skipping restricted system paths effortlessly.
        /// </summary>
        private void SafeRecursiveFileSearch(string rootDirectory, List<string> accumulatedFiles)
        {
            string[] audioExtensions = { ".mp3", ".wav", ".flac", ".m4a" };

            try
            {
                // Grabs audio files in the current folder tier
                foreach (string file in Directory.GetFiles(rootDirectory))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (audioExtensions.Contains(ext))
                    {
                        accumulatedFiles.Add(file);
                    }
                }

                // Recursively drill down into subdirectories while safely bypassing access violation errors
                foreach (string directory in Directory.GetDirectories(rootDirectory))
                {
                    // Skip hidden Windows directory nodes like AppData or system data structures
                    var dirInfo = new DirectoryInfo(directory);
                    if ((dirInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;

                    SafeRecursiveFileSearch(directory, accumulatedFiles);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Gracefully skips hidden system folders without breaking the rest of the directory tree scan
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Directory skip block: {ex.Message}");
            }
        }

        private void TriggerEchoToastMessage(string msg)
        {
            TxtToastMessage.Text = msg;
            ToastNotificationCard.Visibility = Visibility.Visible;

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
            ToastTranslation.BeginAnimation(TranslateTransform.YProperty, slideUp);
        }

        private void BtnViewToggle_Click(object sender, RoutedEventArgs e)
        {
            _isGridViewActive = !_isGridViewActive;
            
            if (_isGridViewActive)
            {
                ListViewContainer.Visibility = Visibility.Collapsed;
                GridViewContainer.Visibility = Visibility.Visible;
                ToggleIconPath.Data = Geometry.Parse("M3,5H21V7H3V5M3,11H21V13H3V11M3,17H21V19H3V17Z");
            }
            else
            {
                GridViewContainer.Visibility = Visibility.Collapsed;
                ListViewContainer.Visibility = Visibility.Visible;
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
                    foreach (var item in trackScopeList) _playbackService.EnqueueTrack(item);
                }
                _playbackService.PlayTrack(track);
            }
        }
    }

    /// <summary>
    /// FIXED: Fully qualified the DebuggerNonUserCode namespace definition path to pass MSBuild checks smoothly
    /// </summary>
    public class LibraryControlSettings : System.Configuration.ApplicationSettingsBase
    {
        private static LibraryControlSettings defaultInstance = ((LibraryControlSettings)(System.Configuration.ApplicationSettingsBase.Synchronized(new LibraryControlSettings())));

        public static LibraryControlSettings Default => defaultInstance;

        [System.Configuration.UserScopedSettingAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()] // FIXED: Now points directly to System.Diagnostics context channels
        [System.Configuration.DefaultSettingValueAttribute("")]
        public string LastSelectedFolderPath
        {
            get => ((string)(this["LastSelectedFolderPath"]));
            set => this["LastSelectedFolderPath"] = value;
        }
    }
}

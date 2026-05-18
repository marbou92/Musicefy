using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        
        private string _rootLibraryPath = null;
        private string _currentBrowsingDirectoryPath = null;
        private readonly List<MusicFile> _currentLevelItemsCollection = new List<MusicFile>();

        public FolderLibraryControl()
        {
            InitializeComponent();
            Loaded += FolderLibraryControl_Loaded;
        }

        private void FolderLibraryControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Assuming LibraryControlSettings is a global settings class in your app
            string savedPath = LibraryControlSettings.Default.LastSelectedFolderPath;
            if (!string.IsNullOrEmpty(savedPath) && Directory.Exists(savedPath))
            {
                _rootLibraryPath = savedPath;
                BtnClearFolder.Visibility = Visibility.Visible;
                RenderRootLibraryHubView();
            }
            else
            {
                BtnClearFolder.Visibility = Visibility.Collapsed;
                UpdateUiCollectionBindingStates(new List<MusicFile>());
            }
        }

        public void InitializeDataStream(IEnumerable<MusicFile> tracks, PlaybackService playbackService)
        {
            _playbackService = playbackService;
            if (!string.IsNullOrEmpty(_rootLibraryPath))
            {
                if (string.IsNullOrEmpty(_currentBrowsingDirectoryPath))
                {
                    RenderRootLibraryHubView();
                }
                else
                {
                    NavigateToTargetDirectoryFolder(_currentBrowsingDirectoryPath);
                }
            }
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
            
            this.UpdateLayout();
            TriggerFluidLayoutEntranceAnimation();
        }

        private void RenderRootLibraryHubView()
        {
            _currentBrowsingDirectoryPath = null;

            if (BtnFolderBack.Visibility == Visibility.Visible)
            {
                var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(150)));
                fadeOut.Completed += (s, e) => { BtnFolderBack.Visibility = Visibility.Collapsed; };
                BtnFolderBack.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }

            _currentLevelItemsCollection.Clear();

            if (!string.IsNullOrEmpty(_rootLibraryPath) && Directory.Exists(_rootLibraryPath))
            {
                _currentLevelItemsCollection.Add(new MusicFile
                {
                    Title = Path.GetFileName(_rootLibraryPath),
                    Artist = "Root Hub Folder Link",
                    SourceType = "FolderItem",
                    FilePath = _rootLibraryPath,
                    SourceUri = _rootLibraryPath
                });
            }

            UpdateUiCollectionBindingStates(_currentLevelItemsCollection);
        }

        private void NavigateToTargetDirectoryFolder(string targetPath)
        {
            if (!Directory.Exists(targetPath)) return;
            _currentBrowsingDirectoryPath = targetPath;

            if (BtnFolderBack.Visibility != Visibility.Visible)
            {
                BtnFolderBack.Visibility = Visibility.Visible;
                var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200)));
                BtnFolderBack.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }

            _currentLevelItemsCollection.Clear();

            try
            {
                foreach (string subDir in Directory.GetDirectories(targetPath))
                {
                    var dirInfo = new DirectoryInfo(subDir);
                    if ((dirInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;

                    _currentLevelItemsCollection.Add(new MusicFile
                    {
                        Title = Path.GetFileName(subDir),
                        Artist = "Folder Container",
                        SourceType = "FolderItem",
                        FilePath = subDir,
                        SourceUri = subDir
                    });
                }

                string[] validExtensions = { ".mp3", ".wav", ".flac", ".m4a" };
                string artworkCacheFolder = Path.Combine(Path.GetTempPath(), "MusicefyArtworkCache");

                foreach (string file in Directory.GetFiles(targetPath))
                {
                    if (validExtensions.Contains(Path.GetExtension(file).ToLower()))
                    {
                        string cleanTitle = Path.GetFileNameWithoutExtension(file);
                        string trackArtist = "Unknown Artist";
                        string trackAlbum = "Local Stream Layer";
                        string detectedArtworkPath = null;
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
                                    if (!string.IsNullOrEmpty(tagContainer.Tag.Title)) cleanTitle = tagContainer.Tag.Title;
                                    if (!string.IsNullOrEmpty(tagContainer.Tag.FirstPerformer)) trackArtist = tagContainer.Tag.FirstPerformer;
                                    if (!string.IsNullOrEmpty(tagContainer.Tag.Album)) trackAlbum = tagContainer.Tag.Album;

                                    string possibleCacheFile = Path.Combine(artworkCacheFolder, "cover_" + Math.Abs(file.GetHashCode()).ToString() + ".jpg");
                                    if (File.Exists(possibleCacheFile))
                                    {
                                        detectedArtworkPath = possibleCacheFile;
                                    }
                                }
                            }
                        }
                        catch { }

                        // Completed Regex and assignment
                        _currentLevelItemsCollection.Add(new MusicFile
                        {
                            Title = Regex.Replace(cleanTitle, @"[\x00-\x1F\x7F-\x9F]", "").Trim(),
                            Artist = Regex.Replace(trackArtist, @"[\x00-\x1F\x7F-\x9F]", "").Trim(),
                            Duration = trackDuration,
                            SourceType = "FileItem",
                            FilePath = file,
                            SourceUri = file,
                            CoverPath = detectedArtworkPath
                        });
                    }
                }
            }
            catch (UnauthorizedAccessException) { /* Silently bypass folders without read permission */ }

            UpdateUiCollectionBindingStates(_currentLevelItemsCollection);
        }

        private void TriggerFluidLayoutEntranceAnimation()
        {
            var transform = _isGridViewActive ? GridTranslate : ListTranslate;
            var element = _isGridViewActive ? (UIElement)GridViewContainer : ListViewContainer;

            transform.Y = 20;
            element.Opacity = 0;

            var slideIn = new DoubleAnimation(20, 0, new Duration(TimeSpan.FromMilliseconds(400)))
            {
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
            };
            
            var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(300)));

            transform.BeginAnimation(TranslateTransform.YProperty, slideIn);
            element.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        // --- Event Handlers ---

        private void BtnFolderBack_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentBrowsingDirectoryPath))
            {
                var parentDir = Directory.GetParent(_currentBrowsingDirectoryPath);
                if (parentDir != null && parentDir.FullName.StartsWith(_rootLibraryPath, StringComparison.OrdinalIgnoreCase))
                {
                    NavigateToTargetDirectoryFolder(parentDir.FullName);
                }
                else
                {
                    RenderRootLibraryHubView();
                }
            }
        }

        private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select a local music folder to add to your Musicefy library";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _rootLibraryPath = dialog.SelectedPath;
                    LibraryControlSettings.Default.LastSelectedFolderPath = _rootLibraryPath;
                    LibraryControlSettings.Default.Save();
                    
                    BtnClearFolder.Visibility = Visibility.Visible;
                    RenderRootLibraryHubView();
                }
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentBrowsingDirectoryPath))
                NavigateToTargetDirectoryFolder(_currentBrowsingDirectoryPath);
            else if (!string.IsNullOrEmpty(_rootLibraryPath))
                RenderRootLibraryHubView();
        }

        private void BtnClearFolder_Click(object sender, RoutedEventArgs e)
        {
            _rootLibraryPath = null;
            _currentBrowsingDirectoryPath = null;
            LibraryControlSettings.Default.LastSelectedFolderPath = string.Empty;
            LibraryControlSettings.Default.Save();

            BtnClearFolder.Visibility = Visibility.Collapsed;
            BtnFolderBack.Visibility = Visibility.Collapsed;
            
            _currentLevelItemsCollection.Clear();
            UpdateUiCollectionBindingStates(_currentLevelItemsCollection);
        }

        private void BtnViewToggle_Click(object sender, RoutedEventArgs e)
        {
            _isGridViewActive = !_isGridViewActive;
            
            // Switch Icon Path Data
            ToggleIconPath.Data = _isGridViewActive 
                ? Geometry.Parse("M3,5H21V7H3V5M3,11H21V13H3V11M3,17H21V19H3V17Z") // List Icon 
                : Geometry.Parse("M3,3H11V11H3V3M13,3H21V11H13V3M3,13H11V21H3V13M13,13H21V21H13V13Z"); // Grid Icon

            UpdateUiCollectionBindingStates(_currentLevelItemsCollection);
        }

        private void OnSongDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            if (FolderSongsListView.SelectedItem is MusicFile selectedItem)
            {
                HandleItemSelection(selectedItem);
            }
        }

        private void GridCardItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is MusicFile selectedItem)
            {
                HandleItemSelection(selectedItem);
            }
        }

        private void HandleItemSelection(MusicFile item)
        {
            if (item.SourceType == "FolderItem")
            {
                NavigateToTargetDirectoryFolder(item.FilePath);
            }
            else if (item.SourceType == "FileItem")
            {
                _playbackService?.PlayTrack(item);
            }
        }
    }

    // Restored the missing configuration settings wrapper
    public class LibraryControlSettings : System.Configuration.ApplicationSettingsBase
    {
        private static LibraryControlSettings defaultInstance = ((LibraryControlSettings)(System.Configuration.ApplicationSettingsBase.Synchronized(new LibraryControlSettings())));
        public static LibraryControlSettings Default => defaultInstance;

        [System.Configuration.UserScopedSettingAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.Configuration.DefaultSettingValueAttribute("")]
        public string LastSelectedFolderPath
        {
            get => ((string)(this["LastSelectedFolderPath"]));
            set => this["LastSelectedFolderPath"] = value;
        }
    }
}

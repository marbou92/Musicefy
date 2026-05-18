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
            
            // FIXED: Force immediate device pixel calculations over tracking boundaries
            // before layout engine animation vectors lock up rendering inputs.
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

                        _currentLevelItemsCollection.Add(new MusicFile
                        {
                            Title = Regex.Replace(cleanTitle, @"[\x00-\x1F\x7F-\x9F]", "").Trim(),
                            Artist = Regex.Replace(trackArtist, @"[\x00-\x1F\x7F-\x9F]", "").Trim(),
                            Album = trackAlbum,
                            FilePath = file,
                            SourceUri = file,
                            Duration = trackDuration,
                            CoverPath = detectedArtworkPath,
                            SourceType = "AudioItem"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Traversing file paths error: {ex.Message}");
            }

            UpdateUiCollectionBindingStates(_currentLevelItemsCollection);
        }

        private void TriggerFluidLayoutEntranceAnimation()
        {
            var targetElement = _isGridViewActive ? (UIElement)GridViewContainer : (UIElement)ListViewContainer;
            var targetTransform = _isGridViewActive ? GridTranslate : ListTranslate;

            targetElement.Opacity = 0;
            targetTransform.Y = 24;

            var fadeAnim = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(350)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var slideAnim = new DoubleAnimation(24, 0, new Duration(TimeSpan.FromMilliseconds(400)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            targetElement.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            targetTransform.BeginAnimation(TranslateTransform.YProperty, slideAnim);
        }

        private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "Select Music Library Folder Node Target Hub Link";
                dialog.ShowNewFolderButton = false;
                
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _rootLibraryPath = dialog.SelectedPath;
                    BtnClearFolder.Visibility = Visibility.Visible;
                    
                    LibraryControlSettings.Default.LastSelectedFolderPath = _rootLibraryPath;
                    LibraryControlSettings.Default.Save();

                    RenderRootLibraryHubView();
                    TriggerEchoToastMessage("Linked root folder successfully.");
                }
            }
        }

        private void BtnClearFolder_Click(object sender, RoutedEventArgs e)
        {
            _rootLibraryPath = null;
            _currentBrowsingDirectoryPath = null;
            _currentLevelItemsCollection.Clear();

            LibraryControlSettings.Default.LastSelectedFolderPath = string.Empty;
            LibraryControlSettings.Default.Save();

            BtnClearFolder.Visibility = Visibility.Collapsed;
            BtnFolderBack.Visibility = Visibility.Collapsed;

            UpdateUiCollectionBindingStates(new List<MusicFile>());
            TriggerEchoToastMessage("Cleared target folder registry memory successfully.");
        }

        private void BtnFolderBack_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentBrowsingDirectoryPath)) return;

            if (_currentBrowsingDirectoryPath.Equals(_rootLibraryPath, StringComparison.OrdinalIgnoreCase))
            {
                RenderRootLibraryHubView();
                return;
            }

            string parentPath = Path.GetDirectoryName(_currentBrowsingDirectoryPath);
            if (!string.IsNullOrEmpty(parentPath) && Directory.Exists(parentPath))
            {
                if (parentPath.Equals(Path.GetDirectoryName(_rootLibraryPath), StringComparison.OrdinalIgnoreCase))
                {
                    RenderRootLibraryHubView();
                }
                else
                {
                    NavigateToTargetDirectoryFolder(parentPath);
                }
            }
            else
            {
                RenderRootLibraryHubView();
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentBrowsingDirectoryPath))
            {
                NavigateToTargetDirectoryFolder(_currentBrowsingDirectoryPath);
                TriggerEchoToastMessage("Refreshed layout tree channels mapping.");
            }
            else if (!string.IsNullOrEmpty(_rootLibraryPath))
            {
                RenderRootLibraryHubView();
                TriggerEchoToastMessage("Refreshed root catalog deck.");
            }
        }

        private void GridCardItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is MusicFile item)
            {
                HandleItemSelectionActivation(item);
            }
        }

        private void OnSongDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            if (FolderSongsListView.SelectedItem is MusicFile item)
            {
                HandleItemSelectionActivation(item);
            }
        }

        private void HandleItemSelectionActivation(MusicFile item)
        {
            if (item == null) return;

            if (item.SourceType == "FolderItem")
            {
                NavigateToTargetDirectoryFolder(item.FilePath);
            }
            else if (item.SourceType == "AudioItem" && _playbackService != null)
            {
                _playbackService.Queue.Clear();
                foreach (var track in _currentLevelItemsCollection.Where(x => x.SourceType == "AudioItem"))
                {
                    _playbackService.EnqueueTrack(track);
                }
                _playbackService.PlayTrack(item);
            }
        }

        private void BtnViewToggle_Click(object sender, RoutedEventArgs e)
        {
            _isGridViewActive = !_isGridViewActive;
            
            if (_isGridViewActive)
            {
                ToggleIconPath.Data = Geometry.Parse("M3,5H21V7H3V5M3,11H21V13H3V11M3,17H21V19H3V17Z");
            }
            else
            {
                ToggleIconPath.Data = Geometry.Parse("M4,11H7V5H4V11M4,18H7V12H4V18M8,11H11V5H8V11M8,18H11V12H8V18M12,11H15V5H12V11M12,18H15V12H12V18M16,11H19V5H16V11M16,18H19V12H16V18Z");
            }

            UpdateUiCollectionBindingStates(_currentLevelItemsCollection);
        }

        private void TriggerEchoToastMessage(string msg)
        {
            TxtToastMessage.Text = msg;
            ToastNotificationCard.Visibility = Visibility.Visible;

            var slideDown = new DoubleAnimation(-40, 12, TimeSpan.FromMilliseconds(400)) { EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut } };
            var slideUp = new DoubleAnimation(12, -40, TimeSpan.FromMilliseconds(350)) { BeginTime = TimeSpan.FromSeconds(2.5), EasingFunction = new CircleEase { EasingMode = EasingMode.EaseIn } };
            slideUp.Completed += (s, ev) => { ToastNotificationCard.Visibility = Visibility.Collapsed; };

            ToastTranslation.BeginAnimation(TranslateTransform.YProperty, slideDown);
            ToastTranslation.BeginAnimation(TranslateTransform.YProperty, slideUp);
        }
    }

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

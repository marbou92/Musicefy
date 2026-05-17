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
                BtnClearFolder.Visibility = Visibility.Visible; // Show clear button if path exists
                NavigateToTargetDirectoryFolder(savedPath);
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
                NavigateToTargetDirectoryFolder(_currentBrowsingDirectoryPath ?? _rootLibraryPath);
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
        }

        private void NavigateToTargetDirectoryFolder(string targetPath)
        {
            if (!Directory.Exists(targetPath)) return;
            _currentBrowsingDirectoryPath = targetPath;

            BtnFolderBack.Visibility = (targetPath.Equals(_rootLibraryPath, StringComparison.OrdinalIgnoreCase)) 
                ? Visibility.Collapsed : Visibility.Visible;

            _currentLevelItemsCollection.Clear();

            try
            {
                // 1. Fetch subdirectories
                foreach (string subDir in Directory.GetDirectories(targetPath))
                {
                    var dirInfo = new DirectoryInfo(subDir);
                    if ((dirInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;

                    _currentLevelItemsCollection.Add(new MusicFile
                    {
                        Title = Path.GetFileName(subDir),
                        Artist = "Subfolder",
                        SourceType = "FolderItem", // XAML trigger token flag
                        FilePath = subDir,
                        SourceUri = subDir
                    });
                }

                // 2. Fetch audio files and resolve artwork paths
                string[] validExtensions = { ".mp3", "*.wav", ".flac", ".m4a" };
                string artworkCacheFolder = Path.Combine(Path.GetTempPath(), "MusicefyArtworkCache");

                foreach (string file in Directory.GetFiles(targetPath))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext == ".mp3" || ext == ".wav" || ext == ".flac" || ext == ".m4a")
                    {
                        string cleanTitle = Path.GetFileNameWithoutExtension(file);
                        string detectedArtworkPath = null;

                        // Check if an artwork file has already been cached for this track hash code
                        string possibleCacheFile = Path.Combine(artworkCacheFolder, "cover_" + Math.Abs(file.GetHashCode()).ToString() + ".jpg");
                        if (File.Exists(possibleCacheFile))
                        {
                            detectedArtworkPath = possibleCacheFile;
                        }

                        _currentLevelItemsCollection.Add(new MusicFile
                        {
                            Title = Regex.Replace(cleanTitle, @"[\x00-\x1F\x7F-\x9F]", "").Trim(),
                            Artist = "Local Audio",
                            SourceType = "AudioItem",
                            FilePath = file,
                            SourceUri = file,
                            CoverPath = detectedArtworkPath // Feeds explicit disk path strings directly into XAML data templates
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Directory exploration sweep failed: {ex.Message}");
            }

            RenderExplorerViewDeck();
        }

        private void RenderExplorerViewDeck()
        {
            UpdateUiCollectionBindingStates(_currentLevelItemsCollection);
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

                    NavigateToTargetDirectoryFolder(_rootLibraryPath);
                    TriggerEchoToastMessage("Linked root folder successfully.");
                }
            }
        }

        // NEW: Clear configuration database registries on demand instantly
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
            if (string.IsNullOrEmpty(_currentBrowsingDirectoryPath) || _currentBrowsingDirectoryPath.Equals(_rootLibraryPath, StringComparison.OrdinalIgnoreCase)) return;

            string parentPath = Path.GetDirectoryName(_currentBrowsingDirectoryPath);
            if (!string.IsNullOrEmpty(parentPath) && Directory.Exists(parentPath))
            {
                NavigateToTargetDirectoryFolder(parentPath);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentBrowsingDirectoryPath))
            {
                NavigateToTargetDirectoryFolder(_currentBrowsingDirectoryPath);
                TriggerEchoToastMessage("Refreshed layout tree channels mapping.");
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

            RenderExplorerViewDeck();
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

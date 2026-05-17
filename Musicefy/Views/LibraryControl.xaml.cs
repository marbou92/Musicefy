using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Musicefy.Core.Models;
using Musicefy.Models;
using Musicefy.Services;

namespace Musicefy.Views
{
    public partial class LibraryControl : UserControl
    {
        public ObservableCollection<LibraryCardItem> DisplayItems { get; set; } = new ObservableCollection<LibraryCardItem>();
        private readonly List<LibraryCardItem> _rootLibraryItems = new List<LibraryCardItem>();
        private readonly Stack<string> _folderNavigationHistory = new Stack<string>();
        private readonly PlaybackService _playbackService;
        private bool _isInsideFolderBrowsingMode = false;

        private readonly Dictionary<string, List<MusicFile>> _directoryTrackCache = new Dictionary<string, List<MusicFile>>();

        private const string IconHeart = "M12,21.35L10.55,20.03C5.4,15.36 2,12.27 2,8.5C2,5.41 4.42,3 7.5,3C9.24,3 10.91,3.81 12,5.08C13.09,3.81 14.76,3 16.5,3C19.58,3 22,5.41 22,8.5C22,12.27 18.6,15.36 13.45,20.03L12,21.35Z";
        private const string IconDownload = "M5,20H19V18H5V20M19,9H15V3H9V9H5L12,16L19,9Z";
        private const string IconHistory = "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4M12.5,7V12.25L17,14.92L16.25,16.15L11,13V7H12.5Z";
        private const string IconFolder = "M10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6H12L10,4Z";

        public LibraryControl(object optionalDependency = null) 
        {
            InitializeComponent();
            
            _playbackService = optionalDependency as PlaybackService;
            if (_playbackService == null)
            {
                var mainWin = Application.Current.MainWindow;
                var prop = mainWin?.GetType().GetProperty("PlaybackService");
                _playbackService = prop?.GetValue(mainWin) as PlaybackService;
            }

            LoadRootLibraryLayout();
            LibraryItemsControl.ItemsSource = DisplayItems;
        }
        
        private void LoadRootLibraryLayout()
        {
            _rootLibraryItems.Clear();
            _rootLibraryItems.Add(new LibraryCardItem { Title = "Favourites", Subtitle = "No Songs", IconData = IconHeart, TargetType = ItemTargetType.Favourites });
            _rootLibraryItems.Add(new LibraryCardItem { Title = "Downloads", Subtitle = "No Songs", IconData = IconDownload, TargetType = ItemTargetType.Downloads });
            _rootLibraryItems.Add(new LibraryCardItem { Title = "History", Subtitle = "7 Songs", IconData = IconHistory, TargetType = ItemTargetType.History });
            _rootLibraryItems.Add(new LibraryCardItem { Title = "Folder", Subtitle = "Local Directories", IconData = IconFolder, TargetType = ItemTargetType.FolderRoot });

            RefreshDisplay(_rootLibraryItems);
        }

        private void RefreshDisplay(IEnumerable<LibraryCardItem> items)
        {
            DisplayItems.Clear();
            foreach (var item in items) DisplayItems.Add(item);
        }

        private void LibraryCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is LibraryCardItem clickedItem)
            {
                // FIXED: Clicking the master Folder card now flips views cleanly instead of triggering an auto disk sweep
                if (clickedItem.TargetType == ItemTargetType.FolderRoot)
                {
                    _isInsideFolderBrowsingMode = true;
                    BtnBack.Visibility = Visibility.Visible;
                    TxtHeaderTitle.Text = "Local Folder Storage";

                    LibraryCardsScrollViewer.Visibility = Visibility.Collapsed;
                    TrackListDisplayPanel.Visibility = Visibility.Visible;
                    
                    // Initialize the view states cleanly without feeding mock parameters
                    TrackListDisplayPanel.InitializeDataStream(new List<MusicFile>(), _playbackService);
                }
                else if (clickedItem.TargetType == ItemTargetType.DirectoryItem)
                {
                    NavigateIntoDirectory(clickedItem.FullPathReference);
                }
            }
        }

        private string FilterWindows7UnicodeBugs(string input, string fallback)
        {
            if (string.IsNullOrEmpty(input)) return fallback;
            string clean = Regex.Replace(input, @"[\x00-\x1F\x7F-\x9F]", "").Trim();
            return (string.IsNullOrEmpty(clean) || clean.StartsWith("???")) ? fallback : clean;
        }

        private void NavigateIntoDirectory(string targetPath)
        {
            if (!Directory.Exists(targetPath)) return;

            _isInsideFolderBrowsingMode = true;
            BtnBack.Visibility = Visibility.Visible;
            _folderNavigationHistory.Push(targetPath);
            TxtHeaderTitle.Text = Path.GetFileName(targetPath);

            var internalContents = new List<LibraryCardItem>();

            try
            {
                foreach (string dir in Directory.GetDirectories(targetPath))
                {
                    internalContents.Add(new LibraryCardItem
                    {
                        Title = Path.GetFileName(dir),
                        Subtitle = "Folder",
                        IconData = IconFolder,
                        TargetType = ItemTargetType.DirectoryItem,
                        FullPathReference = dir
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Directory read skipped: {ex.Message}"); }

            if (_directoryTrackCache.TryGetValue(targetPath, out List<MusicFile> cachedTracks))
            {
                SwitchToDisplayState(cachedTracks, internalContents);
                return;
            }

            var localTracks = new List<MusicFile>();
            try
            {
                string[] audioExtensions = { "*.mp3", "*.wav", "*.flac", "*.m4a" };
                var matchedFiles = audioExtensions.SelectMany(ext => Directory.GetFiles(targetPath, ext)).ToList();

                string tempCachePath = Path.Combine(Path.GetTempPath(), "MusicefyArtworkCache");
                if (!Directory.Exists(tempCachePath)) Directory.CreateDirectory(tempCachePath);

                foreach (string file in matchedFiles)
                {
                    string defaultFilename = Path.GetFileNameWithoutExtension(file);
                    string trackTitle = defaultFilename;
                    string trackArtist = "Unknown Artist";
                    string trackAlbum = "Local Stream";
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
                                    string writeImgPath = Path.Combine(tempCachePath, safeHashName);
                                    if (!File.Exists(writeImgPath))
                                    {
                                        File.WriteAllBytes(writeImgPath, tagContainer.Tag.Pictures[0].Data.Data);
                                    }
                                    trackCoverImageReference = writeImgPath;
                                }
                            }
                        }
                    }
                    catch { trackTitle = defaultFilename; }

                    localTracks.Add(new MusicFile
                    {
                        Title = FilterWindows7UnicodeBugs(trackTitle, defaultFilename),
                        Artist = FilterWindows7UnicodeBugs(trackArtist, "Unknown Artist"),
                        Album = trackAlbum,
                        FilePath = file,
                        SourceUri = file,
                        SourceType = "Local",
                        Duration = trackDuration,
                        CoverPath = trackCoverImageReference
                    });
                }

                _directoryTrackCache[targetPath] = localTracks;
            }
            catch (Exception ex) { MessageBox.Show($"Access Violation Error: {ex.Message}"); }

            SwitchToDisplayState(localTracks, internalContents);
        }

        private void SwitchToDisplayState(List<MusicFile> tracks, List<LibraryCardItem> subFolders)
        {
            if (tracks.Count > 0)
            {
                LibraryCardsScrollViewer.Visibility = Visibility.Collapsed;
                TrackListDisplayPanel.Visibility = Visibility.Visible;
                TrackListDisplayPanel.InitializeDataStream(tracks, _playbackService);
            }
            else
            {
                TrackListDisplayPanel.Visibility = Visibility.Collapsed;
                LibraryCardsScrollViewer.Visibility = Visibility.Visible;
                RefreshDisplay(subFolders);
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_folderNavigationHistory.Count > 0) _folderNavigationHistory.Pop();

            if (_folderNavigationHistory.Count > 0)
            {
                NavigateIntoDirectory(_folderNavigationHistory.Pop());
            }
            else
            {
                _isInsideFolderBrowsingMode = false;
                BtnBack.Visibility = Visibility.Collapsed;
                TrackListDisplayPanel.Visibility = Visibility.Collapsed;
                LibraryCardsScrollViewer.Visibility = Visibility.Visible;
                TxtHeaderTitle.Text = "Saved";
                RefreshDisplay(_rootLibraryItems);
            }
        }

        private void BtnAddPlaylist_Click(object sender, RoutedEventArgs e) { }
    }
}

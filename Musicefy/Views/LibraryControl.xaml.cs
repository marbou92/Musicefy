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
        private List<LibraryCardItem> RootLibraryItems = new List<LibraryCardItem>();
        private Stack<string> FolderNavigationHistory = new Stack<string>();
        private bool IsInsideFolderBrowsingMode = false;
        private PlaybackService _playbackService;

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
            RootLibraryItems.Clear();
            RootLibraryItems.Add(new LibraryCardItem { Title = "Favourites", Subtitle = "No Songs", IconData = IconHeart, TargetType = ItemTargetType.Favourites });
            RootLibraryItems.Add(new LibraryCardItem { Title = "Downloads", Subtitle = "No Songs", IconData = IconDownload, TargetType = ItemTargetType.Downloads });
            RootLibraryItems.Add(new LibraryCardItem { Title = "History", Subtitle = "7 Songs", IconData = IconHistory, TargetType = ItemTargetType.History });
            RootLibraryItems.Add(new LibraryCardItem { Title = "Folder", Subtitle = "Local Directories", IconData = IconFolder, TargetType = ItemTargetType.FolderRoot });

            RefreshDisplay(RootLibraryItems);
        }

        private void RefreshDisplay(IEnumerable<LibraryCardItem> items)
        {
            DisplayItems.Clear();
            foreach (var item in items)
            {
                DisplayItems.Add(item);
            }
        }

        private void LibraryCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is LibraryCardItem clickedItem)
            {
                switch (clickedItem.TargetType)
                {
                    case ItemTargetType.FolderRoot:
                        string defaultMusicPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                        NavigateIntoDirectory(defaultMusicPath);
                        break;
                    case ItemTargetType.DirectoryItem:
                        NavigateIntoDirectory(clickedItem.FullPathReference);
                        break;
                }
            }
        }

        private string FilterWindows7UnicodeBugs(string input)
        {
            if (string.IsNullOrEmpty(input)) return "Unknown Field";
            string clean = Regex.Replace(input, @"[\x00-\x1F\x7F-\x9F]", "").Trim();
            return string.IsNullOrEmpty(clean) ? "Local Track Node" : clean;
        }

        private void NavigateIntoDirectory(string targetPath)
        {
            if (!Directory.Exists(targetPath)) return;

            IsInsideFolderBrowsingMode = true;
            BtnBack.Visibility = Visibility.Visible;
            FolderNavigationHistory.Push(targetPath);
            TxtHeaderTitle.Text = Path.GetFileName(targetPath);

            var internalContents = new List<LibraryCardItem>();
            var localTracks = new List<MusicFile>();

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

                string[] audioExtensions = { "*.mp3", "*.wav", "*.flac", "*.m4a" };
                var matchedFiles = audioExtensions.SelectMany(ext => Directory.GetFiles(targetPath, ext)).ToList();

                string tempCachePath = Path.Combine(Path.GetTempPath(), "MusicefyArtworkCache");
                if (!Directory.Exists(tempCachePath)) Directory.CreateDirectory(tempCachePath);

                foreach (string file in matchedFiles)
                {
                    string trackTitle = Path.GetFileNameWithoutExtension(file);
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

                        using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            if (file.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) && fs.Length >= 128)
                            {
                                byte[] b = new byte[128];
                                fs.Seek(-128, SeekOrigin.End);
                                fs.Read(b, 0, 128);
                                if (Encoding.Default.GetString(b, 0, 3) == "TAG")
                                {
                                    string t = Encoding.Default.GetString(b, 3, 30).Trim();
                                    string a = Encoding.Default.GetString(b, 33, 30).Trim();
                                    string al = Encoding.Default.GetString(b, 63, 30).Trim();

                                    if (!string.IsNullOrEmpty(t)) trackTitle = t;
                                    if (!string.IsNullOrEmpty(a)) trackArtist = a;
                                    if (!string.IsNullOrEmpty(al)) trackAlbum = al;
                                }
                            }
                        }

                        using (var tagContainer = TagLib.File.Create(file))
                        {
                            if (tagContainer.Tag != null && tagContainer.Tag.Pictures != null && tagContainer.Tag.Pictures.Length > 0)
                            {
                                string safeImgName = "cover_" + Math.Abs(file.GetHashCode()).ToString() + ".jpg";
                                string writeImgPath = Path.Combine(tempCachePath, safeImgName);
                                if (!File.Exists(writeImgPath))
                                {
                                    File.WriteAllBytes(writeImgPath, tagContainer.Tag.Pictures[0].Data.Data);
                                }
                                trackCoverImageReference = writeImgPath;
                            }
                        }
                    }
                    catch { }

                    trackTitle = FilterWindows7UnicodeBugs(trackTitle);
                    trackArtist = FilterWindows7UnicodeBugs(trackArtist);

                    localTracks.Add(new MusicFile
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
                MessageBox.Show($"Access Violation Error: {ex.Message}");
            }

            if (localTracks.Count > 0)
            {
                LibraryCardsScrollViewer.Visibility = Visibility.Collapsed;
                TrackListDisplayPanel.Visibility = Visibility.Visible;
                TrackListDisplayPanel.InitializeDataStream(localTracks, _playbackService);
            }
            else
            {
                TrackListDisplayPanel.Visibility = Visibility.Collapsed;
                LibraryCardsScrollViewer.Visibility = Visibility.Visible;
                RefreshDisplay(internalContents);
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (FolderNavigationHistory.Count > 0) FolderNavigationHistory.Pop();

            if (FolderNavigationHistory.Count > 0)
            {
                string parentPath = FolderNavigationHistory.Pop();
                NavigateIntoDirectory(parentPath);
            }
            else
            {
                IsInsideFolderBrowsingMode = false;
                BtnBack.Visibility = Visibility.Collapsed;
                TrackListDisplayPanel.Visibility = Visibility.Collapsed;
                LibraryCardsScrollViewer.Visibility = Visibility.Visible;
                TxtHeaderTitle.Text = "Saved";
                RefreshDisplay(RootLibraryItems);
            }
        }

        private void BtnAddPlaylist_Click(object sender, RoutedEventArgs e) { }
    }
}

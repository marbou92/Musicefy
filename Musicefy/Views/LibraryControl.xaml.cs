using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Musicefy.Views
{
    public partial class LibraryControl : UserControl
    {
        // Layout data streams
        public ObservableCollection<LibraryCardItem> DisplayItems { get; set; } = new ObservableCollection<LibraryCardItem>();
        private List<LibraryCardItem> RootLibraryItems = new List<LibraryCardItem>();
        private Stack<string> FolderNavigationHistory = new Stack<string>();
        private bool IsInsideFolderBrowsingMode = false;

        // Path geometries
        private const string IconHeart = "M12,21.35L10.55,20.03C5.4,15.36 2,12.27 2,8.5C2,5.41 4.42,3 7.5,3C9.24,3 10.91,3.81 12,5.08C13.09,3.81 14.76,3 16.5,3C19.58,3 22,5.41 22,8.5C22,12.27 18.6,15.36 13.45,20.03L12,21.35Z";
        private const string IconDownload = "M5,20H19V18H5V20M19,9H15V3H9V9H5L12,16L19,9Z";
        private const string IconHistory = "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4M12.5,7V12.25L17,14.92L16.25,16.15L11,13V7H12.5Z";
        private const string IconFolder = "M10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6H12L10,4Z";
        private const string IconMusicDisc = "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9Z";

        public LibraryControl(object optionalDependency = null) 
        {
            InitializeComponent();
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

                    case ItemTargetType.Playlist:
                        MessageBox.Show($"Loading Custom Playlist: {clickedItem.Title}", "Musicefy Player");
                        break;

                    default:
                        MessageBox.Show($"Opening structural link: {clickedItem.Title}", "Musicefy System");
                        break;
                }
            }
        }

        private void NavigateIntoDirectory(string targetPath)
        {
            if (!Directory.Exists(targetPath)) return;

            IsInsideFolderBrowsingMode = true;
            BtnBack.Visibility = Visibility.Visible;
            FolderNavigationHistory.Push(targetPath);
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

                string[] audioExtensions = { "*.mp3", "*.wav", "*.flac", "*.m4a" };
                int fileCount = 0;
                foreach (var ext in audioExtensions)
                {
                    foreach (string file in Directory.GetFiles(targetPath, ext))
                    {
                        fileCount++;
                    }
                }
                
                if (fileCount > 0)
                {
                    internalContents.Insert(0, new LibraryCardItem {
                        Title = "All Folder Tracks",
                        Subtitle = $"{fileCount} Audio files found",
                        IconData = IconMusicDisc,
                        TargetType = ItemTargetType.Playlist
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Access Violation Error: {ex.Message}");
            }

            RefreshDisplay(internalContents);
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (FolderNavigationHistory.Count > 0)
            {
                FolderNavigationHistory.Pop();
            }

            if (FolderNavigationHistory.Count > 0)
            {
                string parentPath = FolderNavigationHistory.Pop();
                NavigateIntoDirectory(parentPath);
            }
            else
            {
                IsInsideFolderBrowsingMode = false;
                BtnBack.Visibility = Visibility.Collapsed;
                TxtHeaderTitle.Text = "Saved";
                RefreshDisplay(RootLibraryItems);
            }
        }

        private void BtnAddPlaylist_Click(object sender, RoutedEventArgs e)
        {
            var playlistDialog = new CreatePlaylistWindow();
            playlistDialog.Owner = Window.GetWindow(this);

            if (playlistDialog.ShowDialog() == true)
            {
                string playlistName = playlistDialog.ResultPlaylistName;

                var newPlaylist = new LibraryCardItem
                {
                    Title = playlistName,
                    Subtitle = "Playlist • 0 Tracks",
                    IconData = IconMusicDisc,
                    TargetType = ItemTargetType.Playlist
                };

                if (IsInsideFolderBrowsingMode)
                {
                    DisplayItems.Add(newPlaylist);
                }
                else
                {
                    RootLibraryItems.Add(newPlaylist);
                    RefreshDisplay(RootLibraryItems);
                }
            }
        }
    }

    public class LibraryCardItem
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string IconData { get; set; }
        public ItemTargetType TargetType { get; set; }
        public string FullPathReference { get; set; }
    }

    public enum ItemTargetType
    {
        Favourites,
        Downloads,
        History,
        FolderRoot,
        DirectoryItem,
        Playlist
    }
}

namespace Musicefy.Models
{
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
        Playlist,
        Artists,     // Phase 3: Followed artists section in Library
        Albums       // Phase 3: Saved albums section in Library
    }
}

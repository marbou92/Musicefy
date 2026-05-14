using System.Collections.ObjectModel;
using Musicefy.Core.Models;

namespace Musicefy
{
    /// <summary>
    /// Global static app context for shared collections.
    /// </summary>
    public static class MusicefyApp
    {
        /// <summary>
        /// Global music library accessible across views.
        /// </summary>
        public static ObservableCollection<MusicFile> Library { get; } = new ObservableCollection<MusicFile>();
    }
}

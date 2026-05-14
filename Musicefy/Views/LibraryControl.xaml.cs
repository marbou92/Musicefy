using System.Windows.Controls;
using Musicefy.Services;
using Musicefy.Core.Models;

namespace Musicefy.Views
{
    public partial class LibraryControl : UserControl
    {
        private readonly PlaybackService _playback;

        public LibraryControl(PlaybackService playback)
        {
            InitializeComponent();
            _playback = playback;
            LibraryList.ItemsSource = MusicefyApp.Library;
        }

        private void LibraryList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (LibraryList.SelectedItem is MusicFile track)
                _playback.PlayTrack(track);
        }
    }
}

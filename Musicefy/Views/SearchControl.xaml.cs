using System.Windows.Controls;
using Musicefy.Services;
using Musicefy.Core.Models;

namespace Musicefy.Views
{
    public partial class SearchControl : UserControl
    {
        private readonly PlaybackService _playback;

        public SearchControl(PlaybackService playback)
        {
            InitializeComponent();
            _playback = playback;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Example: filter your library
            var query = SearchBox.Text.ToLower();
            var results = MusicefyApp.Library
                .Where(t => t.Title.ToLower().Contains(query) || t.Artist.ToLower().Contains(query))
                .ToList();
            SearchResults.ItemsSource = results;
        }

        private void SearchResults_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (SearchResults.SelectedItem is MusicFile track)
                _playback.PlayTrack(track);
        }
    }
}

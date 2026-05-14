using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

            // Initialize watermark
            SearchBox.Text = "Search...";
            SearchBox.Foreground = Brushes.Gray;
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchBox.Text == "Search...")
            {
                SearchBox.Text = "";
                SearchBox.Foreground = Brushes.Black;
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                SearchBox.Text = "Search...";
                SearchBox.Foreground = Brushes.Gray;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Ignore watermark text
            if (SearchBox.Text == "Search...")
            {
                SearchResults.ItemsSource = null;
                return;
            }

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

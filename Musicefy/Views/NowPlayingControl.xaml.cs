using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Musicefy.Services;
using Musicefy.Core.Models;

namespace Musicefy.Views
{
    public partial class NowPlayingControl : UserControl
    {
        private readonly PlaybackService _playback;

        // Event to notify MainWindow to collapse back
        public event Action RequestCollapse;

        public NowPlayingControl(PlaybackService playback)
        {
            InitializeComponent();
            _playback = playback;

            _playback.TrackChanged += OnTrackChanged;
            _playback.ProgressChanged += OnProgressChanged;

            // Enable swipe gesture
            this.ManipulationMode = ManipulationModes.TranslateY;
            this.ManipulationDelta += OnManipulationDelta;
        }

        // Swipe down gesture
        private void OnManipulationDelta(object sender, ManipulationDeltaEventArgs e)
        {
            if (e.DeltaManipulation.Translation.Y > 50) // detect downward swipe
            {
                RequestCollapse?.Invoke();
            }
        }

        // Back button click
        private void BackButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            RequestCollapse?.Invoke();
        }

        // Update UI when track changes
        private void OnTrackChanged(MusicFile track)
        {
            Title.Text = track.Title;
            Artist.Text = track.Artist;
            Meta.Text = $"{track.Album}{(track.Year > 0 ? " • " + track.Year : "")}";

            if (!string.IsNullOrEmpty(track.CoverPath))
                Cover.Source = new BitmapImage(new Uri(track.CoverPath, UriKind.RelativeOrAbsolute));
            else
                Cover.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/default_cover.png"));

            Lyrics.Items.Clear();
            if (!string.IsNullOrEmpty(track.Lyrics))
            {
                foreach (var line in track.Lyrics.Split('\n'))
                    Lyrics.Items.Add(line);
            }

            // Update favourite and play count
            FavouriteIcon.Text = track.IsFavourite ? "❤" : "♡";
            PlayCount.Text = $"Played {track.PlayCount} times";
        }

        // Update progress slider
        private void OnProgressChanged(TimeSpan current, TimeSpan total)
        {
            ProgressSlider.Maximum = total.TotalSeconds;
            ProgressSlider.Value = current.TotalSeconds;
        }

        // Playback controls
        private void Play_Click(object sender, System.Windows.RoutedEventArgs e) => _playback.Resume();
        private void Pause_Click(object sender, System.Windows.RoutedEventArgs e) => _playback.Pause();
        private void Next_Click(object sender, System.Windows.RoutedEventArgs e) => _playback.Next();
        private void Previous_Click(object sender, System.Windows.RoutedEventArgs e) => _playback.Previous();

        // Toggle favourite
        private void FavouriteIcon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_playback.CurrentTrack != null)
            {
                _playback.CurrentTrack.ToggleFavourite();
                FavouriteIcon.Text = _playback.CurrentTrack.IsFavourite ? "❤" : "♡";
            }
        }
    }
}

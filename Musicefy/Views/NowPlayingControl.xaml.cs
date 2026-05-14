using System;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Musicefy.Services;
using Musicefy.Core.Models;

namespace Musicefy.Views
{
    public partial class NowPlayingControl : UserControl
    {
        private readonly PlaybackService _playback;

        public NowPlayingControl(PlaybackService playback)
        {
            InitializeComponent();
            _playback = playback;

            _playback.TrackChanged += OnTrackChanged;
            _playback.ProgressChanged += OnProgressChanged;
        }

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
        }

        private void OnProgressChanged(TimeSpan current, TimeSpan total)
        {
            // Could update a progress bar here if you add one
        }

        private void Play_Click(object sender, System.Windows.RoutedEventArgs e) => _playback.Resume();
        private void Pause_Click(object sender, System.Windows.RoutedEventArgs e) => _playback.Pause();
        private void Next_Click(object sender, System.Windows.RoutedEventArgs e) => _playback.Next();
        private void Previous_Click(object sender, System.Windows.RoutedEventArgs e) => _playback.Previous();
    }
}

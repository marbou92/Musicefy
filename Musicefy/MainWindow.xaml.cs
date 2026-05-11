using System.Windows;
using System.Windows.Controls;
using Musicefy.Core.Services;
using Musicefy.Core.Models;
using Musicefy.Views;

namespace Musicefy
{
    public partial class MainWindow : Window
    {
        private AudioPlayer audioPlayer;
        private PlaylistManager playlistManager;
        private StreamingSourceManager sourceManager;

        public MainWindow()
        {
            InitializeComponent();
            InitializeApp();
            RefreshSources();
            RefreshTracks();
        }

        private void InitializeApp()
        {
            audioPlayer = new AudioPlayer();
            playlistManager = new PlaylistManager();
            sourceManager = new StreamingSourceManager();

            TracksList.SelectionChanged += TracksList_SelectionChanged;
            VolumeSlider.ValueChanged += (s, e) => audioPlayer.SetVolume((float)(VolumeSlider.Value / 100.0));
        }

        private void RefreshSources()
        {
            SourcesListBox.Items.Clear();
            var sources = sourceManager.GetAllSources();

            foreach (var source in sources)
            {
                var item = new ListBoxItem
                {
                    Content = source,
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 5, 0, 0)
                };
                SourcesListBox.Items.Add(item);
            }
        }

        private void RefreshTracks()
        {
            TracksList.ItemsSource = playlistManager.GetSampleTracks();
        }

        private void AddSourceButton_Click(object sender, RoutedEventArgs e)
        {
            var addSourceWindow = new AddSourceWindow(sourceManager)
            {
                Owner = this
            };

            if (addSourceWindow.ShowDialog() == true)
            {
                RefreshSources();
                MessageBox.Show("Streaming source has been added. You can now search and stream music!", "Source Added");
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (TracksList.SelectedItem is MusicFile t)
            {
                if (!string.IsNullOrEmpty(t.SourceUri))
                {
                    try
                    {
                        audioPlayer.Play(t.SourceUri);
                        NowPlayingTitle.Text = t.Title;
                        NowPlayingArtist.Text = t.Artist;
                        NowPlayingMeta.Text = $"{t.Album} • {t.Year}";
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show($"Playback error: {ex.Message}", "Playback Error");
                    }
                }
                else
                {
                    MessageBox.Show("Selected track has no source URI.", "Cannot Play");
                }
            }
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: implement playlist navigation
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: implement playlist navigation
        }

        private void TracksList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TracksList.SelectedItem is MusicFile t)
            {
                NowPlayingTitle.Text = t.Title;
                NowPlayingArtist.Text = t.Artist;
                NowPlayingMeta.Text = $"{t.Album} • {t.Year}";
            }
        }

        // Settings button handler
        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow { Owner = this };
            if (settingsWindow.ShowDialog() == true)
            {
                string selectedTheme = Musicefy.Properties.Settings.Default.Theme;
                App.ApplyTheme(selectedTheme);
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Musicefy - Music Streaming Player\nVersion 1.0.0\n© 2026 Musicefy Team",
                            "About Musicefy",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}

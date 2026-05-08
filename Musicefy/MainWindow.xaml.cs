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
        }

        private void InitializeApp()
        {
            audioPlayer = new AudioPlayer();
            playlistManager = new PlaylistManager();
            sourceManager = new StreamingSourceManager();
        }

        private void RefreshSources()
        {
            SourcesListBox.Items.Clear();
            var sources = sourceManager.GetAllSources();

            foreach (var source in sources)
            {
                var item = new ListBoxItem
                {
                    Content = source.ToString(),
                    Background = Application.Current.Resources["SecondaryBackgroundBrush"] as System.Windows.Media.Brush,
                    Foreground = Application.Current.Resources["ForegroundBrush"] as System.Windows.Media.Brush,
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 5, 0, 0)
                };
                SourcesListBox.Items.Add(item);
            }
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
            // Implement play functionality
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement previous functionality
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement next functionality
        }
    }
}

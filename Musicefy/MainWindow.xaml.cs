using System.Windows;
using Musicefy.Core.Services;
using Musicefy.Core.Models;

namespace Musicefy
{
    public partial class MainWindow : Window
    {
        private AudioPlayer audioPlayer;
        private PlaylistManager playlistManager;

        public MainWindow()
        {
            InitializeComponent();
            InitializeApp();
        }

        private void InitializeApp()
        {
            audioPlayer = new AudioPlayer();
            playlistManager = new PlaylistManager();
        }

        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            // Implement folder selection dialog
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement play functionality
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement pause functionality
        }
    }
}

using System.Windows;

namespace Musicefy.Views
{
    public partial class RenamePlaylistWindow : Window
    {
        public string ResultPlaylistName => TxtPlaylistName.Text.Trim();

        public RenamePlaylistWindow(string currentName)
        {
            InitializeComponent();
            TxtPlaylistName.Text = currentName ?? "";
            TxtPlaylistName.SelectAll();
            TxtPlaylistName.Focus();
        }

        private void BtnRename_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtPlaylistName.Text))
            {
                MessageBox.Show("Please enter a playlist name.", "Rename Playlist",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}

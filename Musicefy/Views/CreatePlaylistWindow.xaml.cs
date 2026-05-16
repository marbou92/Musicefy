using System;
using System.Windows;
using System.Windows.Input;

namespace Musicefy.Views
{
    public partial class CreatePlaylistWindow : Window
    {
        // Public output accessor property string token reference payload
        public string ResultPlaylistName { get; private set; }

        public CreatePlaylistWindow()
        {
            InitializeComponent();
            
            // Focus typing cursor frame directly inside container input fields instantly upon visual assembly initialization
            Loaded += (s, e) => TxtPlaylistName.Focus();
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            SubmitAndClose();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TxtPlaylistName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SubmitAndClose();
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void SubmitAndClose()
        {
            if (!string.IsNullOrWhiteSpace(TxtPlaylistName.Text))
            {
                ResultPlaylistName = TxtPlaylistName.Text.Trim();
                DialogResult = true;
                Close();
            }
            else
            {
                TxtPlaylistName.Focus();
            }
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Facilitates natural window drag mechanics across bare custom border edges
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
    }
}

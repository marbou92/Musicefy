using System.Windows;
using Musicefy.Core.Models;
using Musicefy.Core.Services;

namespace Musicefy.Views
{
    public partial class AddSourceWindow : Window
    {
        private StreamingSourceManager sourceManager;

        public AddSourceWindow(StreamingSourceManager manager)
        {
            InitializeComponent();
            sourceManager = manager;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            var source = new StreamingSource
            {
                Name = NameTextBox.Text,
                Url = UrlTextBox.Text,
                Username = UsernameTextBox.Text,
                Password = PasswordBox.Password,
                Type = "Subsonic"
            };

            try
            {
                var client = new SubsonicClient(source);
                bool connected = await client.TestConnectionAsync();

                if (connected)
                {
                    MessageBox.Show("Successfully connected to the streaming service!", "Connection Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to connect to the streaming service. Please check your credentials.", "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var source = new StreamingSource
            {
                Name = NameTextBox.Text,
                Url = UrlTextBox.Text,
                Username = UsernameTextBox.Text,
                Password = PasswordBox.Password,
                Type = "Subsonic"
            };

            try
            {
                await sourceManager.AddSourceAsync(source);
                MessageBox.Show("Streaming source added successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error adding source: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

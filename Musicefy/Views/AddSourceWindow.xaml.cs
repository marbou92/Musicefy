using System.Windows;
using Musicefy.Core.Models;
using Musicefy.Core.Services;
using Microsoft.Win32;
using System.IO;

namespace Musicefy.Views
{
    public partial class AddSourceWindow : Window
    {
        private StreamingSourceManager sourceManager;

        public AddSourceWindow(StreamingSourceManager manager)
        {
            InitializeComponent();
            sourceManager = manager;

            // Update labels when source type changes
            SourceTypeCombo.SelectionChanged += (s, e) =>
            {
                if (SourceTypeCombo.SelectedIndex == 1) // Local Folder
                {
                    UrlLabel.Text = "Local Folder Path";
                    UsernameTextBox.IsEnabled = false;
                    PasswordBox.IsEnabled = false;
                }
                else
                {
                    UrlLabel.Text = "Server URL";
                    UsernameTextBox.IsEnabled = true;
                    PasswordBox.IsEnabled = true;
                }
            };
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            var type = SourceTypeCombo.SelectedIndex == 1 ? "Local" : "Subsonic";

            var source = new StreamingSource
            {
                Name = NameTextBox.Text,
                Url = UrlTextBox.Text,
                Username = UsernameTextBox.Text,
                Password = PasswordBox.Password,
                Type = type
            };

            try
            {
                if (type == "Local")
                {
                    if (Directory.Exists(source.Url))
                    {
                        MessageBox.Show("Local folder found!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Folder not found. Please check the path.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    var client = new SubsonicClient(source);
                    bool connected = await client.TestConnectionAsync();

                    if (connected)
                        MessageBox.Show("Successfully connected to the streaming service!", "Connection Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    else
                        MessageBox.Show("Failed to connect. Please check your credentials.", "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var type = SourceTypeCombo.SelectedIndex == 1 ? "Local" : "Subsonic";

            var source = new StreamingSource
            {
                Name = NameTextBox.Text,
                Url = UrlTextBox.Text,
                Username = UsernameTextBox.Text,
                Password = PasswordBox.Password,
                Type = type
            };

            try
            {
                await sourceManager.AddSourceAsync(source);
                MessageBox.Show($"{type} source added successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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

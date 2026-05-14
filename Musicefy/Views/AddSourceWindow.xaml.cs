using System.IO;
using System.Windows;
using Musicefy.Core.Models;
using Musicefy.Core.Services;
using Musicefy.Services; // for ToastService

namespace Musicefy.Views
{
    public partial class AddSourceWindow : Window
    {
        private readonly StreamingSourceManager sourceManager;

        public AddSourceWindow(StreamingSourceManager manager)
        {
            InitializeComponent();
            sourceManager = manager;

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
            DialogResult = false;
            Close();
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
                        ToastService.ShowToast("✅ Local folder found!", Brushes.ForestGreen);
                    else
                        ToastService.ShowToast("❌ Folder not found. Please check the path.", Brushes.OrangeRed);
                }
                else
                {
                    var client = new SubsonicClient(source);
                    bool connected = await client.TestConnectionAsync();

                    if (connected)
                        ToastService.ShowToast("✅ Successfully connected to the streaming service!", Brushes.ForestGreen);
                    else
                        ToastService.ShowToast("❌ Failed to connect. Please check your credentials.", Brushes.OrangeRed);
                }
            }
            catch (System.Exception ex)
            {
                ToastService.ShowToast($"❌ Error: {ex.Message}", Brushes.OrangeRed);
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
                ToastService.ShowToast($"✅ {type} source added successfully!", Brushes.ForestGreen);
                DialogResult = true;
                Close();
            }
            catch (System.Exception ex)
            {
                ToastService.ShowToast($"❌ Error adding source: {ex.Message}", Brushes.OrangeRed);
            }
        }
    }
}

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Musicefy.Core.Models;
using Musicefy.Core.Services;
using Musicefy.Services; // ToastService

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

        /// <summary>
        /// Validate inputs and show inline errors.
        /// </summary>
        private bool ValidateInputs(bool isLocal)
        {
            bool valid = true;

            ResetValidation(NameTextBox);
            ResetValidation(UrlTextBox);
            ResetValidation(UsernameTextBox);
            ResetValidation(PasswordBox);

            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                ShowValidation(NameTextBox, "Name is required.");
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(UrlTextBox.Text))
            {
                ShowValidation(UrlTextBox, isLocal ? "Folder path is required." : "Server URL is required.");
                valid = false;
            }

            if (!isLocal)
            {
                if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
                {
                    ShowValidation(UsernameTextBox, "Username is required.");
                    valid = false;
                }
                if (string.IsNullOrWhiteSpace(PasswordBox.Password))
                {
                    ShowValidation(PasswordBox, "Password is required.");
                    valid = false;
                }
            }

            return valid;
        }

        private void ShowValidation(Control control, string tooltip)
        {
            control.BorderBrush = Brushes.Red;
            control.ToolTip = tooltip;
        }

        private void ResetValidation(Control control)
        {
            control.ClearValue(Border.BorderBrushProperty);
            control.ToolTip = null;
        }

        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            bool isLocal = SourceTypeCombo.SelectedIndex == 1;
            if (!ValidateInputs(isLocal)) return;

            var type = isLocal ? "Local" : "Subsonic";

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
                if (isLocal)
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
            bool isLocal = SourceTypeCombo.SelectedIndex == 1;
            if (!ValidateInputs(isLocal)) return;

            var type = isLocal ? "Local" : "Subsonic";

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

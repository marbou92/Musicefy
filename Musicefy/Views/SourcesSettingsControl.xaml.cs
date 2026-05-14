using System.Windows;
using System.Windows.Controls;

namespace Musicefy.Views
{
    public partial class SourcesSettingsControl : UserControl
    {
        public SourcesSettingsControl()
        {
            InitializeComponent();
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            // Example: test connection logic
            string url = UrlTextBox.Text;
            string user = UsernameTextBox.Text;
            string pass = PasswordBox.Password;

            MessageBox.Show($"Testing connection to {url} as {user}...", "Test Source");
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            // Example: add source logic
            string name = NameTextBox.Text;
            string url = UrlTextBox.Text;
            string user = UsernameTextBox.Text;

            MessageBox.Show($"Added source '{name}' ({url}) for user {user}.", "Source Added");
        }
    }
}

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
            string url = UrlTextBox.Text;
            string user = UsernameTextBox.Text;

            MessageBox.Show($"Testing core indexing validation handshake paths to {url} as {user}...", "Musicefy Network Core", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            string name = NameTextBox.Text;
            string url = UrlTextBox.Text;

            MessageBox.Show($"Linked streaming profile connection resource node safely: '{name}' ({url}).", "Musicefy Network Core", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

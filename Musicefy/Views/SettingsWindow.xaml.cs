using System.Windows;
using System.Windows.Controls;
using Musicefy.Core; // Import ThemeManager

namespace Musicefy.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = (ThemeCombo.SelectedItem as ComboBoxItem)?.Content.ToString();
            if (!string.IsNullOrEmpty(selected))
            {
                ThemeManager.ApplyTheme(selected);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // ThemeManager already persists the selected theme
            this.DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            Close();
        }
    }
}

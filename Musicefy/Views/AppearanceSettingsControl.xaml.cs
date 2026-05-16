using System.Windows;
using System.Windows.Controls;
using Musicefy.Services; // Ensure this points to where you saved ThemeManager

namespace Musicefy.Views
{
    public partial class AppearanceSettingsControl : UserControl
    {
        public AppearanceSettingsControl()
        {
            InitializeComponent();
        }

        private void Theme_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && PaletteBox != null)
            {
                ThemeManager.ApplyTheme(rb.Content.ToString(), PaletteBox.Text);
            }
        }

        private void PaletteBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is ComboBoxItem item)
            {
                // Find out which theme radio button is currently checked
                string theme = "Dark"; // Fallback
                
                ThemeManager.ApplyTheme(theme, item.Content.ToString());
            }
        }
    }
}

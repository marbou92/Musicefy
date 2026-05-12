using System.Windows;
using System.Windows.Controls;
using Musicefy.Services;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class AppearanceSettingsView : Window
    {
        private string _currentMode = "Dark";     // default
        private string _currentPalette = "Default";

        public AppearanceSettingsView()
        {
            InitializeComponent();
        }

        // Handle TabControl selection (System / Light / Dark)
        private void ThemeTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(sender is TabControl tab)) return;

            switch (tab.SelectedIndex)
            {
                case 0: _currentMode = "System"; break;
                case 1: _currentMode = "Light"; break;
                case 2: _currentMode = "Dark"; break;
            }

            ThemeManager.ApplyTheme(_currentMode, _currentPalette);
        }

        // Handle palette preview click
        private void PalettePreview_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.DataContext is ThemePreview preview)) return;

            _currentPalette = preview.Name;
            ThemeManager.ApplyTheme(_currentMode, _currentPalette);
        }
    }
}

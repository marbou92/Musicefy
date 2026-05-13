using System.Windows;
using System.Windows.Controls;
using Musicefy.Services;

namespace Musicefy.Views
{
    public partial class AppearanceSettingsControl : UserControl
    {
        private string _currentMode = "Dark";     // default
        private string _currentPalette = "Default";

        public AppearanceSettingsControl()
        {
            InitializeComponent();

            // Load saved theme
            string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";
            ApplyThemeFromString(savedTheme);
        }

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

        private void PalettePreview_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.DataContext is ThemePreview preview)) return;

            _currentPalette = preview.Name;
            ThemeManager.ApplyTheme(_currentMode, _currentPalette);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string themeString = $"{_currentMode}|{_currentPalette}";
            ThemeManager.SaveTheme(themeString);
            Musicefy.Properties.Settings.Default.Theme = themeString;
            Musicefy.Properties.Settings.Default.Save();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";
            ApplyThemeFromString(savedTheme);
        }

        private void ApplyThemeFromString(string themeString)
        {
            var parts = themeString.Split('|');
            _currentMode = parts.Length > 0 ? parts[0] : "Dark";
            _currentPalette = parts.Length > 1 ? parts[1] : "Default";

            ThemeManager.ApplyTheme(_currentMode, _currentPalette);
        }
    }
}

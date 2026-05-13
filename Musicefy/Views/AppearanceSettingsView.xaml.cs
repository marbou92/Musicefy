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

            // Load saved theme on startup
            string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";
            ApplyThemeFromString(savedTheme);
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

        // Save button
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string themeString = $"{_currentMode}|{_currentPalette}";
            ThemeManager.SaveTheme(themeString);
            Musicefy.Properties.Settings.Default.Theme = themeString;
            Musicefy.Properties.Settings.Default.Save();
            DialogResult = true;
            Close();
        }

        // Cancel button
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";
            ApplyThemeFromString(savedTheme);
            DialogResult = false;
            Close();
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

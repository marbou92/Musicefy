using System;
using System.Windows;

namespace Musicefy.Core
{
    public static class ThemeManager
    {
        /// <summary>
        /// Apply a theme by name. Looks for a ResourceDictionary in /Themes.
        /// </summary>
        public static void ApplyTheme(string themeName)
        {
            string themePath = $"Themes/{themeName}.xaml";

            try
            {
                var dict = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Clear();
                Application.Current.Resources.MergedDictionaries.Add(dict);

                // Persist choice
                Properties.Settings.Default.Theme = themeName;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to apply theme '{themeName}': {ex.Message}", "Theme Error");
            }
        }

        /// <summary>
        /// Load the saved theme at startup.
        /// </summary>
        public static void LoadSavedTheme()
        {
            string savedTheme = Properties.Settings.Default.Theme;
            if (!string.IsNullOrEmpty(savedTheme))
            {
                ApplyTheme(savedTheme);
            }
            else
            {
                ApplyTheme("Dark"); // default fallback
            }
        }
    }
}

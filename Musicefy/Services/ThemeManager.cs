using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Musicefy.Properties; // Settings

namespace Musicefy.Services
{
    public static class ThemeManager
    {
        // List of available themes
        private static readonly string[] AvailableThemes =
        {
            "Dark",
            "Light",
            "DarkLavender",
            "WhiteLavender"
        };

        // Returns all available themes
        public static IEnumerable<string> GetAvailableThemes() => AvailableThemes;

        // Apply a theme by name
        public static void ApplyTheme(string themeName)
        {
            if (!AvailableThemes.Contains(themeName))
                throw new ArgumentException($"Theme '{themeName}' is not defined.");

            var dict = new ResourceDictionary
            {
                Source = new Uri($"Themes/{themeName}.xaml", UriKind.Relative)
            };

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);

            // Always merge Base.xaml for shared styles/animations
            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary
                {
                    Source = new Uri("Themes/Base.xaml", UriKind.Relative)
                });
        }

        // Load saved theme or default
        public static void LoadSavedTheme()
        {
            string savedTheme = Settings.Default.SelectedTheme;

            if (string.IsNullOrEmpty(savedTheme) || !AvailableThemes.Contains(savedTheme))
                savedTheme = "Dark"; // default

            ApplyTheme(savedTheme);
        }

        // Save selected theme
        public static void SaveTheme(string themeName)
        {
            if (!AvailableThemes.Contains(themeName))
                throw new ArgumentException($"Theme '{themeName}' is not defined.");

            Settings.Default.SelectedTheme = themeName;
            Settings.Default.Save();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Musicefy.Properties; // Settings

namespace Musicefy.Services
{
    public static class ThemeManager
    {
        private static readonly string[] AvailableThemes =
        {
            "Dark",
            "Light",
            "DarkLavender",
            "WhiteLavender"
        };

        public static IEnumerable<string> GetAvailableThemes() => AvailableThemes;

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

            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary
                {
                    Source = new Uri("Themes/Base.xaml", UriKind.Relative)
                });
        }

        public static void LoadSavedTheme()
        {
            string savedTheme = Settings.Default.Theme;

            if (string.IsNullOrEmpty(savedTheme) || !AvailableThemes.Contains(savedTheme))
                savedTheme = "Dark";

            ApplyTheme(savedTheme);
        }

        public static void SaveTheme(string themeName)
        {
            if (!AvailableThemes.Contains(themeName))
                throw new ArgumentException($"Theme '{themeName}' is not defined.");

            Settings.Default.Theme = themeName;
            Settings.Default.Save();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Windows;

namespace Musicefy.Services
{
    public static class ThemeManager
    {
        private static readonly string[] Modes = { "System", "Light", "Dark" };
        private static readonly string[] Palettes = { "Default", "Catppuccin", "GreenApple", "Lavender" };

        public static void ApplyTheme(string mode, string palette)
        {
            Application.Current.Resources.MergedDictionaries.Clear();

            // Always include base styles
            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new Uri("/Themes/Base.xaml", UriKind.Relative) });

            // Mode (System/Light/Dark)
            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new Uri($"/Themes/Modes/{mode}.xaml", UriKind.Relative) });

            // Palette (Default/Catppuccin/GreenApple/Lavender)
            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new Uri($"/Themes/Palettes/{palette}.xaml", UriKind.Relative) });
        }

        /// <summary>
        /// Returns all valid mode + palette combinations as strings like "Dark|Catppuccin".
        /// </summary>
        public static IEnumerable<string> GetAvailableThemes()
        {
            var themes = new List<string>();
            foreach (var mode in Modes)
            {
                foreach (var palette in Palettes)
                {
                    themes.Add($"{mode}|{palette}");
                }
            }
            return themes;
        }

        /// <summary>
        /// Saves the selected theme string (Mode|Palette) to settings.
        /// </summary>
        public static void SaveTheme(string themeString)
        {
            Musicefy.Properties.Settings.Default.Theme = themeString;
            Musicefy.Properties.Settings.Default.Save();
        }
    }
}

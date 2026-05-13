using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace Musicefy.Services
{
    public static class ThemeManager
    {
        private static readonly string[] Modes = { "System", "Light", "Dark" };
        private static readonly string[] Palettes = { "Default", "Catppuccin", "GreenApple", "Lavender" };

        /// <summary>
        /// Apply theme with both mode and palette.
        /// </summary>
        public static void ApplyTheme(string mode, string palette)
        {
            Application.Current.Resources.MergedDictionaries.Clear();

            // Always include base styles
            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new Uri("/Themes/Base.xaml", UriKind.Relative) });

            // Handle "System" mode
            if (mode.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                bool isDark = IsSystemDarkMode();
                mode = isDark ? "Dark" : "Light";
            }

            // Mode dictionary
            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new Uri($"/Themes/Modes/{mode}.xaml", UriKind.Relative) });

            // Palette dictionary
            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new Uri($"/Themes/Palettes/{palette}.xaml", UriKind.Relative) });
        }

        /// <summary>
        /// Apply theme with only mode, defaults to "Default" palette.
        /// </summary>
        public static void ApplyTheme(string mode) => ApplyTheme(mode, "Default");

        /// <summary>
        /// Apply theme from a combined string like "Dark|Catppuccin".
        /// </summary>
        public static void ApplyThemeFromString(string themeString)
        {
            if (string.IsNullOrWhiteSpace(themeString))
            {
                ApplyTheme("Dark", "Default");
                return;
            }

            var parts = themeString.Split('|');
            string mode = parts.Length > 0 ? parts[0] : "Dark";
            string palette = parts.Length > 1 ? parts[1] : "Default";

            ApplyTheme(mode, palette);
        }

        /// <summary>
        /// Returns all valid mode + palette combinations as strings like "Dark|Catppuccin".
        /// </summary>
        public static IEnumerable<string> GetAvailableThemes()
        {
            var themes = new List<string>();
            foreach (var mode in Modes)
                foreach (var palette in Palettes)
                    themes.Add($"{mode}|{palette}");
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

        /// <summary>
        /// Detect if Windows is in dark mode.
        /// </summary>
        private static bool IsSystemDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int val)
                    return val == 0; // 0 = Dark, 1 = Light
            }
            catch { }
            return false; // fallback to Light
        }

        /// <summary>
        /// Start watching for system theme changes.
        /// </summary>
        public static void StartSystemThemeWatcher()
        {
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category == UserPreferenceCategory.General)
                {
                    string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "System|Default";
                    if (savedTheme.StartsWith("System", StringComparison.OrdinalIgnoreCase))
                    {
                        ApplyThemeFromString(savedTheme);
                        AnimateWindowsFade();
                    }
                }
            };
        }

        /// <summary>
        /// Animate all open windows with a fade-in after theme change.
        /// </summary>
        public static void AnimateWindowsFade()
        {
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            foreach (Window win in Application.Current.Windows)
            {
                win.Opacity = 0; // reset
                win.BeginAnimation(UIElement.OpacityProperty, anim);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace Musicefy.Services
{
    public static class ThemeManager
    {
        private static readonly string[] Modes = { "System", "Light", "Dark", "DarkPure" };
        private static readonly string[] Palettes = { "Default", "Catppuccin", "GreenApple", "Lavender" };

        /// <summary>
        /// Apply a theme by mode + palette.
        /// </summary>
        public static void ApplyTheme(string mode, string palette)
        {
            Application.Current.Resources.MergedDictionaries.Clear();

            // Always load base styles first
            MergeDictionary("/Themes/Base.xaml");

            // Resolve system mode
            if (mode.Equals("System", StringComparison.OrdinalIgnoreCase))
                mode = IsSystemDarkMode() ? "Dark" : "Light";

            // Load mode dictionary
            MergeDictionary($"/Themes/Modes/{mode}.xaml");

            // Handle palette adaptation
            if (palette.Equals("Default", StringComparison.OrdinalIgnoreCase))
            {
                string paletteFile = mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase)
                    ? "Default.Dark.xaml"
                    : "Default.Light.xaml";

                MergeDictionary($"/Themes/Palettes/{paletteFile}");
            }
            else
            {
                MergeDictionary($"/Themes/Palettes/{palette}.xaml");
            }
        }

        public static void ApplyTheme(string mode) => ApplyTheme(mode, "Default");

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

        public static IEnumerable<string> GetAvailableThemes()
        {
            foreach (var mode in Modes)
                foreach (var palette in Palettes)
                    yield return $"{mode}|{palette}";
        }

        public static void SaveTheme(string themeString)
        {
            Musicefy.Properties.Settings.Default.Theme = themeString;
            Musicefy.Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Detect system dark mode via registry.
        /// </summary>
        public static bool IsSystemDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int intVal)
                    return intVal == 0;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Watch for system theme changes and reapply if mode is System.
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
                        Application.Current.Dispatcher.BeginInvoke(new Action(AnimateWindowsFade),
                            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    }
                }
            };
        }

        /// <summary>
        /// Fade in all windows + animate buttons.
        /// </summary>
        public static void AnimateWindowsFade()
        {
            var fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            foreach (Window win in Application.Current.Windows)
            {
                win.Opacity = 0;
                win.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
                AnimateButtons(win);
            }
        }

        private static void AnimateButtons(Window win)
        {
            foreach (var child in FindVisualChildren<System.Windows.Controls.Button>(win))
            {
                // Clear existing handlers to avoid duplicates
                child.MouseEnter -= Button_MouseEnter;
                child.PreviewMouseDown -= Button_MouseDown;
                child.MouseLeave -= Button_MouseLeave;

                child.MouseEnter += Button_MouseEnter;
                child.PreviewMouseDown += Button_MouseDown;
                child.MouseLeave += Button_MouseLeave;
            }
        }

        private static void Button_MouseEnter(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn)
                AnimateButtonGradient(btn,
                    (Color)Application.Current.FindResource("AccentHoverStartColor"),
                    (Color)Application.Current.FindResource("AccentHoverEndColor"),
                    300);
        }

        private static void Button_MouseDown(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn)
                AnimateButtonGradient(btn,
                    (Color)Application.Current.FindResource("AccentPressedStartColor"),
                    (Color)Application.Current.FindResource("AccentPressedEndColor"),
                    200);
        }

        private static void Button_MouseLeave(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn)
                AnimateButtonGradient(btn,
                    (Color)Application.Current.FindResource("AccentStartColor"),
                    (Color)Application.Current.FindResource("AccentEndColor"),
                    300);
        }

        private static void AnimateButtonGradient(System.Windows.Controls.Button btn,
            Color toStart, Color toEnd, int durationMs)
        {
            if (btn.Template.FindName("AccentStart", btn) is GradientStop start &&
                btn.Template.FindName("AccentEnd", btn) is GradientStop end)
            {
                var anim1 = new ColorAnimation(toStart, TimeSpan.FromMilliseconds(durationMs));
                var anim2 = new ColorAnimation(toEnd, TimeSpan.FromMilliseconds(durationMs));
                start.BeginAnimation(GradientStop.ColorProperty, anim1);
                end.BeginAnimation(GradientStop.ColorProperty, anim2);
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                    yield return t;

                foreach (T childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }

        /// <summary>
        /// Provides accent brushes for theme previews in MVVM.
        /// </summary>
        public static Brush GetAccentBrush(string name) => name switch
        {
            "Default" => Brushes.Gray,
            "Catppuccin" => Brushes.MediumOrchid,
            "GreenApple" => Brushes.Green,
            "Lavender" => Brushes.MediumPurple,
            _ => Brushes.Gray
        };

        /// <summary>
        /// Helper to merge a resource dictionary safely.
        /// </summary>
        private static void MergeDictionary(string path)
        {
            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new Uri(path, UriKind.Relative) });
        }
    }
}

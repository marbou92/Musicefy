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
        private static readonly string[] Modes = { "System", "Light", "Dark" };
        private static readonly string[] Palettes = { "Default", "Catppuccin", "GreenApple", "Lavender" };

        public static void ApplyTheme(string mode, string palette)
        {
            Application.Current.Resources.MergedDictionaries.Clear();

            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new Uri("/Themes/Base.xaml", UriKind.Relative) });

            if (mode.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                bool isDark = IsSystemDarkMode();
                mode = isDark ? "Dark" : "Light";
            }

            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new Uri($"/Themes/Modes/{mode}.xaml", UriKind.Relative) });

            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new Uri($"/Themes/Palettes/{palette}.xaml", UriKind.Relative) });
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
            var themes = new List<string>();
            foreach (var mode in Modes)
                foreach (var palette in Palettes)
                    themes.Add($"{mode}|{palette}");
            return themes;
        }

        public static void SaveTheme(string themeString)
        {
            Musicefy.Properties.Settings.Default.Theme = themeString;
            Musicefy.Properties.Settings.Default.Save();
        }

        private static bool IsSystemDarkMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("AppsUseLightTheme");
                        if (value is int intVal)
                            return intVal == 0;
                    }
                }
            }
            catch { }
            return false;
        }

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

                        // Run fade AFTER theme re-application
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            AnimateWindowsFade();
                        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    }
                }
            };
        }

        /// <summary>
        /// Animate all open windows and buttons for a smooth fade/hover/press effect.
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
                child.MouseEnter += (s, e) =>
                {
                    AnimateButtonGradient(child,
                        (Color)Application.Current.FindResource("AccentHoverStartColor"),
                        (Color)Application.Current.FindResource("AccentHoverEndColor"),
                        300);
                };

                child.PreviewMouseDown += (s, e) =>
                {
                    AnimateButtonGradient(child,
                        (Color)Application.Current.FindResource("AccentPressedStartColor"),
                        (Color)Application.Current.FindResource("AccentPressedEndColor"),
                        200);
                };

                child.MouseLeave += (s, e) =>
                {
                    AnimateButtonGradient(child,
                        (Color)Application.Current.FindResource("AccentStartColor"),
                        (Color)Application.Current.FindResource("AccentEndColor"),
                        300);
                };
            }
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
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child is T t)
                        yield return t;

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                        yield return childOfChild;
                }
            }
        }

        /// <summary>
        /// Provides accent brushes for theme previews in MVVM.
        /// </summary>
        public static Brush GetAccentBrush(string name)
        {
            switch (name)
            {
                case "Blue": return Brushes.SkyBlue;
                case "Red": return Brushes.IndianRed;
                case "GreenApple": return Brushes.Green;
                case "Lavender": return Brushes.MediumPurple;
                case "Catppuccin": return Brushes.MediumOrchid;
                default: return Brushes.Gray;
            }
        }
    }
}

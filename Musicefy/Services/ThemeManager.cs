using System;
using System.Collections.Generic;
using System.Linq;
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

        private static readonly HashSet<string> _themeUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/Themes/Base.xaml",
            "/Themes/ScrollbarTheme.xaml",
            "/Themes/SidebarStyles.xaml",
            "/Themes/Modes/Light.xaml",
            "/Themes/Modes/Dark.xaml",
            "/Themes/Modes/DarkPure.xaml",
            "/Themes/Palettes/Default.Light.xaml",
            "/Themes/Palettes/Default.Dark.xaml",
            "/Themes/Palettes/Catppuccin.xaml",
            "/Themes/Palettes/GreenApple.xaml",
            "/Themes/Palettes/Lavender.xaml",
        };

        public static void ApplyTheme(string mode, string palette)
        {
            // Remove only known theme dictionaries so third-party resources survive
            var merged = Application.Current.Resources.MergedDictionaries;
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var source = merged[i].Source;
                if (source != null && _themeUris.Contains(source.OriginalString))
                    merged.RemoveAt(i);
            }
        
            // Inject foundational underlying design rules first
            MergeDictionary("/Themes/Base.xaml");
        
            // Force re-inject your custom layout template here
            MergeDictionary("/Themes/ScrollbarTheme.xaml");
        
            // Process hardware-level environmental tracking vectors
            if (mode.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                mode = IsSystemDarkMode() ? "Dark" : "Light";
            }
        
            // Route execution flows to handle pure black vs classic gray layouts dynamically
            if (mode.Equals("DarkPure", StringComparison.OrdinalIgnoreCase))
            {
                MergeDictionary("/Themes/Modes/DarkPure.xaml");
            }
            else
            {
                MergeDictionary($"/Themes/Modes/{mode}.xaml");
            }
        
            // Resolve palette adaptations cleanly
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

            // Automatically maps the persistent config flags on startup if user choice matches pure black
            if (mode.Equals("DarkPure", StringComparison.OrdinalIgnoreCase))
            {
                Musicefy.Properties.Settings.Default.PureBlackMode = true;
            }
            else if (mode.Equals("Dark", StringComparison.OrdinalIgnoreCase))
            {
                Musicefy.Properties.Settings.Default.PureBlackMode = false;
            }

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

        public static bool IsSystemDarkMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key?.GetValue("AppsUseLightTheme") is int intVal)
                        return intVal == 0;
                }
            }
            catch
            {
                // Registry read failed; default to dark mode
            }
            
            return true;
        }

        public static void StartSystemThemeWatcher()
        {
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category == UserPreferenceCategory.General)
                {
                    // FIXED: Dispatched to UI thread. Registry change events fire cross-thread and would crash App Resource updates.
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "System|Default";
                        if (savedTheme.StartsWith("System", StringComparison.OrdinalIgnoreCase))
                        {
                            ApplyThemeFromString(savedTheme);
                            AnimateWindowsFade();
                        }
                    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
            };
        }

        public static void AnimateWindowsFade()
        {
            var fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            var windows = Application.Current.Windows.Cast<Window>().ToArray();
            foreach (Window win in windows)
            {
                win.Opacity = 0;
                win.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
                WireButtons(win);
            }
        }

        private static readonly HashSet<Window> _wiredWindows = new HashSet<Window>();

        private static void WireButtons(Window win)
        {
            if (!_wiredWindows.Add(win)) return;
            foreach (var child in FindVisualChildren<System.Windows.Controls.Button>(win))
            {
                child.MouseEnter += Button_MouseEnter;
                child.PreviewMouseDown += Button_MouseDown;
                child.MouseLeave += Button_MouseLeave;
            }
        }

        private static void Button_MouseEnter(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn &&
                Application.Current.FindResource("AccentHoverBrush") is LinearGradientBrush brush)
            {
                AnimateButtonGradient(btn, brush.GradientStops[0].Color, brush.GradientStops[1].Color, 300);
            }
        }

        private static void Button_MouseDown(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn &&
                Application.Current.FindResource("AccentPressedBrush") is LinearGradientBrush brush)
            {
                AnimateButtonGradient(btn, brush.GradientStops[0].Color, brush.GradientStops[1].Color, 200);
            }
        }

        private static void Button_MouseLeave(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn &&
                Application.Current.FindResource("AccentBrush") is LinearGradientBrush brush)
            {
                AnimateButtonGradient(btn, brush.GradientStops[0].Color, brush.GradientStops[1].Color, 300);
            }
        }

        private static void AnimateButtonGradient(System.Windows.Controls.Button btn, Color toStart, Color toEnd, int durationMs)
        {
            if (btn.Template != null && 
                btn.Template.FindName("AccentStart", btn) is GradientStop start &&
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

        public static Brush GetAccentBrush(string name)
        {
            Brush brush;
            switch (name)
            {
                case "Default": 
                    brush = new LinearGradientBrush(Colors.SkyBlue, Colors.DodgerBlue, 45); 
                    break;
                case "Catppuccin": 
                    brush = new SolidColorBrush(Color.FromRgb(245, 194, 231)); 
                    break;
                case "GreenApple": 
                    brush = new SolidColorBrush(Color.FromRgb(29, 185, 84)); 
                    break;
                case "Lavender": 
                    brush = new SolidColorBrush(Color.FromRgb(181, 126, 220)); 
                    break;
                default: 
                    brush = Brushes.Gray; 
                    break;
            }

            brush.Freeze();
            return brush;
        }

        private static void MergeDictionary(string path)
        {
            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new Uri(path, UriKind.Relative) });
        }
    }
}

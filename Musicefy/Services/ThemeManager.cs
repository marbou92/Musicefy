using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Musicefy.Core.Hct;
using Musicefy.Core.Services;

namespace Musicefy.Services
{
    public static class ThemeManager
    {
        private static readonly string[] Modes = { "System", "Light", "Dark", "DarkPure" };

        private static readonly HashSet<string> _themeUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/Themes/Base.xaml",
            "/Themes/ScrollbarTheme.xaml",
            "/Themes/Modes/Light.xaml",
            "/Themes/Modes/Dark.xaml",
            "/Themes/Modes/DarkPure.xaml",
        };

        private static DynamicScheme _currentScheme;

        public static void ApplyTheme(string mode, string paletteName)
        {
            var merged = Application.Current.Resources.MergedDictionaries;
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var source = merged[i].Source;
                if (source != null && _themeUris.Contains(source.OriginalString))
                    merged.RemoveAt(i);
            }

            MergeDictionary("/Themes/Base.xaml");
            MergeDictionary("/Themes/ScrollbarTheme.xaml");

            if (mode.Equals("System", StringComparison.OrdinalIgnoreCase))
                mode = IsSystemDarkMode() ? "Dark" : "Light";

            bool isDarkPure = mode.Equals("DarkPure", StringComparison.OrdinalIgnoreCase);
            if (isDarkPure)
                MergeDictionary("/Themes/Modes/DarkPure.xaml");
            else
                MergeDictionary($"/Themes/Modes/{mode}.xaml");

            bool isDark = mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase);

            var seed = SeedPalettes.All.Find(p =>
                string.Equals(p.Name, paletteName, StringComparison.OrdinalIgnoreCase));

            if (seed == null)
                seed = SeedPalettes.All[0];

            _currentScheme = DynamicScheme.FromSeed(seed, isDark, isDarkPure);
            ApplySchemeToResources(_currentScheme);
        }

        public static void ApplyTheme(string mode) => ApplyTheme(mode, "Default");

        public static void ApplyCustom(string mode, SeedPalette seed)
        {
            var merged = Application.Current.Resources.MergedDictionaries;
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var source = merged[i].Source;
                if (source != null && _themeUris.Contains(source.OriginalString))
                    merged.RemoveAt(i);
            }

            MergeDictionary("/Themes/Base.xaml");
            MergeDictionary("/Themes/ScrollbarTheme.xaml");

            if (mode.Equals("System", StringComparison.OrdinalIgnoreCase))
                mode = IsSystemDarkMode() ? "Dark" : "Light";

            bool isDarkPure = mode.Equals("DarkPure", StringComparison.OrdinalIgnoreCase);
            if (isDarkPure)
                MergeDictionary("/Themes/Modes/DarkPure.xaml");
            else
                MergeDictionary($"/Themes/Modes/{mode}.xaml");

            bool isDark = mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase);
            _currentScheme = DynamicScheme.FromSeed(seed, isDark, isDarkPure);
            ApplySchemeToResources(_currentScheme);
        }

        public static void ApplyThemeFromString(string themeString)
        {
            if (string.IsNullOrWhiteSpace(themeString))
            {
                ApplyTheme("Dark", "Default");
                return;
            }

            var parts = themeString.Split('|');
            string mode = parts.Length > 0 ? parts[0] : "Dark";
            string paletteName = parts.Length > 1 ? parts[1] : "Default";

            if (mode.Equals("DarkPure", StringComparison.OrdinalIgnoreCase))
                Musicefy.Properties.Settings.Default.PureBlackMode = true;
            else if (mode.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                Musicefy.Properties.Settings.Default.PureBlackMode = false;

            ApplyTheme(mode, paletteName);
        }

        public static void SaveTheme(string mode, string paletteName)
        {
            Musicefy.Properties.Settings.Default.Theme = $"{mode}|{paletteName}";
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
            catch { }

            return true;
        }

        public static void StartSystemThemeWatcher()
        {
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category == UserPreferenceCategory.General)
                {
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
            var windows = Application.Current.Windows.Cast<Window>().ToArray();
            foreach (Window win in windows)
            {
                var fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };
                win.Opacity = 0;
                win.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            }
        }

        private static void ApplySchemeToResources(DynamicScheme scheme)
        {
            var resources = Application.Current.Resources;

            SetBrush(resources, "PrimaryBrush", scheme, ToneRole.Primary);
            SetBrush(resources, "OnPrimaryBrush", scheme, ToneRole.OnPrimary);
            SetBrush(resources, "PrimaryContainerBrush", scheme, ToneRole.PrimaryContainer);
            SetBrush(resources, "OnPrimaryContainerBrush", scheme, ToneRole.OnPrimaryContainer);

            SetBrush(resources, "SecondaryBrush", scheme, ToneRole.Secondary);
            SetBrush(resources, "OnSecondaryBrush", scheme, ToneRole.OnSecondary);
            SetBrush(resources, "SecondaryContainerBrush", scheme, ToneRole.SecondaryContainer);
            SetBrush(resources, "OnSecondaryContainerBrush", scheme, ToneRole.OnSecondaryContainer);

            SetBrush(resources, "TertiaryBrush", scheme, ToneRole.Tertiary);
            SetBrush(resources, "OnTertiaryBrush", scheme, ToneRole.OnTertiary);
            SetBrush(resources, "TertiaryContainerBrush", scheme, ToneRole.TertiaryContainer);
            SetBrush(resources, "OnTertiaryContainerBrush", scheme, ToneRole.OnTertiaryContainer);

            SetBrush(resources, "ErrorBrush", scheme, ToneRole.Error);
            SetBrush(resources, "OnErrorBrush", scheme, ToneRole.OnError);
            SetBrush(resources, "ErrorContainerBrush", scheme, ToneRole.ErrorContainer);
            SetBrush(resources, "OnErrorContainerBrush", scheme, ToneRole.OnErrorContainer);

            SetBrushFromArgb(resources, "AccentBrush", scheme.GetAccentArgb(AccentVariant.Default));
            SetBrushFromArgb(resources, "AccentHoverBrush", scheme.GetAccentArgb(AccentVariant.Hover));
            SetBrushFromArgb(resources, "AccentPressedBrush", scheme.GetAccentArgb(AccentVariant.Pressed));
            SetBrushFromArgb(resources, "AccentGlowBrush", scheme.GetAccentArgb(AccentVariant.Glow));
        }

        private static void SetBrush(ResourceDictionary resources, string key, DynamicScheme scheme, ToneRole role)
        {
            int argb = scheme.GetArgb(role);
            var brush = new SolidColorBrush(Color.FromArgb(
                (byte)((argb >> 24) & 0xFF),
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF)));
            brush.Freeze();
            resources[key] = brush;
        }

        private static void SetBrushFromArgb(ResourceDictionary resources, string key, int argb)
        {
            var brush = new SolidColorBrush(Color.FromArgb(
                (byte)((argb >> 24) & 0xFF),
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF)));
            brush.Freeze();
            resources[key] = brush;
        }

        public static void ApplyDynamicColors(ExtractedColors colors)
        {
            if (_currentScheme == null)
            {
                ApplyTheme("Dark", "Default");
            }

            int argb = (255 << 24) | (colors.Primary.R << 16) | (colors.Primary.G << 8) | colors.Primary.B;
            var hct = Hct.FromInt(argb);

            var seed = new SeedPalette(
                "_Dynamic",
                ColorFamily.Vibrant,
                hct.Hue, Math.Max(hct.Chroma, 20));

            bool isDark = _currentScheme != null && _currentScheme.IsDark;
            bool isDarkPure = _currentScheme != null && _currentScheme.IsDarkPure;

            var scheme = DynamicScheme.FromSeed(seed, isDark, isDarkPure);
            ApplySchemeToResources(scheme);
        }

        public static void ClearDynamicColors()
        {
            string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";
            ApplyThemeFromString(savedTheme);
        }

        public static LinearGradientBrush CreateVerticalGradient(Color topColor, Color bottomColor, double topOpacity = 1.0, double bottomOpacity = 1.0)
        {
            var top = topColor;
            var bottom = bottomColor;
            if (topOpacity < 1.0)
                top = Color.FromArgb((byte)(topOpacity * 255), top.R, top.G, top.B);
            if (bottomOpacity < 1.0)
                bottom = Color.FromArgb((byte)(bottomOpacity * 255), bottom.R, bottom.G, bottom.B);

            return new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(top, 0.0),
                    new GradientStop(bottom, 1.0)
                }
            };
        }

        public static LinearGradientBrush CreateHomeGradient(Color dominantColor)
        {
            var surface = Color.FromRgb(24, 24, 24);
            return new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(dominantColor, 0.0),
                    new GradientStop(dominantColor, 0.15),
                    new GradientStop(surface, 1.0)
                }
            };
        }

        private static void MergeDictionary(string path)
        {
            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new Uri(path, UriKind.Relative) });
        }
    }
}

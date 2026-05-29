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
        private static readonly string[] Modes = { "System", "Light", "Dark" };

        private static readonly string[] _modeUris = { "/Themes/Modes/Light.xaml", "/Themes/Modes/Dark.xaml" };

        private static readonly HashSet<string> _themeUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/Themes/Base.xaml",
            "/Themes/ScrollbarTheme.xaml",
            "/Themes/Modes/Light.xaml",
            "/Themes/Modes/Dark.xaml",
        };

        private static DynamicScheme _currentScheme;
        private static readonly TimeSpan _animationDuration = TimeSpan.FromMilliseconds(400);

        private static readonly (string key, ToneRole role)[] _colorKeys =
        {
            ("PrimaryBrush", ToneRole.Primary),
            ("OnPrimaryBrush", ToneRole.OnPrimary),
            ("PrimaryContainerBrush", ToneRole.PrimaryContainer),
            ("OnPrimaryContainerBrush", ToneRole.OnPrimaryContainer),
            ("SecondaryBrush", ToneRole.Secondary),
            ("OnSecondaryBrush", ToneRole.OnSecondary),
            ("SecondaryContainerBrush", ToneRole.SecondaryContainer),
            ("OnSecondaryContainerBrush", ToneRole.OnSecondaryContainer),
            ("TertiaryBrush", ToneRole.Tertiary),
            ("OnTertiaryBrush", ToneRole.OnTertiary),
            ("TertiaryContainerBrush", ToneRole.TertiaryContainer),
            ("OnTertiaryContainerBrush", ToneRole.OnTertiaryContainer),
            ("ErrorBrush", ToneRole.Error),
            ("OnErrorBrush", ToneRole.OnError),
            ("ErrorContainerBrush", ToneRole.ErrorContainer),
            ("OnErrorContainerBrush", ToneRole.OnErrorContainer),
            ("SurfaceBrush", ToneRole.Surface),
            ("OnSurfaceBrush", ToneRole.OnSurface),
            ("SurfaceVariantBrush", ToneRole.SurfaceVariant),
            ("OnSurfaceVariantBrush", ToneRole.OnSurfaceVariant),
            ("OutlineBrush", ToneRole.Outline),
            ("OutlineVariantBrush", ToneRole.OutlineVariant),
            ("SurfaceContainerLowBrush", ToneRole.SurfaceContainerLow),
            ("SurfaceContainerHighBrush", ToneRole.SurfaceContainerHigh),
            ("HoverBrush", ToneRole.Hover),
            ("BackgroundBrush", ToneRole.Surface),
            ("SecondaryBackgroundBrush", ToneRole.SurfaceVariant),
            ("ForegroundBrush", ToneRole.OnSurface),
            ("TextBrush", ToneRole.OnSurface),
            ("MutedTextBrush", ToneRole.OnSurfaceVariant),
            ("BorderBrush", ToneRole.Outline),
            ("DynamicPrimaryBrush", ToneRole.Primary),
        };

        private static readonly (string key, AccentVariant variant)[] _accentKeys =
        {
            ("AccentBrush", AccentVariant.Default),
            ("AccentHoverBrush", AccentVariant.Hover),
            ("AccentPressedBrush", AccentVariant.Pressed),
            ("AccentGlowBrush", AccentVariant.Glow),
        };

        public static void ApplyTheme(string mode, string paletteName, bool? isDarkPureOverride = null)
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

            bool isDarkPure = isDarkPureOverride ?? (mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase) && Musicefy.Properties.Settings.Default.PureBlackMode);
            MergeDictionary($"/Themes/Modes/{mode}.xaml");

            bool isDark = mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase);

            var seed = SeedPalettes.All.Find(p =>
                string.Equals(p.Name, paletteName, StringComparison.OrdinalIgnoreCase));

            if (seed == null)
                seed = SeedPalettes.All[0];

            var newScheme = DynamicScheme.FromColors(seed.PrimaryArgb, seed.SecondaryArgb, seed.TertiaryArgb, seed.NeutralArgb, isDark, isDarkPure);
            ApplySchemeWithAnimation(newScheme);
        }

        public static void ApplyTheme(string mode) => ApplyTheme(mode, "Default");

        public static void ApplyCustom(string mode, SeedPalette seed, bool? isDarkPureOverride = null)
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

            bool isDarkPure = isDarkPureOverride ?? (mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase) && Musicefy.Properties.Settings.Default.PureBlackMode);
            MergeDictionary($"/Themes/Modes/{mode}.xaml");

            bool isDark = mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase);
            var newScheme = DynamicScheme.FromSeed(seed, isDark, isDarkPure);
            ApplySchemeWithAnimation(newScheme);
        }

        public static void ApplyCustomFromColors(string mode, int primaryArgb, int secondaryArgb, int tertiaryArgb, int neutralArgb, bool? isDarkPureOverride = null)
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

            bool isDarkPure = isDarkPureOverride ?? (mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase) && Musicefy.Properties.Settings.Default.PureBlackMode);
            MergeDictionary($"/Themes/Modes/{mode}.xaml");

            bool isDark = mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase);
            var newScheme = DynamicScheme.FromColors(primaryArgb, secondaryArgb, tertiaryArgb, neutralArgb, isDark, isDarkPure);
            ApplySchemeWithAnimation(newScheme);
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

            ApplyTheme(mode, paletteName);
        }

        public static void SaveTheme(string mode, string paletteName)
        {
            string normalMode = mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
            Musicefy.Properties.Settings.Default.Theme = $"{normalMode}|{paletteName}";
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

        private static void ApplySchemeWithAnimation(DynamicScheme newScheme)
        {
            var oldScheme = _currentScheme;
            _currentScheme = newScheme;

            if (oldScheme == null)
            {
                ApplySchemeSnap(newScheme);
                return;
            }

            var resources = Application.Current.Resources;

            foreach (var (key, role) in _colorKeys)
            {
                AnimateBrushColor(resources, key, newScheme.GetArgb(role));
            }

            foreach (var (key, variant) in _accentKeys)
            {
                AnimateBrushColor(resources, key, newScheme.GetAccentArgb(variant));
            }

            SetColorResource(resources, "SkeletonBaseColor", newScheme.GetArgb(ToneRole.SkeletonBase));
            SetColorResource(resources, "SkeletonHighColor", newScheme.GetArgb(ToneRole.SkeletonHigh));

            if (newScheme.IsDarkPure)
            {
                AnimateBrushColor(resources, "SurfaceBrush", (int)0xFF000000);
                AnimateBrushColor(resources, "BackgroundBrush", (int)0xFF000000);
                AnimateBrushColor(resources, "SurfaceContainerLowBrush", (int)0xFF000000);
                AnimateBrushColor(resources, "SurfaceContainerHighBrush", (int)0xFF000000);
            }

            SetPlayerGradientBrush(resources, newScheme, GetPlayerBackgroundStyle());
        }

        private static void ApplySchemeSnap(DynamicScheme scheme)
        {
            var resources = Application.Current.Resources;

            foreach (var (key, role) in _colorKeys)
            {
                SetBrush(resources, key, ArgbsToColor(scheme.GetArgb(role)));
            }

            foreach (var (key, variant) in _accentKeys)
            {
                SetBrush(resources, key, ArgbsToColor(scheme.GetAccentArgb(variant)));
            }

            SetColorResource(resources, "SkeletonBaseColor", scheme.GetArgb(ToneRole.SkeletonBase));
            SetColorResource(resources, "SkeletonHighColor", scheme.GetArgb(ToneRole.SkeletonHigh));

            if (scheme.IsDarkPure)
            {
                var black = new SolidColorBrush(Colors.Black);
                resources["SurfaceBrush"] = black;
                resources["BackgroundBrush"] = black;
                resources["SurfaceContainerLowBrush"] = black;
                resources["SurfaceContainerHighBrush"] = black;
            }

            SetPlayerGradientBrush(resources, scheme, GetPlayerBackgroundStyle());
        }

        private static void AnimateBrushColor(ResourceDictionary resources, string key, int targetArgb)
        {
            var targetColor = ArgbsToColor(targetArgb);
            if (resources[key] is SolidColorBrush brush && !brush.IsFrozen)
            {
                brush.BeginAnimation(SolidColorBrush.ColorProperty,
                    new ColorAnimation(targetColor, _animationDuration)
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                    });
            }
            else
            {
                resources[key] = new SolidColorBrush(targetColor);
            }
        }

        private static void SetBrush(ResourceDictionary resources, string key, Color color)
        {
            resources[key] = new SolidColorBrush(color);
        }

        private static void SetColorResource(ResourceDictionary resources, string key, int argb)
        {
            resources[key] = ArgbsToColor(argb);
        }

        private static Color ArgbsToColor(int argb) => Color.FromArgb(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF));

        public static string GetPlayerBackgroundStyle()
        {
            try { return Musicefy.Properties.Settings.Default.PlayerBackgroundStyle ?? "GRADIENT"; }
            catch { return "GRADIENT"; }
        }

        public static void SetPlayerGradientBrush(ResourceDictionary resources, DynamicScheme scheme, string style)
        {
            var surfaceColor = ArgbsToColor(scheme.GetArgb(ToneRole.Surface));
            var topColor = ArgbsToColor(scheme.PrimaryPalette.GetTone(scheme.IsDark ? 80 : 90));
            var midColor = ArgbsToColor(scheme.PrimaryPalette.GetTone(scheme.IsDark ? 40 : 30));

            resources["PlayerSurfaceColor"] = surfaceColor;
            resources["PlayerPrimaryColor"] = topColor;

            switch (style)
            {
                case "COLORING":
                {
                    var brush = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(0, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(topColor, 0.0),
                            new GradientStop(midColor, 0.5),
                            new GradientStop(Colors.Black, 1.0)
                        }
                    };
                    resources["PlayerGradientBrush"] = brush;
                    resources["PlayerBackgroundSolid"] = new SolidColorBrush(topColor);
                    break;
                }
                case "GLOW":
                {
                    var brush = new RadialGradientBrush
                    {
                        Center = new Point(0.5, 0.3),
                        RadiusX = 1.5,
                        RadiusY = 1.8,
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(topColor, 0.0),
                            new GradientStop(Color.FromArgb(60, topColor.R, topColor.G, topColor.B), 0.5),
                            new GradientStop(surfaceColor, 1.0)
                        }
                    };
                    resources["PlayerGradientBrush"] = brush;
                    resources["PlayerBackgroundSolid"] = new SolidColorBrush(surfaceColor);
                    break;
                }
                default:
                {
                    var brush = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(0, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(topColor, 0.0),
                            new GradientStop(midColor, 0.6),
                            new GradientStop(surfaceColor, 1.0)
                        }
                    };
                    resources["PlayerGradientBrush"] = brush;
                    resources["PlayerBackgroundSolid"] = new SolidColorBrush(Colors.Transparent);
                    break;
                }
            }
        }

        public static void ApplyDynamicColors(ExtractedColors colors)
        {
            if (_currentScheme == null)
            {
                ApplyTheme("Dark", "Default");
            }

            int argb = (255 << 24) | (colors.Primary.R << 16) | (colors.Primary.G << 8) | colors.Primary.B;
            var hct = Hct.FromInt(argb);
            double chroma = Math.Max(hct.Chroma, 20);

            bool isDark = _currentScheme != null && _currentScheme.IsDark;
            bool isDarkPure = _currentScheme != null && _currentScheme.IsDarkPure;

            // Build 4 seed colors from the extracted primary using default offsets
            int primaryArgb = Hct.From(hct.Hue, chroma, 60).ToInt();
            int secondaryArgb = Hct.From(hct.Hue + 30, chroma * 1.1, 60).ToInt();
            int tertiaryArgb = Hct.From(hct.Hue + 60, chroma * 0.8, 60).ToInt();
            int neutralArgb = Hct.From(hct.Hue, 4.0, 60).ToInt();

            var scheme = DynamicScheme.FromColors(primaryArgb, secondaryArgb, tertiaryArgb, neutralArgb, isDark, isDarkPure);
            ApplySchemeWithAnimation(scheme);
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

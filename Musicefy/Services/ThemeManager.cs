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
        // ── Color token map ──────────────────────────────────────────────────
        // Each entry binds a WPF resource key → the ToneRole that drives it.
        // This is the single source of truth: add a key here and it's animated everywhere.
        private static readonly (string key, ToneRole role)[] _colorKeys =
        {
            // Primary
            ("PrimaryBrush",              ToneRole.Primary),
            ("OnPrimaryBrush",            ToneRole.OnPrimary),
            ("PrimaryContainerBrush",     ToneRole.PrimaryContainer),
            ("OnPrimaryContainerBrush",   ToneRole.OnPrimaryContainer),

            // Secondary
            ("SecondaryBrush",              ToneRole.Secondary),
            ("OnSecondaryBrush",            ToneRole.OnSecondary),
            ("SecondaryContainerBrush",     ToneRole.SecondaryContainer),
            ("OnSecondaryContainerBrush",   ToneRole.OnSecondaryContainer),

            // Tertiary
            ("TertiaryBrush",              ToneRole.Tertiary),
            ("OnTertiaryBrush",            ToneRole.OnTertiary),
            ("TertiaryContainerBrush",     ToneRole.TertiaryContainer),
            ("OnTertiaryContainerBrush",   ToneRole.OnTertiaryContainer),

            // Error
            ("ErrorBrush",              ToneRole.Error),
            ("OnErrorBrush",            ToneRole.OnError),
            ("ErrorContainerBrush",     ToneRole.ErrorContainer),
            ("OnErrorContainerBrush",   ToneRole.OnErrorContainer),

            // ── Tonal surfaces (derived from NeutralPalette, not hardcoded gray) ──
            // This is the ArchiveTune approach: surfaces carry a subtle hue bias
            // from the palette so they feel cohesive rather than generic gray.
            ("SurfaceBrush",                  ToneRole.Surface),
            ("OnSurfaceBrush",                ToneRole.OnSurface),
            ("SurfaceVariantBrush",           ToneRole.SurfaceVariant),
            ("OnSurfaceVariantBrush",         ToneRole.OnSurfaceVariant),
            ("SurfaceContainerLowBrush",      ToneRole.SurfaceContainerLow),
            ("SurfaceContainerBrush",         ToneRole.SurfaceContainer),
            ("SurfaceContainerHighBrush",     ToneRole.SurfaceContainerHigh),
            ("SurfaceContainerHighestBrush",  ToneRole.SurfaceContainerHighest),
            ("HoverBrush",                    ToneRole.Hover),
            ("OutlineBrush",                  ToneRole.Outline),
            ("OutlineVariantBrush",           ToneRole.OutlineVariant),
            ("InverseSurfaceBrush",           ToneRole.InverseSurface),
            ("InverseOnSurfaceBrush",         ToneRole.InverseOnSurface),
            ("InversePrimaryBrush",           ToneRole.InversePrimary),

            // Legacy aliases (keep for XAML back-compat)
            ("BackgroundBrush",          ToneRole.Surface),
            ("SecondaryBackgroundBrush", ToneRole.SurfaceContainerLow),
            ("ForegroundBrush",          ToneRole.OnSurface),
            ("TextBrush",                ToneRole.OnSurface),
            ("MutedTextBrush",           ToneRole.OnSurfaceVariant),
            ("BorderBrush",              ToneRole.Outline),
            ("DynamicPrimaryBrush",      ToneRole.Primary),
        };

        private static readonly (string key, AccentVariant variant)[] _accentKeys =
        {
            ("AccentBrush",        AccentVariant.Default),
            ("AccentHoverBrush",   AccentVariant.Hover),
            ("AccentPressedBrush", AccentVariant.Pressed),
            ("AccentGlowBrush",    AccentVariant.Glow),
        };

        private static readonly HashSet<string> _themeUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/Themes/Modes/Light.xaml",
            "/Themes/Modes/Dark.xaml",
            "/Themes/Base.xaml",
            "/Themes/ScrollbarTheme.xaml",
        };

        private static DynamicScheme _currentScheme;
        private static readonly TimeSpan _animDuration = TimeSpan.FromMilliseconds(360);

        // ── Public API ───────────────────────────────────────────────────────

        public static void ApplyTheme(
            string mode,
            string paletteName,
            bool? isDarkPureOverride = null,
            bool? isExactPaletteOverride = null,
            PaletteStyle? styleOverride = null)
        {
            SwapModeDict(mode);

            if (mode.Equals("System", StringComparison.OrdinalIgnoreCase))
                mode = IsSystemDarkMode() ? "Dark" : "Light";

            bool isDark     = mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase);
            bool isDarkPure = isDarkPureOverride ?? (isDark && Musicefy.Properties.Settings.Default.PureBlackMode);
            bool isExact    = isExactPaletteOverride ?? Musicefy.Properties.Settings.Default.ExactPalette;
            PaletteStyle style = styleOverride ?? ParsePaletteStyle(Musicefy.Properties.Settings.Default.PaletteStyle);

            var seed = SeedPalettes.All.Find(p =>
                string.Equals(p.Name, paletteName, StringComparison.OrdinalIgnoreCase))
                ?? SeedPalettes.All[0];

            var scheme = DynamicScheme.FromSeed(seed, isDark, isDarkPure, isExact, style);
            ApplySchemeWithAnimation(scheme);
        }

        public static void ApplyTheme(string mode) => ApplyTheme(mode, "Default");

        public static void ApplyThemeFromString(string themeString)
        {
            if (string.IsNullOrWhiteSpace(themeString)) { ApplyTheme("Dark", "Default"); return; }
            var parts   = themeString.Split('|');
            string mode    = parts.Length > 0 ? parts[0] : "Dark";
            string palette = parts.Length > 1 ? parts[1] : "Default";
            ApplyTheme(mode, palette);
        }

        /// <summary>
        /// Parse a PaletteStyle enum from a settings string.
        /// Falls back to TonalSpot for unrecognized values.
        /// </summary>
        public static PaletteStyle ParsePaletteStyle(string styleStr)
        {
            if (Enum.TryParse<PaletteStyle>(styleStr, ignoreCase: true, out var result))
                return result;
            return PaletteStyle.TonalSpot;
        }

        /// <summary>
        /// Apply a full scheme from extracted album-art colors.
        /// Uses the dominant hue + chroma to reconstruct a complete M3 scheme,
        /// respecting the current mode and pure-black setting.
        /// This is the ArchiveTune approach: album art drives the ENTIRE palette,
        /// not just the accent brush. Vibrant and Muted extracted colors feed
        /// secondary and tertiary channels for a richer, more harmonious scheme.
        /// </summary>
        public static void ApplyDynamicColors(ExtractedColors colors)
        {
            if (_currentScheme == null) ApplyTheme("Dark", "Default");

            int primaryArgb = ColorToArgb(colors.Primary);
            var hct = Hct.FromInt(primaryArgb);

            // Boost chroma so muted artwork still drives a vivid scheme
            double chroma = Math.Max(hct.Chroma, 24.0);
            primaryArgb = Hct.From(hct.Hue, chroma, 60).ToInt();

            // Derive secondary from Vibrant color — gives a complementary accent
            int secondaryArgb;
            if (!AreColorsSimilar(colors.Vibrant, colors.Primary))
            {
                var vibHct = Hct.FromInt(ColorToArgb(colors.Vibrant));
                double vibChroma = Math.Max(vibHct.Chroma, 16.0);
                secondaryArgb = Hct.From(vibHct.Hue, vibChroma, 60).ToInt();
            }
            else
            {
                secondaryArgb = Hct.From(
                    MathUtils.SanitizeDegrees(hct.Hue + 30),
                    Math.Max(chroma * 0.5, 16.0), 60).ToInt();
            }

            // Derive tertiary from Muted color — gives a softer contrast accent
            int tertiaryArgb;
            if (!AreColorsSimilar(colors.Muted, colors.Primary))
            {
                var mutHct = Hct.FromInt(ColorToArgb(colors.Muted));
                double mutChroma = Math.Max(mutHct.Chroma, 12.0);
                tertiaryArgb = Hct.From(mutHct.Hue, mutChroma, 60).ToInt();
            }
            else
            {
                tertiaryArgb = Hct.From(
                    MathUtils.SanitizeDegrees(hct.Hue + 60),
                    Math.Max(chroma * 0.35, 12.0), 60).ToInt();
            }

            // Neutral comes from primary hue at very low chroma (tonal surface)
            int neutralArgb = Hct.From(hct.Hue, Math.Min(chroma * 0.08, 6.0), 60).ToInt();

            bool isDark     = _currentScheme?.IsDark ?? true;
            bool isDarkPure = _currentScheme?.IsDarkPure ?? false;

            // Use Fidelity style for album art — stays true to the source hue
            var scheme = DynamicScheme.FromColors(
                primaryArgb, secondaryArgb, tertiaryArgb, neutralArgb,
                isDark, isDarkPure, isExactPalette: false, PaletteStyle.Fidelity);

            ApplySchemeWithAnimation(scheme);
        }

        /// <summary>
        /// Check if two colors are too similar (hue within 15°, chroma within 10) to be useful as distinct channels.
        /// </summary>
        private static bool AreColorsSimilar(System.Windows.Media.Color a, System.Windows.Media.Color b)
        {
            var hctA = Hct.FromInt(ColorToArgb(a));
            var hctB = Hct.FromInt(ColorToArgb(b));
            double hueDiff = MathUtils.DifferenceDegrees(hctA.Hue, hctB.Hue);
            double chromaDiff = Math.Abs(hctA.Chroma - hctB.Chroma);
            return hueDiff < 15 && chromaDiff < 10;
        }

        private static int ColorToArgb(System.Windows.Media.Color c)
        {
            return (255 << 24) | (c.R << 16) | (c.G << 8) | c.B;
        }

        public static void ClearDynamicColors()
        {
            string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";
            ApplyThemeFromString(savedTheme);
        }

        public static void ApplyCustom(
            string mode,
            SeedPalette seed,
            bool? isDarkPureOverride = null,
            bool? isExactPaletteOverride = null,
            PaletteStyle? styleOverride = null)
        {
            SwapModeDict(mode);
            if (mode.Equals("System", StringComparison.OrdinalIgnoreCase))
                mode = IsSystemDarkMode() ? "Dark" : "Light";

            bool isDark     = mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase);
            bool isDarkPure = isDarkPureOverride ?? (isDark && Musicefy.Properties.Settings.Default.PureBlackMode);
            bool isExact    = isExactPaletteOverride ?? Musicefy.Properties.Settings.Default.ExactPalette;
            PaletteStyle style = styleOverride ?? ParsePaletteStyle(Musicefy.Properties.Settings.Default.PaletteStyle);

            var scheme = DynamicScheme.FromSeed(seed, isDark, isDarkPure, isExact, style);
            ApplySchemeWithAnimation(scheme);
        }

        public static void ApplyCustomFromColors(
            string mode,
            int primaryArgb,
            int secondaryArgb,
            int tertiaryArgb,
            int neutralArgb,
            bool? isDarkPureOverride = null,
            bool? isExactPaletteOverride = null,
            PaletteStyle? styleOverride = null)
        {
            SwapModeDict(mode);
            if (mode.Equals("System", StringComparison.OrdinalIgnoreCase))
                mode = IsSystemDarkMode() ? "Dark" : "Light";

            bool isDark     = mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase);
            bool isDarkPure = isDarkPureOverride ?? (isDark && Musicefy.Properties.Settings.Default.PureBlackMode);
            bool isExact    = isExactPaletteOverride ?? Musicefy.Properties.Settings.Default.ExactPalette;
            PaletteStyle style = styleOverride ?? ParsePaletteStyle(Musicefy.Properties.Settings.Default.PaletteStyle);

            var scheme = DynamicScheme.FromColors(
                primaryArgb, secondaryArgb, tertiaryArgb, neutralArgb,
                isDark, isDarkPure, isExact, style);
            ApplySchemeWithAnimation(scheme);
        }

        public static void SaveTheme(string mode, string paletteName)
        {
            string normalMode = mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
            Musicefy.Properties.Settings.Default.Theme = $"{normalMode}|{paletteName}";
            Musicefy.Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Save the current palette style and exact-palette toggle to settings.
        /// </summary>
        public static void SavePaletteOptions(PaletteStyle style, bool exactPalette)
        {
            Musicefy.Properties.Settings.Default.PaletteStyle = style.ToString();
            Musicefy.Properties.Settings.Default.ExactPalette = exactPalette;
            Musicefy.Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Get the currently saved PaletteStyle from settings.
        /// </summary>
        public static PaletteStyle GetSavedPaletteStyle()
            => ParsePaletteStyle(Musicefy.Properties.Settings.Default.PaletteStyle ?? "TonalSpot");

        /// <summary>
        /// Get the currently saved ExactPalette toggle from settings.
        /// </summary>
        public static bool GetSavedExactPalette()
            => Musicefy.Properties.Settings.Default.ExactPalette;

        // ── Scheme → Resource Dictionaries ────────────────────────────────────

        private static void ApplySchemeWithAnimation(DynamicScheme newScheme)
        {
            var old = _currentScheme;
            _currentScheme = newScheme;

            if (old == null) { ApplySchemeSnap(newScheme); return; }

            var resources = Application.Current.Resources;

            foreach (var (key, role) in _colorKeys)
                AnimateBrushColor(resources, key, newScheme.GetArgb(role));

            foreach (var (key, variant) in _accentKeys)
                AnimateBrushColor(resources, key, newScheme.GetAccentArgb(variant));

            SetColorResource(resources, "SkeletonBaseColor", newScheme.GetArgb(ToneRole.SkeletonBase));
            SetColorResource(resources, "SkeletonHighColor",  newScheme.GetArgb(ToneRole.SkeletonHigh));

            SetPlayerGradientBrush(resources, newScheme, GetPlayerBackgroundStyle());
        }

        private static void ApplySchemeSnap(DynamicScheme scheme)
        {
            var resources = Application.Current.Resources;

            foreach (var (key, role) in _colorKeys)
                SetBrush(resources, key, ArgbsToColor(scheme.GetArgb(role)));

            foreach (var (key, variant) in _accentKeys)
                SetBrush(resources, key, ArgbsToColor(scheme.GetAccentArgb(variant)));

            SetColorResource(resources, "SkeletonBaseColor", scheme.GetArgb(ToneRole.SkeletonBase));
            SetColorResource(resources, "SkeletonHighColor",  scheme.GetArgb(ToneRole.SkeletonHigh));

            SetPlayerGradientBrush(resources, scheme, GetPlayerBackgroundStyle());
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static void SwapModeDict(string mode)
        {
            var merged = Application.Current.Resources.MergedDictionaries;
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var src = merged[i].Source;
                if (src != null && _themeUris.Contains(src.OriginalString))
                    merged.RemoveAt(i);
            }
            MergeDictionary("/Themes/Base.xaml");
            MergeDictionary("/Themes/ScrollbarTheme.xaml");

            string effectiveMode = mode.Equals("System", StringComparison.OrdinalIgnoreCase)
                ? (IsSystemDarkMode() ? "Dark" : "Light")
                : mode;

            MergeDictionary($"/Themes/Modes/{effectiveMode}.xaml");
        }

        private static void AnimateBrushColor(ResourceDictionary res, string key, int targetArgb)
        {
            var targetColor = ArgbsToColor(targetArgb);
            if (res[key] is SolidColorBrush brush && !brush.IsFrozen)
            {
                brush.BeginAnimation(SolidColorBrush.ColorProperty,
                    new ColorAnimation(targetColor, _animDuration)
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    });
            }
            else
            {
                res[key] = new SolidColorBrush(targetColor);
            }
        }

        private static void SetBrush(ResourceDictionary res, string key, Color color)
            => res[key] = new SolidColorBrush(color);

        private static void SetColorResource(ResourceDictionary res, string key, int argb)
            => res[key] = ArgbsToColor(argb);

        private static Color ArgbsToColor(int argb) => Color.FromArgb(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >>  8) & 0xFF),
            (byte)( argb        & 0xFF));

        private static void MergeDictionary(string path)
            => Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new Uri(path, UriKind.Relative) });

        // ── System theme watcher ──────────────────────────────────────────────

        public static bool IsSystemDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int i) return i == 0;
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
                    Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        string saved = Musicefy.Properties.Settings.Default.Theme ?? "System|Default";
                        if (saved.StartsWith("System", StringComparison.OrdinalIgnoreCase))
                        {
                            ApplyThemeFromString(saved);
                            AnimateWindowsFade();
                        }
                    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
            };
        }

        public static void AnimateWindowsFade()
        {
            foreach (Window win in Application.Current.Windows)
            {
                var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400))
                    { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } };
                win.Opacity = 0;
                win.BeginAnimation(UIElement.OpacityProperty, anim);
            }
        }

        // ── Player gradient ────────────────────────────────────────────────────

        public static string GetPlayerBackgroundStyle()
        {
            try { return Musicefy.Properties.Settings.Default.PlayerBackgroundStyle ?? "GRADIENT"; }
            catch { return "GRADIENT"; }
        }

        public static void SetPlayerGradientBrush(
            ResourceDictionary resources,
            DynamicScheme scheme,
            string style)
        {
            // In Exact palette mode the primary color is very close to the seed,
            // so we use a slightly lower tone for contrast in the gradient.
            double topTone = scheme.IsExactPalette
                ? (scheme.IsDark ? 70 : 50)
                : (scheme.IsDark ? 80 : 90);

            var surfaceColor = ArgbsToColor(scheme.GetArgb(ToneRole.Surface));
            var topColor     = ArgbsToColor(scheme.PrimaryPalette.GetTone(topTone));
            var midColor     = ArgbsToColor(scheme.PrimaryPalette.GetTone(scheme.IsDark ? 40 : 30));

            resources["PlayerSurfaceColor"] = surfaceColor;
            resources["PlayerPrimaryColor"] = topColor;

            switch (style)
            {
                case "COLORING":
                    resources["PlayerGradientBrush"] = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(topColor,    0.0),
                            new GradientStop(midColor,    0.5),
                            new GradientStop(Colors.Black, 1.0)
                        }
                    };
                    resources["PlayerBackgroundSolid"] = new SolidColorBrush(topColor);
                    break;

                case "GLOW":
                    resources["PlayerGradientBrush"] = new RadialGradientBrush
                    {
                        Center = new Point(0.5, 0.3), RadiusX = 1.5, RadiusY = 1.8,
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(topColor, 0.0),
                            new GradientStop(Color.FromArgb(60, topColor.R, topColor.G, topColor.B), 0.5),
                            new GradientStop(surfaceColor, 1.0)
                        }
                    };
                    resources["PlayerBackgroundSolid"] = new SolidColorBrush(surfaceColor);
                    break;

                default: // GRADIENT
                    resources["PlayerGradientBrush"] = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(topColor,     0.0),
                            new GradientStop(midColor,     0.6),
                            new GradientStop(surfaceColor, 1.0)
                        }
                    };
                    resources["PlayerBackgroundSolid"] = new SolidColorBrush(Colors.Transparent);
                    break;
            }
        }

        // ── Gradient helpers ───────────────────────────────────────────────────

        public static LinearGradientBrush CreateVerticalGradient(
            Color top, Color bottom, double topOpacity = 1.0, double bottomOpacity = 1.0)
        {
            if (topOpacity    < 1.0) top    = Color.FromArgb((byte)(topOpacity    * 255), top.R,    top.G,    top.B);
            if (bottomOpacity < 1.0) bottom = Color.FromArgb((byte)(bottomOpacity * 255), bottom.R, bottom.G, bottom.B);
            return new LinearGradientBrush
            {
                StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(top,    0.0),
                    new GradientStop(bottom, 1.0)
                }
            };
        }

        public static LinearGradientBrush CreateHomeGradient(Color dominantColor)
        {
            var surface = _currentScheme != null
                ? ArgbsToColor(_currentScheme.GetArgb(ToneRole.Surface))
                : Color.FromRgb(24, 24, 24);

            return new LinearGradientBrush
            {
                StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(dominantColor, 0.0),
                    new GradientStop(dominantColor, 0.15),
                    new GradientStop(surface,       1.0)
                }
            };
        }

        // ── Mode-accurate palette preview ──────────────────────────────────────
        // ArchiveTune shows palette swatches using the CURRENT mode's tones.
        // Call this to get preview colors that match what the palette will look like.
        public static (Color primary, Color secondary, Color tertiary, Color surface)
            GetModeAccuratePreview(SeedPalette seed, bool isDark, PaletteStyle style = PaletteStyle.TonalSpot, bool isExactPalette = false)
        {
            var scheme = DynamicScheme.FromSeed(seed, isDark, false, isExactPalette, style);
            return (
                ArgbsToColor(scheme.GetPreviewPrimaryArgb()),
                ArgbsToColor(scheme.GetPreviewSecondaryArgb()),
                ArgbsToColor(scheme.GetPreviewTertiaryArgb()),
                ArgbsToColor(scheme.GetPreviewSurfaceArgb())
            );
        }
    }
}

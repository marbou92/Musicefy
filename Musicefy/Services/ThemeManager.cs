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

            // ── Tonal surfaces ──
            ("SurfaceBrush",                  ToneRole.Surface),
            ("OnSurfaceBrush",                ToneRole.OnSurface),
            ("SurfaceVariantBrush",           ToneRole.SurfaceVariant),
            ("OnSurfaceVariantBrush",         ToneRole.OnSurfaceVariant),
            ("SurfaceContainerLowestBrush",   ToneRole.SurfaceContainerLowest),
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

        // ── NEW: only touches accent-family brushes, never surfaces ──────────────
        private static readonly (string key, ToneRole role)[] _accentOnlyColorKeys =
        {
            ("PrimaryBrush",              ToneRole.Primary),
            ("OnPrimaryBrush",            ToneRole.OnPrimary),
            ("PrimaryContainerBrush",     ToneRole.PrimaryContainer),
            ("OnPrimaryContainerBrush",   ToneRole.OnPrimaryContainer),
            ("SecondaryBrush",            ToneRole.Secondary),
            ("OnSecondaryBrush",          ToneRole.OnSecondary),
            ("SecondaryContainerBrush",   ToneRole.SecondaryContainer),
            ("OnSecondaryContainerBrush", ToneRole.OnSecondaryContainer),
            ("TertiaryBrush",             ToneRole.Tertiary),
            ("OnTertiaryBrush",           ToneRole.OnTertiary),
            ("TertiaryContainerBrush",    ToneRole.TertiaryContainer),
            ("OnTertiaryContainerBrush",  ToneRole.OnTertiaryContainer),
            ("DynamicPrimaryBrush",       ToneRole.Primary),
            ("InversePrimaryBrush",       ToneRole.InversePrimary),
        };

        // URIs that get swapped when changing mode (Light, Dark, DarkPure)
        private static readonly HashSet<string> _themeUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/Themes/Modes/Light.xaml",
            "/Themes/Modes/Dark.xaml",
            "/Themes/Modes/DarkPure.xaml",
            "/Themes/Base.xaml",
            "/Themes/ScrollbarTheme.xaml",
        };

        private static DynamicScheme _currentScheme;
        // The base scheme from the user's chosen seed palette. When dynamic album
        // colors are applied, we keep the base scheme's neutral palettes so that
        // surface/container colors remain anchored to the palette rather than
        // becoming tinted by the album art's hue (which caused the cyan/lavender
        // sidebar in light mode).
        private static DynamicScheme _baseScheme;
        private static readonly TimeSpan _animDuration = TimeSpan.FromMilliseconds(360);

        // Pauses dynamic color application while user is browsing palette picker
        private static bool _dynamicColorsPaused = false;

        public static void PauseDynamicColors() => _dynamicColorsPaused = true;
        public static void ResumeDynamicColors() => _dynamicColorsPaused = false;

        // ── Public API ───────────────────────────────────────────────────────

        public static void ApplyTheme(
            string mode,
            string paletteName,
            bool? isDarkPureOverride = null,
            bool? isExactPaletteOverride = null,
            PaletteStyle? styleOverride = null,
            bool autoSelectStyle = false)
        {
            bool isDarkPure = isDarkPureOverride ?? (mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase)
                && Musicefy.Properties.Settings.Default.PureBlackMode);

            SwapModeDict(mode, isDarkPure);

            if (mode.Equals("System", StringComparison.OrdinalIgnoreCase))
                mode = IsSystemDarkMode() ? "Dark" : "Light";

            bool isDark     = mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase);
            bool isExact    = isExactPaletteOverride ?? Musicefy.Properties.Settings.Default.ExactPalette;
            PaletteStyle style = styleOverride ?? ParsePaletteStyle(Musicefy.Properties.Settings.Default.PaletteStyle);

            var seed = SeedPalettes.All.Find(p =>
                string.Equals(p.Name, paletteName, StringComparison.OrdinalIgnoreCase))
                ?? SeedPalettes.All[0];

            // ArchiveTune approach: auto-select style based on seed chroma
            if (autoSelectStyle && style == PaletteStyle.TonalSpot)
                style = DynamicScheme.AutoSelectStyle(seed);

            var scheme = DynamicScheme.FromSeed(seed, isDark, isDarkPure, isExact, style);
            _baseScheme = scheme;  // Remember the seed scheme as the base for dynamic colors
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

        public static PaletteStyle ParsePaletteStyle(string styleStr)
        {
            if (Enum.TryParse<PaletteStyle>(styleStr, ignoreCase: true, out var result))
                return result;
            return PaletteStyle.TonalSpot;
        }

        public static void ApplyDynamicColors(ExtractedColors colors)
        {
            // Guard: never run before the base palette is initialised
            if (_baseScheme == null)
            {
                if (_currentScheme == null) ApplyTheme("Dark", "Default");
                else return;
            }

            // Respect pause flag — don't override user's palette picker selection
            if (_dynamicColorsPaused) return;

            // ArchiveTune approach: extract accent colors from album art,
            // but keep surface/neutral colors anchored to the base seed palette.
            // This prevents the cyan/lavender surface contamination in light mode
            // that occurred when the album art hue was used for the neutral palette.

            int primaryArgb = ColorToArgb(colors.Primary);
            var hct = Hct.FromInt(primaryArgb);

            double chroma = Math.Max(hct.Chroma, 24.0);
            primaryArgb = Hct.From(hct.Hue, chroma, 60).ToInt();

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

            // Build dynamic accent palettes from the extracted colors
            var pHct = Hct.FromInt(primaryArgb);
            var sHct = Hct.FromInt(secondaryArgb);
            var tHct = Hct.FromInt(tertiaryArgb);

            bool isDark     = _currentScheme?.IsDark ?? true;
            bool isDarkPure = _currentScheme?.IsDarkPure ?? false;
            double chromaFactor = isDarkPure ? 0.65 : 1.0;

            var dynamicPrimary   = TonalPalette.FromHueAndChroma(pHct.Hue, Math.Max(pHct.Chroma, 36.0) * chromaFactor);
            var dynamicSecondary = TonalPalette.FromHueAndChroma(sHct.Hue, Math.Max(sHct.Chroma, 4.0) * chromaFactor);
            var dynamicTertiary  = TonalPalette.FromHueAndChroma(tHct.Hue, Math.Max(tHct.Chroma, 4.0) * chromaFactor);

            // Use the base seed palette's neutral palettes — NOT album-art-tinted ones.
            // This is the key fix: surfaces stay anchored to the chosen palette.
            var baseScheme = _baseScheme;   // always use _baseScheme, never _currentScheme fallback

            var scheme = DynamicScheme.CreateDynamicAccentScheme(
                baseScheme, dynamicPrimary, dynamicSecondary, dynamicTertiary);

            // ← CHANGED: was ApplySchemeWithAnimation(scheme)
            ApplyAccentOnlyWithAnimation(scheme);
        }

        private static void ApplyAccentOnlyWithAnimation(DynamicScheme accentScheme)
        {
            var resources = Application.Current.Resources;

            foreach (var (key, role) in _accentOnlyColorKeys)
                AnimateBrushColor(resources, key, accentScheme.GetArgb(role));

            foreach (var (key, variant) in _accentKeys)
                AnimateBrushColor(resources, key, accentScheme.GetAccentArgb(variant));

            // Gradient uses accent primary — safe to update, doesn't touch surface brushes
            SetPlayerGradientBrush(resources, accentScheme, GetPlayerBackgroundStyle());
            UpdateHomeGradientBrush(resources, accentScheme);

            // Update _currentScheme so NowPlaying gradient is correct,
            // but DO NOT change surface/neutral brushes — those stay from _baseScheme.
            _currentScheme = accentScheme;
        }

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
            bool isDarkPure = isDarkPureOverride ?? (mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase)
                && Musicefy.Properties.Settings.Default.PureBlackMode);

            SwapModeDict(mode, isDarkPure);
            if (mode.Equals("System", StringComparison.OrdinalIgnoreCase))
                mode = IsSystemDarkMode() ? "Dark" : "Light";

            bool isDark     = mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase);
            bool isExact    = isExactPaletteOverride ?? Musicefy.Properties.Settings.Default.ExactPalette;
            PaletteStyle style = styleOverride ?? ParsePaletteStyle(Musicefy.Properties.Settings.Default.PaletteStyle);

            var scheme = DynamicScheme.FromSeed(seed, isDark, isDarkPure, isExact, style);
            _baseScheme = scheme;  // Custom seed palette becomes the new base
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
            bool isDarkPure = isDarkPureOverride ?? (mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase)
                && Musicefy.Properties.Settings.Default.PureBlackMode);

            SwapModeDict(mode, isDarkPure);
            if (mode.Equals("System", StringComparison.OrdinalIgnoreCase))
                mode = IsSystemDarkMode() ? "Dark" : "Light";

            bool isDark     = mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase);
            bool isExact    = isExactPaletteOverride ?? Musicefy.Properties.Settings.Default.ExactPalette;
            PaletteStyle style = styleOverride ?? ParsePaletteStyle(Musicefy.Properties.Settings.Default.PaletteStyle);

            var scheme = DynamicScheme.FromColors(
                primaryArgb, secondaryArgb, tertiaryArgb, neutralArgb,
                isDark, isDarkPure, isExact, style);
            _baseScheme = scheme;  // Custom color scheme becomes the new base
            ApplySchemeWithAnimation(scheme);
        }

        public static void SaveTheme(string mode, string paletteName)
        {
            string normalMode = mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase) ? "Dark" : "Light";
            Musicefy.Properties.Settings.Default.Theme = $"{normalMode}|{paletteName}";
            Musicefy.Properties.Settings.Default.Save();
        }

        public static void SavePaletteOptions(PaletteStyle style, bool exactPalette)
        {
            Musicefy.Properties.Settings.Default.PaletteStyle = style.ToString();
            Musicefy.Properties.Settings.Default.ExactPalette = exactPalette;
            Musicefy.Properties.Settings.Default.Save();
        }

        public static PaletteStyle GetSavedPaletteStyle()
            => ParsePaletteStyle(Musicefy.Properties.Settings.Default.PaletteStyle ?? "TonalSpot");

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
            UpdateHomeGradientBrush(resources, newScheme);
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
            UpdateHomeGradientBrush(resources, scheme);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Swaps the mode-specific resource dictionary (Light, Dark, or DarkPure).
        /// DarkPure.xaml is loaded when pure black mode is enabled in dark mode;
        /// otherwise Dark.xaml or Light.xaml is used as appropriate.
        /// </summary>
        private static void SwapModeDict(string mode, bool isDarkPure = false)
        {
            var merged = Application.Current.Resources.MergedDictionaries;
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var src = merged[i].Source;
                if (src != null && IsThemeUri(src))
                    merged.RemoveAt(i);
            }
            MergeDictionary("/Themes/Base.xaml");
            MergeDictionary("/Themes/ScrollbarTheme.xaml");

            string effectiveMode = mode.Equals("System", StringComparison.OrdinalIgnoreCase)
                ? (IsSystemDarkMode() ? "Dark" : "Light")
                : mode;

            // In dark mode with PureBlack enabled, load DarkPure.xaml
            // which has AMOLED-black surface fallback values.
            // The DynamicScheme still handles tone overrides at runtime,
            // but this gives proper fallback brushes for design-time.
            if (effectiveMode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase) && isDarkPure)
            {
                MergeDictionary("/Themes/Modes/DarkPure.xaml");
            }
            else if (effectiveMode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase))
            {
                MergeDictionary("/Themes/Modes/Dark.xaml");
            }
            else
            {
                MergeDictionary("/Themes/Modes/Light.xaml");
            }
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

        /// <summary>
        /// Checks whether a ResourceDictionary's Source URI refers to a theme
        /// dictionary that should be swapped when changing modes.
        /// Uses suffix matching because WPF may resolve relative URIs into
        /// absolute pack:// URIs at runtime, causing exact comparison to fail.
        /// </summary>
        private static bool IsThemeUri(Uri src)
        {
            string path = src.OriginalString;
            foreach (var themeUri in _themeUris)
            {
                // Match either exact (e.g. "/Themes/Modes/Dark.xaml")
                // or as a suffix of a pack URI (e.g. "pack://application:,,,/Themes/Modes/Dark.xaml")
                if (string.Equals(path, themeUri, StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(themeUri, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

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

        /// <summary>
        /// Gets the current surface color from the active DynamicScheme.
        /// Used by ViewModels and code-behind to build theme-aware gradients.
        /// </summary>
        public static Color GetCurrentSurfaceColor()
        {
            return _currentScheme != null
                ? ArgbsToColor(_currentScheme.GetArgb(ToneRole.Surface))
                : (IsCurrentModeDark() ? Color.FromRgb(24, 24, 24) : Color.FromRgb(252, 251, 254));
        }

        /// <summary>
        /// Gets the current primary container color (tinted surface) for gradient accents.
        /// </summary>
        public static Color GetCurrentPrimaryContainerColor()
        {
            return _currentScheme != null
                ? ArgbsToColor(_currentScheme.GetArgb(ToneRole.PrimaryContainer))
                : Color.FromRgb(60, 140, 231);
        }

        /// <summary>
        /// Returns true if the current mode is dark (including DarkPure).
        /// </summary>
        public static bool IsCurrentModeDark()
        {
            return _currentScheme?.IsDark ?? true;
        }

        /// <summary>
        /// Updates the HomeGradientBrush resource that the Home view can use
        /// via DynamicResource. This gradient transitions from the dominant
        /// color (from album art or palette) to the current surface color,
        /// so it automatically adapts to both light and dark modes.
        /// </summary>
        public static void UpdateHomeGradientBrush(ResourceDictionary resources, DynamicScheme scheme)
        {
            var surfaceColor = ArgbsToColor(scheme.GetArgb(ToneRole.Surface));
            var primaryColor = ArgbsToColor(scheme.PrimaryPalette.GetTone(scheme.IsDark ? 80 : 40));

            resources["HomeGradientBrush"] = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(primaryColor,  0.0),
                    new GradientStop(primaryColor,  0.15),
                    new GradientStop(surfaceColor,   1.0)
                }
            };

            // Also store the individual colors for code-behind gradient builders
            resources["HomeSurfaceColor"] = surfaceColor;
            resources["HomePrimaryColor"] = primaryColor;
        }

        /// <summary>
        /// Rebuilds the HomeGradientBrush when the dominant color changes
        /// (e.g. from album art). The surface color stays anchored to
        /// the current scheme so it remains correct for light/dark mode.
        /// </summary>
        public static void UpdateHomeGradientWithDominantColor(Color dominantColor)
        {
            if (_currentScheme == null) return;
            var resources = Application.Current.Resources;
            var surfaceColor = ArgbsToColor(_currentScheme.GetArgb(ToneRole.Surface));

            resources["HomeGradientBrush"] = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(dominantColor,  0.0),
                    new GradientStop(dominantColor,  0.15),
                    new GradientStop(surfaceColor,   1.0)
                }
            };
        }

        public static LinearGradientBrush CreateHomeGradient(Color dominantColor)
        {
            var surface = _currentScheme != null
                ? ArgbsToColor(_currentScheme.GetArgb(ToneRole.Surface))
                : (IsCurrentModeDark() ? Color.FromRgb(24, 24, 24) : Color.FromRgb(252, 251, 254));

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

        // ── Raw seed preview (ArchiveTune approach) ─────────────────────────
        // Shows the raw seed color at a neutral tone (60), NOT adjusted by mode.
        public static (Color primary, Color secondary, Color tertiary, Color neutral)
            GetRawSeedPreview(SeedPalette seed, PaletteStyle style = PaletteStyle.TonalSpot)
        {
            var scheme = DynamicScheme.FromSeed(seed, true, false, false, style);
            return (
                ArgbsToColor(scheme.GetRawSeedPrimaryArgb()),
                ArgbsToColor(scheme.GetRawSeedSecondaryArgb()),
                ArgbsToColor(scheme.GetRawSeedTertiaryArgb()),
                ArgbsToColor(scheme.GetRawSeedNeutralArgb())
            );
        }

        // ── Auto-select palette style (ArchiveTune approach) ────────────────
        public static PaletteStyle AutoSelectStyle(SeedPalette seed)
            => DynamicScheme.AutoSelectStyle(seed);

        public static PaletteStyle AutoSelectStyle(double chroma)
            => DynamicScheme.AutoSelectStyle(chroma);

        public static PaletteStyle AutoSelectStyle(int argb)
            => DynamicScheme.AutoSelectStyle(argb);
    }
}

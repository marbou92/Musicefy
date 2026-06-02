using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using Musicefy.Core.Hct;
using Musicefy.Core.Services;
using Musicefy.Core.Theme;

namespace Musicefy.Services
{
    /// <summary>
    /// Theme manager rewritten using the Aniyomi/Mihon model:
    /// <list type="bullet">
    ///   <item><see cref="AppTheme"/> (enum) = a named, pre-defined palette</item>
    ///   <item><see cref="ThemeMode"/> (enum) = brightness mode (System/Light/Dark/Amoled)</item>
    ///   <item>Final ColorScheme = <c>AppThemeColorSchemes.GetColorScheme(appTheme, themeMode)</c></item>
    ///   <item>AMOLED is not a separate palette — it takes the dark palette's accents
    ///         but replaces all surface/background slots with pure black.</item>
    ///   <item><c>AppTheme.Dynamic</c> is the special case for album-art color extraction
    ///         (equivalent to Aniyomi's MONET). Handled by <see cref="ApplyDynamicColors"/>.</item>
    /// </list>
    /// </summary>
    public static class ThemeManager
    {
        // ── State ──────────────────────────────────────────────────────────────
        private static MusicefyColorScheme _currentScheme;
        private static AppTheme            _currentAppTheme;
        private static ThemeMode           _currentThemeMode;
        private static readonly TimeSpan   _animDuration = TimeSpan.FromMilliseconds(360);

        // Pauses dynamic color application while user is browsing palette picker
        private static bool _dynamicColorsPaused = false;

        public static void PauseDynamicColors() => _dynamicColorsPaused = true;
        public static void ResumeDynamicColors() => _dynamicColorsPaused = false;

        // URIs that get swapped when changing mode (Light, Dark, DarkPure)
        private static readonly HashSet<string> _themeUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/Themes/Modes/Light.xaml",
            "/Themes/Modes/Dark.xaml",
            "/Themes/Modes/DarkPure.xaml",
            "/Themes/Base.xaml",
            "/Themes/ScrollbarTheme.xaml",
        };

        // ════════════════════════════════════════════════════════════════════════
        // Primary entry points
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called from App.OnStartup and from the settings controls.
        /// Resolves the static palette for the given AppTheme + ThemeMode,
        /// then applies all brushes.
        /// </summary>
        public static void ApplyTheme(AppTheme appTheme, ThemeMode themeMode)
        {
            _currentAppTheme  = appTheme;
            _currentThemeMode = themeMode;

            bool systemIsDark = IsSystemDarkMode();
            var scheme = AppThemeColorSchemes.GetColorScheme(appTheme, themeMode, systemIsDark);
            _currentScheme = scheme;
            ApplyScheme(scheme);
        }

        /// <summary>
        /// Called when album art changes. Only active when AppTheme == Dynamic.
        /// Uses HCT extraction from album art colors, then builds a
        /// MusicefyColorScheme from the extracted hues.
        /// </summary>
        public static void ApplyDynamicColors(ExtractedColors colors)
        {
            // Guard: only apply dynamic colors when the user chose Dynamic theme
            if (_currentAppTheme != AppTheme.Dynamic) return;

            // Respect pause flag — don't override user's palette picker selection
            if (_dynamicColorsPaused) return;

            var scheme = BuildDynamicSchemeFromColors(colors, _currentThemeMode);
            _currentScheme = scheme;
            ApplyAccentOnly(scheme);
        }

        /// <summary>
        /// Reverts dynamic colors back to the static palette.
        /// </summary>
        public static void ClearDynamicColors()
        {
            ApplyTheme(_currentAppTheme, _currentThemeMode);
        }

        /// <summary>
        /// Called from system theme watcher (registry change) when ThemeMode == System.
        /// </summary>
        public static void OnSystemThemeChanged()
        {
            if (_currentThemeMode == ThemeMode.System)
                ApplyTheme(_currentAppTheme, ThemeMode.System);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Persistence
        // ════════════════════════════════════════════════════════════════════════

        public static void SavePreferences(AppTheme appTheme, ThemeMode themeMode)
        {
            Properties.Settings.Default.AppTheme  = appTheme.ToString();
            Properties.Settings.Default.ThemeMode = themeMode.ToString();
            Properties.Settings.Default.Save();
        }

        public static (AppTheme appTheme, ThemeMode themeMode) LoadPreferences()
        {
            Enum.TryParse(Properties.Settings.Default.AppTheme,  out AppTheme  a);
            Enum.TryParse(Properties.Settings.Default.ThemeMode, out ThemeMode m);
            return (a, m);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Core brush application
        // ════════════════════════════════════════════════════════════════════════

        private static void ApplyScheme(MusicefyColorScheme scheme)
        {
            var old = _currentScheme;
            _currentScheme = scheme;

            var resources = Application.Current.Resources;

            // Swap the XAML mode dictionary (Dark.xaml vs Light.xaml vs DarkPure.xaml)
            SwapModeDict(scheme.IsDark, isAmoled: _currentThemeMode == ThemeMode.Amoled);

            if (old == null)
            {
                // First apply — snap all brushes immediately (no animation)
                foreach (var (key, color) in BuildColorMap(scheme))
                    SetBrush(resources, key, color);

                foreach (var (key, color) in BuildAccentColorMap(scheme))
                    SetBrush(resources, key, color);
            }
            else
            {
                // Animate all brushes to the new scheme values
                foreach (var (key, color) in BuildColorMap(scheme))
                    AnimateBrushColor(resources, key, color);

                foreach (var (key, color) in BuildAccentColorMap(scheme))
                    AnimateBrushColor(resources, key, color);
            }

            // Skeleton colors
            resources["SkeletonBaseColor"] = scheme.IsDark
                ? scheme.SurfaceContainerLow
                : scheme.SurfaceContainerHigh;
            resources["SkeletonHighColor"] = scheme.IsDark
                ? scheme.SurfaceContainerHigh
                : scheme.SurfaceContainerLow;

            // Gradient brushes
            SetPlayerGradientBrush(resources, scheme, GetPlayerBackgroundStyle());
            UpdateHomeGradientBrush(resources, scheme);
        }

        /// <summary>
        /// Applies only accent-family brushes, never surfaces. Used by dynamic album-art colors
        /// so that surface/neutral colors remain anchored to the base palette.
        /// </summary>
        private static void ApplyAccentOnly(MusicefyColorScheme accentScheme)
        {
            var resources = Application.Current.Resources;

            foreach (var (key, color) in BuildAccentOnlyColorMap(accentScheme))
                AnimateBrushColor(resources, key, color);

            foreach (var (key, color) in BuildAccentColorMap(accentScheme))
                AnimateBrushColor(resources, key, color);

            // Gradient uses accent primary — safe to update, doesn't touch surface brushes
            SetPlayerGradientBrush(resources, accentScheme, GetPlayerBackgroundStyle());
            UpdateHomeGradientBrush(resources, accentScheme);
        }

        // ── Color maps ────────────────────────────────────────────────────────

        private static Dictionary<string, Color> BuildColorMap(MusicefyColorScheme s)
            => new()
            {
                // Primary
                ["PrimaryBrush"]              = s.Primary,
                ["OnPrimaryBrush"]            = s.OnPrimary,
                ["PrimaryContainerBrush"]     = s.PrimaryContainer,
                ["OnPrimaryContainerBrush"]   = s.OnPrimaryContainer,
                // Secondary
                ["SecondaryBrush"]            = s.Secondary,
                ["OnSecondaryBrush"]          = s.OnSecondary,
                ["SecondaryContainerBrush"]   = s.SecondaryContainer,
                ["OnSecondaryContainerBrush"] = s.OnSecondaryContainer,
                // Tertiary
                ["TertiaryBrush"]             = s.Tertiary,
                ["OnTertiaryBrush"]           = s.OnTertiary,
                ["TertiaryContainerBrush"]    = s.TertiaryContainer,
                ["OnTertiaryContainerBrush"]  = s.OnTertiaryContainer,
                // Error
                ["ErrorBrush"]                = s.Error,
                ["OnErrorBrush"]              = s.OnError,
                ["ErrorContainerBrush"]       = s.ErrorContainer,
                ["OnErrorContainerBrush"]     = s.OnErrorContainer,
                // Surface
                ["SurfaceBrush"]                  = s.Surface,
                ["OnSurfaceBrush"]                = s.OnSurface,
                ["SurfaceVariantBrush"]           = s.SurfaceVariant,
                ["OnSurfaceVariantBrush"]         = s.OnSurfaceVariant,
                ["SurfaceContainerLowestBrush"]   = s.SurfaceContainerLowest,
                ["SurfaceContainerLowBrush"]      = s.SurfaceContainerLow,
                ["SurfaceContainerBrush"]         = s.SurfaceContainer,
                ["SurfaceContainerHighBrush"]     = s.SurfaceContainerHigh,
                ["SurfaceContainerHighestBrush"]  = s.SurfaceContainerHighest,
                ["OutlineBrush"]                  = s.Outline,
                ["OutlineVariantBrush"]           = s.OutlineVariant,
                ["InverseSurfaceBrush"]           = s.InverseSurface,
                ["InverseOnSurfaceBrush"]         = s.InverseOnSurface,
                ["InversePrimaryBrush"]           = s.InversePrimary,
                ["HoverBrush"]                    = Lerp(s.OnSurface, s.Surface, 0.88f),
                // Legacy aliases
                ["BackgroundBrush"]          = s.Surface,
                ["SecondaryBackgroundBrush"] = s.SurfaceContainerLow,
                ["ForegroundBrush"]          = s.OnSurface,
                ["TextBrush"]                = s.OnSurface,
                ["MutedTextBrush"]           = s.OnSurfaceVariant,
                ["BorderBrush"]              = s.Outline,
                ["DynamicPrimaryBrush"]      = s.Primary,
            };

        /// <summary>
        /// Accent-only map — used by ApplyAccentOnly so surface brushes are not touched.
        /// </summary>
        private static Dictionary<string, Color> BuildAccentOnlyColorMap(MusicefyColorScheme s)
            => new()
            {
                ["PrimaryBrush"]              = s.Primary,
                ["OnPrimaryBrush"]            = s.OnPrimary,
                ["PrimaryContainerBrush"]     = s.PrimaryContainer,
                ["OnPrimaryContainerBrush"]   = s.OnPrimaryContainer,
                ["SecondaryBrush"]            = s.Secondary,
                ["OnSecondaryBrush"]          = s.OnSecondary,
                ["SecondaryContainerBrush"]   = s.SecondaryContainer,
                ["OnSecondaryContainerBrush"] = s.OnSecondaryContainer,
                ["TertiaryBrush"]             = s.Tertiary,
                ["OnTertiaryBrush"]           = s.OnTertiary,
                ["TertiaryContainerBrush"]    = s.TertiaryContainer,
                ["OnTertiaryContainerBrush"]  = s.OnTertiaryContainer,
                ["DynamicPrimaryBrush"]       = s.Primary,
                ["InversePrimaryBrush"]       = s.InversePrimary,
            };

        /// <summary>
        /// Accent variant color map — AccentBrush, AccentHoverBrush, etc.
        /// </summary>
        private static Dictionary<string, Color> BuildAccentColorMap(MusicefyColorScheme s)
            => new()
            {
                ["AccentBrush"]        = s.Primary,
                ["AccentHoverBrush"]   = s.PrimaryContainer,
                ["AccentPressedBrush"] = s.OnPrimaryContainer,
                ["AccentGlowBrush"]    = s.Primary,
            };

        // ── Color interpolation helper ────────────────────────────────────────

        private static Color Lerp(Color a, Color b, float t)
        {
            return Color.FromArgb(
                (byte)(a.A + (b.A - a.A) * t),
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }

        // ════════════════════════════════════════════════════════════════════════
        // Dynamic scheme from album art (HCT — only used for AppTheme.Dynamic)
        // ════════════════════════════════════════════════════════════════════════

        private static MusicefyColorScheme BuildDynamicSchemeFromColors(
            ExtractedColors colors, ThemeMode mode)
        {
            bool isDark = mode switch
            {
                ThemeMode.Light  => false,
                ThemeMode.Dark   => true,
                ThemeMode.Amoled => true,
                _                => IsSystemDarkMode(),
            };

            // Extract accent hues from album art using HCT
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

            // Build the dynamic accent scheme using HCT → DynamicScheme
            var dynamicScheme = DynamicScheme.FromColors(
                primaryArgb, secondaryArgb, tertiaryArgb,
                // Use the Default palette's neutral ARGB for surfaces
                AppThemeColorSchemes.GetColorScheme(AppTheme.Default, isDark ? ThemeMode.Dark : ThemeMode.Light)
                    .SurfaceArgb(),
                isDark, mode == ThemeMode.Amoled);

            // Convert DynamicScheme roles to MusicefyColorScheme
            var scheme = new MusicefyColorScheme
            {
                IsDark                  = dynamicScheme.IsDark,
                Primary                 = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.Primary)),
                OnPrimary               = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.OnPrimary)),
                PrimaryContainer        = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.PrimaryContainer)),
                OnPrimaryContainer      = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.OnPrimaryContainer)),
                Secondary               = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.Secondary)),
                OnSecondary             = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.OnSecondary)),
                SecondaryContainer      = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.SecondaryContainer)),
                OnSecondaryContainer    = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.OnSecondaryContainer)),
                Tertiary                = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.Tertiary)),
                OnTertiary              = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.OnTertiary)),
                TertiaryContainer       = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.TertiaryContainer)),
                OnTertiaryContainer     = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.OnTertiaryContainer)),
                Error                   = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.Error)),
                OnError                 = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.OnError)),
                ErrorContainer          = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.ErrorContainer)),
                OnErrorContainer        = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.OnErrorContainer)),
                Surface                 = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.Surface)),
                OnSurface               = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.OnSurface)),
                SurfaceVariant          = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.SurfaceVariant)),
                OnSurfaceVariant        = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.OnSurfaceVariant)),
                SurfaceContainerLowest  = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.SurfaceContainerLowest)),
                SurfaceContainerLow     = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.SurfaceContainerLow)),
                SurfaceContainer        = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.SurfaceContainer)),
                SurfaceContainerHigh    = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.SurfaceContainerHigh)),
                SurfaceContainerHighest = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.SurfaceContainerHighest)),
                Outline                 = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.Outline)),
                OutlineVariant          = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.OutlineVariant)),
                InverseSurface          = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.InverseSurface)),
                InverseOnSurface        = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.InverseOnSurface)),
                InversePrimary          = ArgbsToColor(dynamicScheme.GetArgb(ToneRole.InversePrimary)),
            };

            // AMOLED override
            if (mode == ThemeMode.Amoled)
                scheme = AppThemeColorSchemes.GetColorScheme(AppTheme.Dynamic, ThemeMode.Amoled, IsSystemDarkMode());

            return scheme;
        }

        // ── HCT helpers (only used for AppTheme.Dynamic) ──────────────────────

        private static bool AreColorsSimilar(System.Windows.Media.Color a, System.Windows.Media.Color b)
        {
            var hctA = Hct.FromInt(ColorToArgb(a));
            var hctB = Hct.FromInt(ColorToArgb(b));
            double hueDiff = MathUtils.DifferenceDegrees(hctA.Hue, hctB.Hue);
            double chromaDiff = Math.Abs(hctA.Chroma - hctB.Chroma);
            return hueDiff < 15 && chromaDiff < 10;
        }

        private static int ColorToArgb(System.Windows.Media.Color c)
            => (255 << 24) | (c.R << 16) | (c.G << 8) | c.B;

        // ════════════════════════════════════════════════════════════════════════
        // Brush helpers
        // ════════════════════════════════════════════════════════════════════════

        private static void AnimateBrushColor(ResourceDictionary res, string key, Color targetColor)
        {
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

        // ════════════════════════════════════════════════════════════════════════
        // Mode dictionary swapping
        // ════════════════════════════════════════════════════════════════════════

        private static void SwapModeDict(bool isDark, bool isAmoled = false)
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

            if (isDark && isAmoled)
                MergeDictionary("/Themes/Modes/DarkPure.xaml");
            else if (isDark)
                MergeDictionary("/Themes/Modes/Dark.xaml");
            else
                MergeDictionary("/Themes/Modes/Light.xaml");
        }

        private static bool IsThemeUri(Uri src)
        {
            string path = src.OriginalString;
            foreach (var themeUri in _themeUris)
            {
                if (string.Equals(path, themeUri, StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(themeUri, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void MergeDictionary(string path)
            => Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new Uri(path, UriKind.Relative) });

        // ════════════════════════════════════════════════════════════════════════
        // System theme watcher
        // ════════════════════════════════════════════════════════════════════════

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
                        if (_currentThemeMode == ThemeMode.System)
                        {
                            OnSystemThemeChanged();
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

        // ════════════════════════════════════════════════════════════════════════
        // Player gradient
        // ════════════════════════════════════════════════════════════════════════

        public static string GetPlayerBackgroundStyle()
        {
            try { return Properties.Settings.Default.PlayerBackgroundStyle ?? "GRADIENT"; }
            catch { return "GRADIENT"; }
        }

        public static void SetPlayerGradientBrush(
            ResourceDictionary resources,
            MusicefyColorScheme scheme,
            string style)
        {
            var surfaceColor = scheme.Surface;
            var topColor     = scheme.Primary;
            var midColor     = scheme.PrimaryContainer;

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

        // ════════════════════════════════════════════════════════════════════════
        // Gradient helpers
        // ════════════════════════════════════════════════════════════════════════

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

        public static Color GetCurrentSurfaceColor()
        {
            return _currentScheme?.Surface
                ?? (IsCurrentModeDark() ? Color.FromRgb(20, 18, 24) : Color.FromRgb(255, 251, 254));
        }

        public static Color GetCurrentPrimaryContainerColor()
        {
            return _currentScheme?.PrimaryContainer ?? Color.FromRgb(255, 218, 212);
        }

        public static bool IsCurrentModeDark()
        {
            return _currentScheme?.IsDark ?? false;
        }

        public static void UpdateHomeGradientBrush(ResourceDictionary resources, MusicefyColorScheme scheme)
        {
            var surfaceColor = scheme.Surface;
            var primaryColor = scheme.Primary;

            double gradientReach = scheme.IsDark ? 0.15 : 0.08;

            resources["HomeGradientBrush"] = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(primaryColor,  0.0),
                    new GradientStop(primaryColor,  gradientReach),
                    new GradientStop(surfaceColor,   1.0)
                }
            };

            resources["HomeSurfaceColor"] = surfaceColor;
            resources["HomePrimaryColor"] = primaryColor;
        }

        public static void UpdateHomeGradientWithDominantColor(Color dominantColor)
        {
            if (_currentScheme == null) return;
            var resources = Application.Current.Resources;
            var surfaceColor = _currentScheme.Surface;

            double gradientReach = _currentScheme.IsDark ? 0.15 : 0.08;

            resources["HomeGradientBrush"] = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(dominantColor,  0.0),
                    new GradientStop(dominantColor,  gradientReach),
                    new GradientStop(surfaceColor,   1.0)
                }
            };
        }

        public static LinearGradientBrush CreateHomeGradient(Color dominantColor)
        {
            var surface = _currentScheme?.Surface
                ?? (IsCurrentModeDark() ? Color.FromRgb(20, 18, 24) : Color.FromRgb(255, 251, 254));

            double gradientReach = IsCurrentModeDark() ? 0.15 : 0.08;

            return new LinearGradientBrush
            {
                StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(dominantColor, 0.0),
                    new GradientStop(dominantColor, gradientReach),
                    new GradientStop(surface,       1.0)
                }
            };
        }

        // ════════════════════════════════════════════════════════════════════════
        // Legacy compatibility helpers
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Legacy entry point used by old code paths. Converts old-style
        /// "Dark|Default" string to the new AppTheme + ThemeMode model.
        /// </summary>
        public static void ApplyThemeFromString(string themeString)
        {
            if (string.IsNullOrWhiteSpace(themeString))
            {
                ApplyTheme(AppTheme.Default, ThemeMode.Dark);
                return;
            }

            var parts = themeString.Split('|');
            string modeStr = parts.Length > 0 ? parts[0] : "Dark";
            string paletteStr = parts.Length > 1 ? parts[1] : "Default";

            ThemeMode mode = modeStr switch
            {
                "Light"  => ThemeMode.Light,
                "System" => ThemeMode.System,
                _        => Properties.Settings.Default.PureBlackMode ? ThemeMode.Amoled : ThemeMode.Dark,
            };

            AppTheme theme = MapOldPaletteName(paletteStr);
            ApplyTheme(theme, mode);
        }

        /// <summary>
        /// Maps old SeedPalette names to the new AppTheme enum values.
        /// </summary>
        public static AppTheme MapOldPaletteName(string oldName)
        {
            return oldName?.ToLowerInvariant() switch
            {
                "emerald" or "lime" or "forest" or "mint" or "pine" or "clover" or "olive" or "sage"
                    => AppTheme.GreenApple,
                "lavender" or "wisteria" or "periwinkle"
                    => AppTheme.Lavender,
                "crimson" or "rose" or "cherry" or "raspberry" or "ruby" or "scarlet" or "burgundy"
                    => AppTheme.StrawberryDaiquiri,
                "navy" or "indigo" or "denim" or "slate"
                    => AppTheme.MidnightDusk,
                "teal" or "cyan" or "aqua" or "turquoise" or "seafoam"
                    => AppTheme.TealTurquoise,
                "azure" or "ocean" or "steel" or "royal" or "cerulean"
                    => AppTheme.TidalWave,
                "violet" or "purple" or "amethyst" or "orchid" or "plum"
                    => AppTheme.Sapphire,
                "pink" or "magenta" or "fuchsia" or "blush" or "rosebud" or "bubblegum"
                    => AppTheme.CottonCandy,
                "tangerine" or "pumpkin" or "apricot" or "amber" or "coral" or "peach" or "carrot"
                    => AppTheme.Cloudflare,
                "lemon" or "gold" or "sunflower" or "honey" or "banana" or "mustard"
                    => AppTheme.Yotsuba,
                "brown" or "sand" or "clay" or "taupe" or "coffee" or "terracotta" or "walnut"
                    => AppTheme.Mocha,
                "gray" or "charcoal" or "warm stone" or "cool stone" or "pearl"
                    => AppTheme.Monochrome,
                "electric blue" or "neon green" or "hot pink" or "cyber yellow" or "volt"
                    => AppTheme.Doom,
                _ => AppTheme.Default,
            };
        }

        // ── ARGB conversion (used by BuildDynamicSchemeFromColors) ────────────

        private static Color ArgbsToColor(int argb) => Color.FromArgb(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >>  8) & 0xFF),
            (byte)( argb        & 0xFF));
    }

    // ── Extension: SurfaceArgb for MusicefyColorScheme ────────────────────────
    // Used by BuildDynamicSchemeFromColors to get a neutral ARGB from the palette.
    public static class MusicefyColorSchemeExtensions
    {
        public static int SurfaceArgb(this MusicefyColorScheme s)
        {
            return (255 << 24) | (s.Surface.R << 16) | (s.Surface.G << 8) | s.Surface.B;
        }
    }
}

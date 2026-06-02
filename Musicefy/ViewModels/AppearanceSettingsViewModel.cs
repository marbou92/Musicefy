using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Musicefy.Core.Theme;
using Musicefy.Services;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// Rewritten using the Aniyomi model: exposes exactly two persisted choices:
    /// <see cref="SelectedAppTheme"/> (a named palette) and <see cref="SelectedThemeMode"/>
    /// (a brightness mode). No more FamilyGroup, PaletteStyle, ExactPalette, or
    /// CustomThemeEditor — the palette picker is a flat list of <see cref="AppThemePreview"/>.
    /// </summary>
    public class AppearanceSettingsViewModel : ViewModelBase
    {
        private ThemeMode _selectedThemeMode;
        private AppTheme  _selectedAppTheme;
        private bool      _isSuppressingThemeApplication;
        private string    _selectedDateFormat;

        public AppearanceSettingsViewModel()
        {
            _isSuppressingThemeApplication = true;

            // Load saved preferences
            var (appTheme, themeMode) = ThemeManager.LoadPreferences();
            _selectedAppTheme  = appTheme;
            _selectedThemeMode = themeMode;

            // Build the flat palette list
            AppThemePreviews = new ObservableCollection<AppThemePreview>();
            RefreshPreviews();

            DateFormats = new ObservableCollection<string> { "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM-dd" };
            _selectedDateFormat = Musicefy.Properties.Settings.Default.DateFormat ?? DateFormats[0];

            _isSuppressingThemeApplication = false;
        }

        // ── Theme Mode (System / Light / Dark / AMOLED) ──────────────────────

        public ThemeMode SelectedThemeMode
        {
            get => _selectedThemeMode;
            set
            {
                if (_selectedThemeMode != value)
                {
                    _selectedThemeMode = value;
                    OnPropertyChanged();
                    if (!_isSuppressingThemeApplication)
                        ApplyTheme();
                }
            }
        }

        // ── App Theme (named palette) ─────────────────────────────────────────

        public AppTheme SelectedAppTheme
        {
            get => _selectedAppTheme;
            set
            {
                if (_selectedAppTheme != value)
                {
                    _selectedAppTheme = value;
                    OnPropertyChanged();
                    if (!_isSuppressingThemeApplication)
                        ApplyTheme();
                }
            }
        }

        // ── Flat palette list ─────────────────────────────────────────────────

        public ObservableCollection<AppThemePreview> AppThemePreviews { get; }

        // ── Dynamic Colors ────────────────────────────────────────────────────

        public bool DynamicColorsEnabled
        {
            get => _selectedAppTheme == AppTheme.Dynamic;
            set
            {
                if (value && _selectedAppTheme != AppTheme.Dynamic)
                {
                    _selectedAppTheme = AppTheme.Dynamic;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedAppTheme));
                    if (!_isSuppressingThemeApplication)
                        ApplyTheme();
                }
                else if (!value && _selectedAppTheme == AppTheme.Dynamic)
                {
                    _selectedAppTheme = AppTheme.Default;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedAppTheme));
                    if (!_isSuppressingThemeApplication)
                    {
                        ApplyTheme();
                        RefreshPreviews();
                    }
                }
            }
        }

        // ── Player Background Style ───────────────────────────────────────────

        public ObservableCollection<string> PlayerBackgroundStyles { get; } =
            new ObservableCollection<string> { "GRADIENT", "COLORING", "GLOW" };

        public int SelectedPlayerBackgroundIndex
        {
            get
            {
                var current = Musicefy.Properties.Settings.Default.PlayerBackgroundStyle ?? "GRADIENT";
                return current switch
                {
                    "COLORING" => 1,
                    "GLOW" => 2,
                    _ => 0
                };
            }
            set
            {
                string[] styles = { "GRADIENT", "COLORING", "GLOW" };
                if (value >= 0 && value < styles.Length)
                {
                    Musicefy.Properties.Settings.Default.PlayerBackgroundStyle = styles[value];
                    OnPropertyChanged();
                    if (!_isSuppressingThemeApplication)
                        ThemeManager.ApplyTheme(_selectedAppTheme, _selectedThemeMode);
                }
            }
        }

        public string PlayerBackgroundStyle => Musicefy.Properties.Settings.Default.PlayerBackgroundStyle ?? "GRADIENT";

        // ── Date Format ───────────────────────────────────────────────────────

        public ObservableCollection<string> DateFormats { get; }
        public string SelectedDateFormat
        {
            get => _selectedDateFormat;
            set { _selectedDateFormat = value; OnPropertyChanged(); }
        }

        // ── Theme application ─────────────────────────────────────────────────

        private void ApplyTheme()
        {
            ThemeManager.ApplyTheme(_selectedAppTheme, _selectedThemeMode);
            ThemeManager.SavePreferences(_selectedAppTheme, _selectedThemeMode);
            RefreshPreviews();
        }

        /// <summary>
        /// Select a palette by AppTheme and apply the theme. Aniyomi behavior:
        /// does NOT close the palette picker so user can keep browsing.
        /// </summary>
        public void SelectAppTheme(AppTheme theme)
        {
            _selectedAppTheme = theme;
            OnPropertyChanged(nameof(SelectedAppTheme));
            OnPropertyChanged(nameof(DynamicColorsEnabled));
            ApplyTheme();
        }

        // ── Preview refresh ───────────────────────────────────────────────────

        private void RefreshPreviews()
        {
            AppThemePreviews.Clear();

            foreach (AppTheme theme in Enum.GetValues(typeof(AppTheme)))
            {
                if (theme == AppTheme.Dynamic) continue; // Dynamic is toggled via the checkbox

                var (primary, surface, secondary, tertiary) =
                    AppThemeColorSchemes.GetPreviewColors(theme);

                var preview = new AppThemePreview
                {
                    Theme         = theme,
                    Label         = FormatThemeLabel(theme),
                    IsSelected    = theme == _selectedAppTheme,
                    PrimaryColor  = primary,
                    SurfaceColor  = surface,
                    SecondaryColor = secondary,
                    TertiaryColor = tertiary,
                };

                AppThemePreviews.Add(preview);
            }
        }

        private static string FormatThemeLabel(AppTheme theme) => theme switch
        {
            AppTheme.Default            => "Default",
            AppTheme.GreenApple         => "Green Apple",
            AppTheme.Lavender           => "Lavender",
            AppTheme.StrawberryDaiquiri => "Strawberry Daiquiri",
            AppTheme.MidnightDusk       => "Midnight Dusk",
            AppTheme.Tako               => "Tako",
            AppTheme.TealTurquoise      => "Teal Turquoise",
            AppTheme.TidalWave          => "Tidal Wave",
            AppTheme.CottonCandy        => "Cotton Candy",
            AppTheme.Cloudflare         => "Cloudflare",
            AppTheme.Doom               => "Doom",
            AppTheme.Mocha              => "Mocha",
            AppTheme.Sapphire           => "Sapphire",
            AppTheme.Nord               => "Nord",
            AppTheme.YinAndYang         => "Yin & Yang",
            AppTheme.Yotsuba            => "Yotsuba",
            AppTheme.Monochrome         => "Monochrome",
            AppTheme.Dynamic            => "Dynamic",
            _                           => theme.ToString(),
        };

        // ── Save / Cancel ─────────────────────────────────────────────────────

        public void Save()
        {
            ThemeManager.SavePreferences(_selectedAppTheme, _selectedThemeMode);
            Musicefy.Properties.Settings.Default.DynamicColorsEnabled = DynamicColorsEnabled;
            Musicefy.Properties.Settings.Default.DateFormat = _selectedDateFormat;
            Musicefy.Properties.Settings.Default.PlayerBackgroundStyle = PlayerBackgroundStyle;
            Musicefy.Properties.Settings.Default.Save();
        }

        public void Cancel()
        {
            var (appTheme, themeMode) = ThemeManager.LoadPreferences();
            ThemeManager.ApplyTheme(appTheme, themeMode);
            _selectedAppTheme  = appTheme;
            _selectedThemeMode = themeMode;
            OnPropertyChanged(nameof(SelectedAppTheme));
            OnPropertyChanged(nameof(SelectedThemeMode));
            RefreshPreviews();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Preview model for each palette card in the flat picker
    // ══════════════════════════════════════════════════════════════════════════

    public class AppThemePreview : INotifyPropertyChanged
    {
        private bool _isSelected;

        public AppTheme  Theme          { get; init; }
        public string    Label          { get; init; }

        // Four swatch colors sampled from the light scheme (always shown in light
        // mode regardless of user's current mode, just like Aniyomi's picker)
        public Color PrimaryColor   { get; init; }
        public Color SurfaceColor   { get; init; }
        public Color SecondaryColor { get; init; }
        public Color TertiaryColor  { get; init; }

        public Brush PrimaryBrush   => new SolidColorBrush(PrimaryColor);
        public Brush SurfaceBrush   => new SolidColorBrush(SurfaceColor);
        public Brush SecondaryBrush => new SolidColorBrush(SecondaryColor);
        public Brush TertiaryBrush  => new SolidColorBrush(TertiaryColor);

        // Border: neutral gray for subtle separation
        public Brush BorderBrush => new SolidColorBrush(Color.FromArgb(60, 128, 128, 128));

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

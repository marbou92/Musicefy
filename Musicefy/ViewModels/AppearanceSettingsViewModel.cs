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

        // ── Sprint 9.1: Theme sub-view navigation ───────────────────────────
        private bool _isThemeView;
        public bool IsMainView => !_isThemeView;
        public bool IsThemeView
        {
            get => _isThemeView;
            set
            {
                _isThemeView = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMainView));
            }
        }

        public ICommand NavigateToThemeCommand { get; }
        public ICommand NavigateBackCommand { get; }

        public AppearanceSettingsViewModel()
        {
            _isSuppressingThemeApplication = true;

            NavigateToThemeCommand = new RelayCommand(_ => IsThemeView = true);
            NavigateBackCommand = new RelayCommand(_ => IsThemeView = false);

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

        public AppTheme  Theme          { get; set; }
        public string    Label          { get; set; }

        // Four swatch colors sampled from the light scheme (always shown in light
        // mode regardless of user's current mode, just like Aniyomi's picker)
        public Color PrimaryColor   { get; set; }
        public Color SurfaceColor   { get; set; }
        public Color SecondaryColor { get; set; }
        public Color TertiaryColor  { get; set; }

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

        // ── Sprint 9: New appearance properties (all auto-save) ─────────────

        public bool HidePlayerThumbnail
        {
            get => Musicefy.Properties.Settings.Default.HidePlayerThumbnail;
            set { Musicefy.Properties.Settings.Default.HidePlayerThumbnail = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public bool CropAlbumArt
        {
            get => Musicefy.Properties.Settings.Default.CropAlbumArt;
            set { Musicefy.Properties.Settings.Default.CropAlbumArt = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public bool ShowCodecOnPlayer
        {
            get => Musicefy.Properties.Settings.Default.ShowCodecOnPlayer;
            set { Musicefy.Properties.Settings.Default.ShowCodecOnPlayer = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public int ThumbnailCornerRadius
        {
            get => Musicefy.Properties.Settings.Default.ThumbnailCornerRadius;
            set { Musicefy.Properties.Settings.Default.ThumbnailCornerRadius = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public int MiniPlayerBackgroundStyleIndex
        {
            get => (Musicefy.Properties.Settings.Default.MiniPlayerBackgroundStyle ?? "FollowTheme") switch
            {
                "Solid" => 1,
                "Gradient" => 2,
                _ => 0
            };
            set
            {
                Musicefy.Properties.Settings.Default.MiniPlayerBackgroundStyle = value switch
                {
                    1 => "Solid",
                    2 => "Gradient",
                    _ => "FollowTheme"
                };
                Musicefy.Properties.Settings.Default.Save();
                OnPropertyChanged();
            }
        }

        public int PlayerSliderStyleIndex
        {
            get => (Musicefy.Properties.Settings.Default.PlayerSliderStyle ?? "Default") switch
            {
                "Wavy" => 1,
                "Squiggly" => 2,
                _ => 0
            };
            set
            {
                Musicefy.Properties.Settings.Default.PlayerSliderStyle = value switch
                {
                    1 => "Wavy",
                    2 => "Squiggly",
                    _ => "Default"
                };
                Musicefy.Properties.Settings.Default.Save();
                OnPropertyChanged();
            }
        }

        public int LyricsTextPositionIndex
        {
            get => string.Equals(Musicefy.Properties.Settings.Default.LyricsTextPosition, "Center", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            set
            {
                Musicefy.Properties.Settings.Default.LyricsTextPosition = value == 1 ? "Center" : "Left";
                Musicefy.Properties.Settings.Default.Save();
                OnPropertyChanged();
            }
        }

        public int LyricsTextSize
        {
            get => Musicefy.Properties.Settings.Default.LyricsTextSize;
            set { Musicefy.Properties.Settings.Default.LyricsTextSize = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public double LyricsLineSpacing
        {
            get => Musicefy.Properties.Settings.Default.LyricsLineSpacing;
            set { Musicefy.Properties.Settings.Default.LyricsLineSpacing = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public bool GlowingLyricsEffect
        {
            get => Musicefy.Properties.Settings.Default.GlowingLyricsEffect;
            set { Musicefy.Properties.Settings.Default.GlowingLyricsEffect = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public bool LyricsBlurInactive
        {
            get => Musicefy.Properties.Settings.Default.LyricsBlurInactive;
            set { Musicefy.Properties.Settings.Default.LyricsBlurInactive = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public bool AutoScrollLyrics
        {
            get => Musicefy.Properties.Settings.Default.AutoScrollLyrics;
            set { Musicefy.Properties.Settings.Default.AutoScrollLyrics = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public int DefaultOpenTabIndex
        {
            get => (Musicefy.Properties.Settings.Default.DefaultOpenTab ?? "Home") switch
            {
                "Search" => 1,
                "Library" => 2,
                _ => 0
            };
            set
            {
                Musicefy.Properties.Settings.Default.DefaultOpenTab = value switch
                {
                    1 => "Search",
                    2 => "Library",
                    _ => "Home"
                };
                Musicefy.Properties.Settings.Default.Save();
                OnPropertyChanged();
            }
        }

        public int GridCellSizeIndex
        {
            get => (Musicefy.Properties.Settings.Default.GridCellSize ?? "Medium") switch
            {
                "Small" => 0,
                "Large" => 2,
                _ => 1
            };
            set
            {
                Musicefy.Properties.Settings.Default.GridCellSize = value switch
                {
                    0 => "Small",
                    2 => "Large",
                    _ => "Medium"
                };
                Musicefy.Properties.Settings.Default.Save();
                OnPropertyChanged();
            }
        }

        public bool ShowLikedPlaylist
        {
            get => Musicefy.Properties.Settings.Default.ShowLikedPlaylist;
            set { Musicefy.Properties.Settings.Default.ShowLikedPlaylist = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public bool ShowDownloadedPlaylist
        {
            get => Musicefy.Properties.Settings.Default.ShowDownloadedPlaylist;
            set { Musicefy.Properties.Settings.Default.ShowDownloadedPlaylist = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }
    }
}

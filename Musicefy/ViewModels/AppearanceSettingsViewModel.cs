using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Musicefy.Core.Hct;
using Musicefy.Services;

namespace Musicefy.ViewModels
{
    public class AppearanceSettingsViewModel : ViewModelBase
    {
        private int _selectedThemeIndex;
        private string _selectedDateFormat;
        private bool _isSuppressingThemeApplication;
        private string _activePaletteName;
        private PaletteStyle _selectedPaletteStyle;
        private bool _exactPaletteEnabled;

        public AppearanceSettingsViewModel()
        {
            _isSuppressingThemeApplication = true;

            string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";
            var parts = savedTheme.Split('|');
            string mode = parts.Length > 0 ? parts[0] : "Dark";
            string palette = parts.Length > 1 ? parts[1] : "Default";

            _selectedThemeIndex = mode switch
            {
                "System" => 0,
                "Light" => 1,
                _ => 2
            };

            _selectedPaletteStyle = ThemeManager.GetSavedPaletteStyle();
            _exactPaletteEnabled = ThemeManager.GetSavedExactPalette();

            PaletteStyles = new ObservableCollection<PaletteStyleOption>
            {
                new("Tonal Spot",  PaletteStyle.TonalSpot),
                new("Vibrant",     PaletteStyle.Vibrant),
                new("Expressive",  PaletteStyle.Expressive),
                new("Fidelity",    PaletteStyle.Fidelity),
                new("Neutral",     PaletteStyle.Neutral),
                new("Monochrome",  PaletteStyle.Monochrome),
                new("Rainbow",     PaletteStyle.Rainbow),
                new("Fruit Salad", PaletteStyle.FruitSalad),
            };

            FamilyGroups = new ObservableCollection<FamilyGroup>();
            DateFormats = new ObservableCollection<string> { "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM-dd" };
            _selectedDateFormat = Musicefy.Properties.Settings.Default.DateFormat ?? DateFormats[0];

            RefreshPreviews(palette);

            OpenPaletteSubspaceCommand = new RelayCommand(_ => IsPaletteSubspaceOpen = true);
            ClosePaletteSubspaceCommand = new RelayCommand(_ => { IsPaletteSubspaceOpen = false; IsCustomThemeEditorOpen = false; });
            SelectSeedRoleCommand = new RelayCommand(o => SelectSeedRole(Convert.ToInt32(o)));
            CustomThemeCommand = new RelayCommand(_ => ExecuteCustomTheme());
            ImportThemeCommand = new RelayCommand(_ => ExecuteImportTheme());
            ApplyCustomThemeCommand = new RelayCommand(_ => ExecuteApplyCustomTheme());
            CloseCustomThemeCommand = new RelayCommand(_ => ExecuteCloseCustomTheme());

            InitCustomColorsFromDefault();

            _isSuppressingThemeApplication = false;
        }

        private void ExecuteCustomTheme()
        {
            IsCustomThemeEditorOpen = true;
            SelectedSeedRole = 0;
            SyncRgbFromSeed(0);
        }

        private void ExecuteApplyCustomTheme()
        {
            int pArgb = ColorToArgb(CustomPrimaryColor);
            int sArgb = ColorToArgb(CustomSecondaryColor);
            int tArgb = ColorToArgb(CustomTertiaryColor);
            int nArgb = ColorToArgb(CustomNeutralColor);

            ThemeManager.ApplyCustomFromColors(
                GetModeFromIndex(_selectedThemeIndex), pArgb, sArgb, tArgb, nArgb,
                styleOverride: _selectedPaletteStyle,
                isExactPaletteOverride: _exactPaletteEnabled);
            IsCustomThemeEditorOpen = false;
        }

        private void ExecuteCloseCustomTheme()
        {
            IsCustomThemeEditorOpen = false;
            SelectedSeedRole = 0;
            SyncRgbFromSeed(0);
        }

        private void ExecuteImportTheme()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Theme files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Import Theme"
            };
            if (dialog.ShowDialog() == true)
            {
                System.Windows.MessageBox.Show($"Theme imported from:\n{dialog.FileName}", "Theme Imported");
            }
        }

        public int SelectedThemeIndex
        {
            get => _selectedThemeIndex;
            set
            {
                if (_selectedThemeIndex != value)
                {
                    _selectedThemeIndex = value;
                    OnPropertyChanged();
                    if (!_isSuppressingThemeApplication) ApplyTheme();
                }
            }
        }

        public ObservableCollection<FamilyGroup> FamilyGroups { get; }
        public ObservableCollection<string> DateFormats { get; }

        public string SelectedDateFormat
        {
            get => _selectedDateFormat;
            set { _selectedDateFormat = value; OnPropertyChanged(); }
        }

        public bool DynamicColorsEnabled
        {
            get => Musicefy.Properties.Settings.Default.DynamicColorsEnabled;
            set
            {
                if (Musicefy.Properties.Settings.Default.DynamicColorsEnabled != value)
                {
                    Musicefy.Properties.Settings.Default.DynamicColorsEnabled = value;
                    OnPropertyChanged();

                    // When disabled, immediately revert to the base seed palette scheme
                    // so the user sees the change without needing a track change.
                    if (!value)
                        ThemeManager.ClearDynamicColors();
                }
            }
        }

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
                    if (!_isSuppressingThemeApplication) ThemeManager.ApplyThemeFromString(
                        Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default");
                }
            }
        }

        public string PlayerBackgroundStyle => Musicefy.Properties.Settings.Default.PlayerBackgroundStyle ?? "GRADIENT";

        public bool PureBlackMode
        {
            get => Musicefy.Properties.Settings.Default.PureBlackMode;
            set
            {
                if (Musicefy.Properties.Settings.Default.PureBlackMode != value)
                {
                    Musicefy.Properties.Settings.Default.PureBlackMode = value;
                    OnPropertyChanged();
                    if (!_isSuppressingThemeApplication) ApplyTheme();
                }
            }
        }

        // ── Palette Style selection (ArchiveTune feature) ─────────────────
        public ObservableCollection<PaletteStyleOption> PaletteStyles { get; }

        public PaletteStyle SelectedPaletteStyle
        {
            get => _selectedPaletteStyle;
            set
            {
                if (_selectedPaletteStyle != value)
                {
                    _selectedPaletteStyle = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedPaletteStyleIndex));
                    if (!_isSuppressingThemeApplication)
                    {
                        ApplyTheme();
                        RefreshPreviews(_activePaletteName);
                    }
                }
            }
        }

        public int SelectedPaletteStyleIndex
        {
            get
            {
                for (int i = 0; i < PaletteStyles.Count; i++)
                    if (PaletteStyles[i].Style == _selectedPaletteStyle) return i;
                return 0;
            }
            set
            {
                if (value >= 0 && value < PaletteStyles.Count)
                    SelectedPaletteStyle = PaletteStyles[value].Style;
            }
        }

        // ── Exact Palette toggle (ArchiveTune feature) ────────────────────
        public bool ExactPaletteEnabled
        {
            get => _exactPaletteEnabled;
            set
            {
                if (_exactPaletteEnabled != value)
                {
                    _exactPaletteEnabled = value;
                    OnPropertyChanged();
                    if (!_isSuppressingThemeApplication)
                    {
                        ApplyTheme();
                        RefreshPreviews(_activePaletteName);
                    }
                }
            }
        }

        public ICommand OpenPaletteSubspaceCommand { get; }
        public ICommand ClosePaletteSubspaceCommand { get; }
        public ICommand SelectSeedRoleCommand { get; }
        public ICommand CustomThemeCommand { get; }
        public ICommand ImportThemeCommand { get; }
        public ICommand ApplyCustomThemeCommand { get; }
        public ICommand CloseCustomThemeCommand { get; }

        #region Palette Subspace

        private bool _isPaletteSubspaceOpen;

        public bool IsPaletteSubspaceOpen
        {
            get => _isPaletteSubspaceOpen;
            set
            {
                _isPaletteSubspaceOpen = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMainSettingsVisible));
                OnPropertyChanged(nameof(IsPaletteSubspaceVisible));
            }
        }

        public bool IsMainSettingsVisible => !_isPaletteSubspaceOpen;
        public bool IsPaletteSubspaceVisible => _isPaletteSubspaceOpen;

        #endregion

        #region Custom Theme Editor (RGB)

        private bool _isCustomThemeEditorOpen;
        private int _selectedSeedRole;
        private int _customR;
        private int _customG;
        private int _customB;

        public bool IsCustomThemeEditorOpen
        {
            get => _isCustomThemeEditorOpen;
            set
            {
                _isCustomThemeEditorOpen = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCustomThemeEditorHidden));
            }
        }

        public bool IsCustomThemeEditorHidden => !_isCustomThemeEditorOpen;

        public int SelectedSeedRole
        {
            get => _selectedSeedRole;
            set
            {
                if (_selectedSeedRole != value)
                {
                    _selectedSeedRole = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsPrimarySelected));
                    OnPropertyChanged(nameof(IsSecondarySelected));
                    OnPropertyChanged(nameof(IsTertiarySelected));
                    OnPropertyChanged(nameof(IsNeutralSelected));
                    OnPropertyChanged(nameof(SeedRoleLabel));
                    SyncRgbFromSeed(value);
                }
            }
        }

        public bool IsPrimarySelected => _selectedSeedRole == 0;
        public bool IsSecondarySelected => _selectedSeedRole == 1;
        public bool IsTertiarySelected => _selectedSeedRole == 2;
        public bool IsNeutralSelected => _selectedSeedRole == 3;

        public string SeedRoleLabel => _selectedSeedRole switch
        {
            0 => "Primary",
            1 => "Secondary",
            2 => "Tertiary",
            3 => "Neutral",
            _ => "Primary"
        };

        public int CustomR
        {
            get => _customR;
            set
            {
                value = Math.Max(0, Math.Min(255, value));
                if (_customR != value)
                {
                    _customR = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CustomRDisplay));
                    UpdateSeedFromRgb();
                }
            }
        }

        public int CustomG
        {
            get => _customG;
            set
            {
                value = Math.Max(0, Math.Min(255, value));
                if (_customG != value)
                {
                    _customG = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CustomGDisplay));
                    UpdateSeedFromRgb();
                }
            }
        }

        public int CustomB
        {
            get => _customB;
            set
            {
                value = Math.Max(0, Math.Min(255, value));
                if (_customB != value)
                {
                    _customB = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CustomBDisplay));
                    UpdateSeedFromRgb();
                }
            }
        }

        public string CustomRDisplay => _customR.ToString();
        public string CustomGDisplay => _customG.ToString();
        public string CustomBDisplay => _customB.ToString();

        public Color CustomPrimaryColor { get; private set; }
        public Color CustomSecondaryColor { get; private set; }
        public Color CustomTertiaryColor { get; private set; }
        public Color CustomNeutralColor { get; private set; }

        public Brush CustomPrimaryBrush => new SolidColorBrush(CustomPrimaryColor);
        public Brush CustomSecondaryBrush => new SolidColorBrush(CustomSecondaryColor);
        public Brush CustomTertiaryBrush => new SolidColorBrush(CustomTertiaryColor);
        public Brush CustomNeutralBrush => new SolidColorBrush(CustomNeutralColor);

        public Brush ActiveSeedBrush => _selectedSeedRole switch
        {
            0 => CustomPrimaryBrush,
            1 => CustomSecondaryBrush,
            2 => CustomTertiaryBrush,
            3 => CustomNeutralBrush,
            _ => CustomPrimaryBrush
        };

        private void InitCustomColorsFromDefault()
        {
            var firstSeed = SeedPalettes.All[0];
            CustomPrimaryColor = ArgbToColor(firstSeed.PrimaryArgb);
            CustomSecondaryColor = ArgbToColor(firstSeed.SecondaryArgb);
            CustomTertiaryColor = ArgbToColor(firstSeed.TertiaryArgb);
            CustomNeutralColor = ArgbToColor(firstSeed.NeutralArgb);
            OnPropertyChanged(nameof(CustomPrimaryColor));
            OnPropertyChanged(nameof(CustomSecondaryColor));
            OnPropertyChanged(nameof(CustomTertiaryColor));
            OnPropertyChanged(nameof(CustomNeutralColor));
            OnPropertyChanged(nameof(CustomPrimaryBrush));
            OnPropertyChanged(nameof(CustomSecondaryBrush));
            OnPropertyChanged(nameof(CustomTertiaryBrush));
            OnPropertyChanged(nameof(CustomNeutralBrush));
        }

        private void SyncRgbFromSeed(int roleIndex)
        {
            Color c = roleIndex switch
            {
                0 => CustomPrimaryColor,
                1 => CustomSecondaryColor,
                2 => CustomTertiaryColor,
                3 => CustomNeutralColor,
                _ => CustomPrimaryColor
            };

            _customR = c.R;
            _customG = c.G;
            _customB = c.B;
            OnPropertyChanged(nameof(CustomR));
            OnPropertyChanged(nameof(CustomG));
            OnPropertyChanged(nameof(CustomB));
            OnPropertyChanged(nameof(CustomRDisplay));
            OnPropertyChanged(nameof(CustomGDisplay));
            OnPropertyChanged(nameof(CustomBDisplay));
        }

        private void UpdateSeedFromRgb()
        {
            Color newColor = Color.FromArgb(255, (byte)_customR, (byte)_customG, (byte)_customB);

            switch (_selectedSeedRole)
            {
                case 0:
                    CustomPrimaryColor = newColor;
                    OnPropertyChanged(nameof(CustomPrimaryColor));
                    OnPropertyChanged(nameof(CustomPrimaryBrush));
                    break;
                case 1:
                    CustomSecondaryColor = newColor;
                    OnPropertyChanged(nameof(CustomSecondaryColor));
                    OnPropertyChanged(nameof(CustomSecondaryBrush));
                    break;
                case 2:
                    CustomTertiaryColor = newColor;
                    OnPropertyChanged(nameof(CustomTertiaryColor));
                    OnPropertyChanged(nameof(CustomTertiaryBrush));
                    break;
                case 3:
                    CustomNeutralColor = newColor;
                    OnPropertyChanged(nameof(CustomNeutralColor));
                    OnPropertyChanged(nameof(CustomNeutralBrush));
                    break;
            }

            OnPropertyChanged(nameof(ActiveSeedBrush));
        }

        private void SelectSeedRole(int roleIndex)
        {
            SelectedSeedRole = roleIndex;
        }

        #endregion

        /// <summary>
        /// Select a palette and apply the theme. ArchiveTune behavior:
        /// does NOT close the palette subspace so user can keep browsing.
        /// </summary>
        public void SelectPalette(string paletteName)
        {
            _activePaletteName = paletteName;           // ← ensure field is set first

            ThemeManager.ApplyTheme(
                GetModeFromIndex(_selectedThemeIndex), paletteName,
                styleOverride: _selectedPaletteStyle,
                isExactPaletteOverride: _exactPaletteEnabled,
                autoSelectStyle: true);

            RefreshPreviews(paletteName);

            // Persist immediately so the choice survives a restart
            ThemeManager.SaveTheme(
                GetModeFromIndex(_selectedThemeIndex), paletteName);
            Musicefy.Properties.Settings.Default.PaletteStyle = _selectedPaletteStyle.ToString();
            Musicefy.Properties.Settings.Default.ExactPalette = _exactPaletteEnabled;
            Musicefy.Properties.Settings.Default.Save();
        }

        public void Save()
        {
            string mode = GetModeFromIndex(_selectedThemeIndex);
            Musicefy.Properties.Settings.Default.Theme = $"{mode}|{_activePaletteName}";
            Musicefy.Properties.Settings.Default.DynamicColorsEnabled = DynamicColorsEnabled;
            Musicefy.Properties.Settings.Default.DateFormat = _selectedDateFormat;
            Musicefy.Properties.Settings.Default.PlayerBackgroundStyle = PlayerBackgroundStyle;
            Musicefy.Properties.Settings.Default.PaletteStyle = _selectedPaletteStyle.ToString();
            Musicefy.Properties.Settings.Default.ExactPalette = _exactPaletteEnabled;
            Musicefy.Properties.Settings.Default.Save();
        }

        public void Cancel()
        {
            string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";
            var parts = savedTheme.Split('|');
            string palette = parts.Length > 1 ? parts[1] : "Default";

            ThemeManager.ApplyThemeFromString(savedTheme);
            RefreshPreviews(palette);
        }

        private string GetModeFromIndex(int index)
        {
            if (index == 0) return "System";
            if (index == 1) return "Light";
            return "Dark";
        }

        private string GetCurrentPalette()
        {
            var selected = FamilyGroups
                .SelectMany(g => g.Previews)
                .FirstOrDefault(p => p.IsSelected);
            return selected?.CardName ?? "Default";
        }

        private void ApplyTheme()
        {
            string mode = GetModeFromIndex(_selectedThemeIndex);
            string palette = GetCurrentPalette();
            _activePaletteName = palette;
            ThemeManager.ApplyTheme(mode, palette,
                styleOverride: _selectedPaletteStyle,
                isExactPaletteOverride: _exactPaletteEnabled,
                autoSelectStyle: true);
            RefreshPreviews(palette);
        }

        private void RefreshPreviews(string activePalette)
        {
            FamilyGroups.Clear();

            string mode = GetModeFromIndex(_selectedThemeIndex);
            if (mode.Equals("System", StringComparison.OrdinalIgnoreCase))
                mode = ThemeManager.IsSystemDarkMode() ? "Dark" : "Light";

            bool isDark = mode.StartsWith("Dark", StringComparison.OrdinalIgnoreCase);

            var grouped = SeedPalettes.All
                .GroupBy(s => s.Family)
                .OrderBy(g => (int)g.Key);

            foreach (var grp in grouped)
            {
                var familyGroup = new FamilyGroup
                {
                    FamilyName = FormatFamilyName(grp.Key),
                    IsExpanded = true
                };

                foreach (var seed in grp)
                {
                    bool isSelected = seed.Name.Equals(activePalette, StringComparison.OrdinalIgnoreCase);
                    if (isSelected) _activePaletteName = seed.Name;

                    // ArchiveTune: palette cards show mode-accurate colors so the user
                    // can see exactly what the theme will look like in the current mode.
                    PaletteStyle autoStyle = _selectedPaletteStyle == PaletteStyle.TonalSpot
                        ? DynamicScheme.AutoSelectStyle(seed)
                        : _selectedPaletteStyle;

                    var (primary, secondary, tertiary, surface) =
                        ThemeManager.GetModeAccuratePreview(seed, isDark, autoStyle, _exactPaletteEnabled);

                    // OnSurface for the selected checkmark visibility
                    var scheme = DynamicScheme.FromSeed(seed, isDark, false, _exactPaletteEnabled, autoStyle);
                    Color onSurface = ArgbToColor(scheme.GetArgb(ToneRole.OnSurface));

                    var preview = new ThemePreview
                    {
                        CardName = seed.Name,
                        Family = seed.Family,
                        IsSelected = isSelected,
                        PrimaryColor = primary,
                        SecondaryColor = secondary,
                        TertiaryColor = tertiary,
                        SurfaceColor = surface,
                        OnSurfaceColor = onSurface,
                    };

                    familyGroup.Previews.Add(preview);
                }

                FamilyGroups.Add(familyGroup);
            }
        }

        private static Color ArgbToColor(int argb)
        {
            return Color.FromArgb(255,
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF));
        }

        private static int ColorToArgb(Color c)
        {
            return (255 << 24) | (c.R << 16) | (c.G << 8) | c.B;
        }

        private static string FormatFamilyName(ColorFamily family)
        {
            return family switch
            {
                ColorFamily.Reds => "Reds",
                ColorFamily.Oranges => "Oranges",
                ColorFamily.Yellows => "Yellows",
                ColorFamily.Greens => "Greens",
                ColorFamily.Teals => "Teals",
                ColorFamily.Blues => "Blues",
                ColorFamily.Indigos => "Indigos",
                ColorFamily.Purples => "Purples",
                ColorFamily.Pinks => "Pinks",
                ColorFamily.Earth => "Earth & Neutral Warm",
                ColorFamily.Seasonal => "Seasonal",
                ColorFamily.Vibrant => "Vibrant",
                ColorFamily.Neutral => "Neutral Cool",
                _ => family.ToString()
            };
        }
    }

    public class FamilyGroup : INotifyPropertyChanged
    {
        private bool _isExpanded = true;

        public string FamilyName { get; set; }
        public ObservableCollection<ThemePreview> Previews { get; } = new ObservableCollection<ThemePreview>();

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// Model for each palette card. Shows mode-accurate preview colors so the
    /// circle accurately represents what the theme will look like when applied.
    /// Surface color forms the background; primary/secondary/tertiary are accent
    /// dots on top — the ArchiveTune approach.
    /// </summary>
    public class ThemePreview : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string CardName { get; set; }
        public ColorFamily Family { get; set; }

        // Mode-accurate colors — what the user will actually see
        public Color PrimaryColor { get; set; }
        public Color SecondaryColor { get; set; }
        public Color TertiaryColor { get; set; }
        public Color SurfaceColor { get; set; }
        public Color OnSurfaceColor { get; set; }

        public Brush PrimaryBrush => new SolidColorBrush(PrimaryColor);
        public Brush SecondaryBrush => new SolidColorBrush(SecondaryColor);
        public Brush TertiaryBrush => new SolidColorBrush(TertiaryColor);
        public Brush SurfaceBrush => new SolidColorBrush(SurfaceColor);
        public Brush OnSurfaceBrush => new SolidColorBrush(OnSurfaceColor);

        // Border: use OnSurface at low opacity so it's visible on any surface
        public Brush BorderBrush => new SolidColorBrush(Color.FromArgb(60, OnSurfaceColor.R, OnSurfaceColor.G, OnSurfaceColor.B));

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

    public class PaletteStyleOption
    {
        public string DisplayName { get; }
        public PaletteStyle Style { get; }

        public PaletteStyleOption(string displayName, PaletteStyle style)
        {
            DisplayName = displayName;
            Style = style;
        }

        public override string ToString() => DisplayName;
    }
}

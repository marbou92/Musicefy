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

            FamilyGroups = new ObservableCollection<FamilyGroup>();
            DateFormats = new ObservableCollection<string> { "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM-dd" };
            _selectedDateFormat = Musicefy.Properties.Settings.Default.DateFormat ?? DateFormats[0];

            RefreshPreviews(palette);

            CustomThemeCommand = new RelayCommand(_ => ExecuteCustomTheme());
            ImportThemeCommand = new RelayCommand(_ => ExecuteImportTheme());
            ApplyCustomThemeCommand = new RelayCommand(_ => ExecuteApplyCustomTheme());
            CloseCustomThemeCommand = new RelayCommand(_ => ExecuteCloseCustomTheme());

            _isSuppressingThemeApplication = false;
        }

        private void ExecuteCustomTheme()
        {
            IsCustomThemeEditorOpen = true;
            RefreshCustomPreviewColors();
        }

        private void ExecuteApplyCustomTheme()
        {
            var seed = new SeedPalette(
                "_Custom",
                ColorFamily.Vibrant,
                _customHue, _customChroma,
                _customSecondaryOffset, 1.1,
                _customTertiaryOffset, 0.8,
                _customNeutralChroma);

            ThemeManager.ApplyCustom(GetModeFromIndex(_selectedThemeIndex), seed);
            IsCustomThemeEditorOpen = false;
        }

        private void ExecuteCloseCustomTheme()
        {
            IsCustomThemeEditorOpen = false;
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
                }
            }
        }

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

        public ICommand CustomThemeCommand { get; }
        public ICommand ImportThemeCommand { get; }
        public ICommand ApplyCustomThemeCommand { get; }
        public ICommand CloseCustomThemeCommand { get; }

        #region Custom Theme Editor

        private bool _isCustomThemeEditorOpen;
        private double _customHue = 264;
        private double _customChroma = 44;
        private double _customSecondaryOffset = 30;
        private double _customTertiaryOffset = 60;
        private double _customNeutralChroma = 4;

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

        public double CustomHue
        {
            get => _customHue;
            set
            {
                _customHue = Math.Max(0, Math.Min(360, value));
                OnPropertyChanged();
                RefreshCustomPreviewColors();
            }
        }

        public double CustomChroma
        {
            get => _customChroma;
            set
            {
                _customChroma = Math.Max(0, Math.Min(200, value));
                OnPropertyChanged();
                RefreshCustomPreviewColors();
            }
        }

        public double CustomSecondaryOffset
        {
            get => _customSecondaryOffset;
            set
            {
                _customSecondaryOffset = Math.Max(-180, Math.Min(180, value));
                OnPropertyChanged();
                RefreshCustomPreviewColors();
            }
        }

        public double CustomTertiaryOffset
        {
            get => _customTertiaryOffset;
            set
            {
                _customTertiaryOffset = Math.Max(-180, Math.Min(180, value));
                OnPropertyChanged();
                RefreshCustomPreviewColors();
            }
        }

        public double CustomNeutralChroma
        {
            get => _customNeutralChroma;
            set
            {
                _customNeutralChroma = Math.Max(0, Math.Min(20, value));
                OnPropertyChanged();
                RefreshCustomPreviewColors();
            }
        }

        public Color CustomPrimaryColor { get; private set; }
        public Color CustomSecondaryColor { get; private set; }
        public Color CustomTertiaryColor { get; private set; }
        public Color CustomNeutralColor { get; private set; }

        public Brush CustomPrimaryBrush => new SolidColorBrush(CustomPrimaryColor);
        public Brush CustomSecondaryBrush => new SolidColorBrush(CustomSecondaryColor);
        public Brush CustomTertiaryBrush => new SolidColorBrush(CustomTertiaryColor);
        public Brush CustomNeutralBrush => new SolidColorBrush(CustomNeutralColor);

        private void RefreshCustomPreviewColors()
        {
            CustomPrimaryColor = SeedToColor(_customHue, _customChroma);
            CustomSecondaryColor = SeedToColor(_customHue + _customSecondaryOffset, _customChroma * 1.1);
            CustomTertiaryColor = SeedToColor(_customHue + _customTertiaryOffset, _customChroma * 0.8);
            CustomNeutralColor = SeedToColor(_customHue, _customNeutralChroma);
            OnPropertyChanged(nameof(CustomPrimaryColor));
            OnPropertyChanged(nameof(CustomSecondaryColor));
            OnPropertyChanged(nameof(CustomTertiaryColor));
            OnPropertyChanged(nameof(CustomNeutralColor));
            OnPropertyChanged(nameof(CustomPrimaryBrush));
            OnPropertyChanged(nameof(CustomSecondaryBrush));
            OnPropertyChanged(nameof(CustomTertiaryBrush));
            OnPropertyChanged(nameof(CustomNeutralBrush));
        }

        #endregion

        public void SelectPalette(string paletteName)
        {
            ThemeManager.ApplyTheme(GetModeFromIndex(_selectedThemeIndex), paletteName);
            RefreshPreviews(paletteName);
        }

        public void Save()
        {
            string themeString = $"{GetModeFromIndex(_selectedThemeIndex)}|{_activePaletteName}";
            ThemeManager.SaveTheme(GetModeFromIndex(_selectedThemeIndex), _activePaletteName);

            Musicefy.Properties.Settings.Default.Theme = themeString;
            Musicefy.Properties.Settings.Default.DynamicColorsEnabled = DynamicColorsEnabled;
            Musicefy.Properties.Settings.Default.DateFormat = _selectedDateFormat;
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
            return PureBlackMode ? "DarkPure" : "Dark";
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
            ThemeManager.ApplyTheme(mode, palette);
            RefreshPreviews(palette);
        }

        private void RefreshPreviews(string activePalette)
        {
            FamilyGroups.Clear();

            string mode = GetModeFromIndex(_selectedThemeIndex);
            if (mode.Equals("System", StringComparison.OrdinalIgnoreCase))
                mode = ThemeManager.IsSystemDarkMode() ? "Dark" : "Light";

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

                    var preview = new ThemePreview
                    {
                        CardName = seed.Name,
                        Family = seed.Family,
                        IsSelected = isSelected,
                        PrimarySeed = SeedToColor(seed.PrimaryHue, seed.PrimaryChroma),
                        SecondarySeed = SeedToColor(
                            seed.PrimaryHue + seed.SecondaryHueOffset,
                            seed.PrimaryChroma * seed.SecondaryChromaRatio),
                        TertiarySeed = SeedToColor(
                            seed.PrimaryHue + seed.TertiaryHueOffset,
                            seed.PrimaryChroma * seed.TertiaryChromaRatio),
                        NeutralSeed = SeedToColor(seed.PrimaryHue, seed.NeutralChroma),
                    };

                    familyGroup.Previews.Add(preview);
                }

                FamilyGroups.Add(familyGroup);
            }
        }

        private static Color SeedToColor(double hue, double chroma)
        {
            int argb = Hct.From(hue, Math.Max(chroma, 0.5), 60).ToInt();
            return Color.FromArgb(255,
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF));
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

    public class ThemePreview : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string CardName { get; set; }
        public ColorFamily Family { get; set; }
        public Color PrimarySeed { get; set; }
        public Color SecondarySeed { get; set; }
        public Color TertiarySeed { get; set; }
        public Color NeutralSeed { get; set; }

        public Brush PrimaryBrush => new SolidColorBrush(PrimarySeed);
        public Brush SecondaryBrush => new SolidColorBrush(SecondarySeed);
        public Brush TertiaryBrush => new SolidColorBrush(TertiarySeed);
        public Brush NeutralBrush => new SolidColorBrush(NeutralSeed);

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(BorderBrush));
                }
            }
        }

        public Brush BorderBrush => IsSelected
            ? (Brush)Application.Current.FindResource("AccentBrush")
            : Brushes.Transparent;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

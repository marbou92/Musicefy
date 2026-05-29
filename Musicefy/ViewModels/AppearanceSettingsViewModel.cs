using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Musicefy.Services;

namespace Musicefy.ViewModels
{
    public class AppearanceSettingsViewModel : ViewModelBase
    {

        private int _selectedThemeIndex;
        private string _selectedDateFormat;
        private bool _isSuppressingThemeApplication = false;

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

            ThemePreviews = new ObservableCollection<ThemePreview>();
            DateFormats = new ObservableCollection<string> { "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM-dd" };
            _selectedDateFormat = Musicefy.Properties.Settings.Default.DateFormat ?? DateFormats[0];

            RefreshPreviews(palette);

            CustomThemeCommand = new RelayCommand(_ => ExecuteCustomTheme());
            ImportThemeCommand = new RelayCommand(_ => ExecuteImportTheme());

            _isSuppressingThemeApplication = false;
        }

        private void ExecuteCustomTheme()
        {
            System.Windows.MessageBox.Show("Custom theme editor coming soon", "Coming Soon");
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

        public ObservableCollection<ThemePreview> ThemePreviews { get; }
        public ObservableCollection<string> DateFormats { get; }

        public string SelectedDateFormat
        {
            get => _selectedDateFormat;
            set { _selectedDateFormat = value; OnPropertyChanged(); }
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

        public ThemePreview SelectedPalettePreview
        {
            get => ThemePreviews.FirstOrDefault(p => p.IsSelected);
            set
            {
                if (value != null && !value.IsSelected)
                {
                    SelectPalette(value.CardName);
                    OnPropertyChanged();
                }
            }
        }

        public ICommand CustomThemeCommand { get; }
        public ICommand ImportThemeCommand { get; }

        public void SelectPalette(string paletteName)
        {
            ThemeManager.ApplyTheme(GetModeFromIndex(_selectedThemeIndex), paletteName);
            RefreshPreviews(paletteName);
        }

        public void Save()
        {
            string themeString = $"{GetModeFromIndex(_selectedThemeIndex)}|{GetCurrentPalette()}";
            ThemeManager.SaveTheme(themeString);

            Musicefy.Properties.Settings.Default.Theme = themeString;
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
            var selected = ThemePreviews.FirstOrDefault(tp => tp.IsSelected);
            return selected?.CardName ?? "Default";
        }

        private void ApplyTheme()
        {
            string mode = GetModeFromIndex(_selectedThemeIndex);
            string palette = GetCurrentPalette();

            ThemeManager.ApplyTheme(mode, palette);
            RefreshPreviews(palette);
        }

        private void RefreshPreviews(string activePalette)
        {
            ThemePreviews.Clear();

            string mode = GetModeFromIndex(_selectedThemeIndex);
            if (mode.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                mode = ThemeManager.IsSystemDarkMode() ? "Dark" : "Light";
            }

            AddPreviewCard("Default", mode, activePalette);
            AddPreviewCard("Catppuccin", mode, activePalette);
            AddPreviewCard("GreenApple", mode, activePalette);
            AddPreviewCard("Lavender", mode, activePalette);
        }

        private void AddPreviewCard(string paletteName, string mode, string activePalette)
        {
            Brush bg;
            Color darkColor;
            if (mode.Equals("Light", StringComparison.OrdinalIgnoreCase))
            {
                bg = Brushes.White;
                darkColor = Color.FromRgb(224, 224, 224);
            }
            else if (mode.Equals("DarkPure", StringComparison.OrdinalIgnoreCase))
            {
                bg = Brushes.Black;
                darkColor = Color.FromRgb(0, 0, 0);
            }
            else
            {
                var comfortGray = new SolidColorBrush(Color.FromRgb(36, 36, 36));
                comfortGray.Freeze();
                bg = comfortGray;
                darkColor = Color.FromRgb(26, 26, 26);
            }

            var preview = new ThemePreview
            {
                CardName = paletteName,
                AccentBrush = GetAccentBrush(paletteName),
                BackgroundBrush = bg,
                IsSelected = paletteName.Equals(activePalette, StringComparison.OrdinalIgnoreCase),
                DarkColor = darkColor
            };

            ThemePreviews.Add(preview);
        }

        private static Brush GetAccentBrush(string paletteName)
        {
            return paletteName switch
            {
                "Default" => new SolidColorBrush(Color.FromRgb(30, 136, 229)),
                "Catppuccin" => new SolidColorBrush(Color.FromRgb(245, 194, 231)),
                "GreenApple" => new SolidColorBrush(Color.FromRgb(29, 185, 84)),
                "Lavender" => new SolidColorBrush(Color.FromRgb(181, 126, 220)),
                _ => new SolidColorBrush(Colors.Gray),
            };
        }

    }

    public class ThemePreview : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string CardName { get; set; }
        public Brush AccentBrush { get; set; }
        public Brush BackgroundBrush { get; set; }
        public Color DarkColor { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HighlightBrush));
                }
            }
        }

        public Brush HighlightBrush => IsSelected
            ? (Brush)Application.Current.FindResource("AccentBrush")
            : Brushes.Transparent;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

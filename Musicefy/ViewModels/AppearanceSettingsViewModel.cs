using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Musicefy.Services;

namespace Musicefy.ViewModels
{
    public class AppearanceSettingsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private int _selectedThemeIndex;
        private string _selectedDateFormat;

        public AppearanceSettingsViewModel()
        {
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
            RefreshPreviews(palette);

            DateFormats = new ObservableCollection<string> { "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM-dd" };
            _selectedDateFormat = Musicefy.Properties.Settings.Default.DateFormat ?? DateFormats[0];
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
                    ApplyTheme();
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
                    ApplyTheme();
                }
            }
        }

        // ADD THIS PROPERTY TO AppearanceSettingsViewModel.cs
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

        private string GetModeFromIndex(int index) =>
            index switch { 0 => "System", 1 => "Light", _ => "Dark" };

        private string GetCurrentPalette()
        {
            var selected = ThemePreviews.FirstOrDefault(tp => tp.IsSelected);
            return selected?.CardName ?? "Default";
        }

        private void ApplyTheme()
        {
            string mode = GetModeFromIndex(_selectedThemeIndex);
            string palette = GetCurrentPalette();

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new Uri("/Themes/Base.xaml", UriKind.Relative) });

            if (mode.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                mode = ThemeManager.IsSystemDarkMode() ? "Dark" : "Light";
            }

            if (mode == "Dark" && PureBlackMode)
            {
                Application.Current.Resources.MergedDictionaries.Add(
                    new ResourceDictionary { Source = new Uri("/Themes/Modes/DarkPure.xaml", UriKind.Relative) });
            }
            else
            {
                Application.Current.Resources.MergedDictionaries.Add(
                    new ResourceDictionary { Source = new Uri($"/Themes/Modes/{mode}.xaml", UriKind.Relative) });
            }

            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new Uri($"/Themes/Palettes/{palette}.xaml", UriKind.Relative) });

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
            Brush bg = (mode == "Dark" && PureBlackMode && paletteName == "Default") 
                ? Brushes.Black 
                : (mode == "Dark" ? Brushes.DarkGray : Brushes.White);

            ThemePreviews.Add(new ThemePreview
            {
                CardName = paletteName,
                AccentBrush = ThemeManager.GetAccentBrush(paletteName),
                BackgroundBrush = bg,
                IsSelected = paletteName.Equals(activePalette, StringComparison.OrdinalIgnoreCase)
            });
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class ThemePreview : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string CardName { get; set; }
        public Brush AccentBrush { get; set; }
        public Brush BackgroundBrush { get; set; }

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

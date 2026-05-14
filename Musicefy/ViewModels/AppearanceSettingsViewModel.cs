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
            // Load saved theme string
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
            RefreshPreviews();

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

        public void SelectPalette(string paletteName)
        {
            foreach (var preview in ThemePreviews)
                preview.IsSelected = preview.CardName.Equals(paletteName, StringComparison.OrdinalIgnoreCase);

            ThemeManager.ApplyTheme(GetModeFromIndex(_selectedThemeIndex), paletteName);
            RefreshPreviews();
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
            ThemeManager.ApplyThemeFromString(savedTheme);
            RefreshPreviews();
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
                bool isDark = ThemeManager.IsSystemDarkMode();
                mode = isDark ? "Dark" : "Light";
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

            RefreshPreviews();
        }

        private void RefreshPreviews()
        {
            ThemePreviews.Clear();

            string mode = GetModeFromIndex(_selectedThemeIndex);
            if (mode.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                bool isDark = ThemeManager.IsSystemDarkMode();
                mode = isDark ? "Dark" : "Light";
            }

            // Default card
            if (mode == "Dark" && PureBlackMode)
            {
                ThemePreviews.Add(new ThemePreview
                {
                    CardName = "Default (Pure Black)",
                    AccentBrush = ThemeManager.GetAccentBrush("Default"),
                    BackgroundBrush = Brushes.Black,
                    IsSelected = true
                });
            }
            else
            {
                ThemePreviews.Add(new ThemePreview
                {
                    CardName = "Default",
                    AccentBrush = ThemeManager.GetAccentBrush("Default"),
                    BackgroundBrush = mode == "Dark" ? Brushes.DarkGray : Brushes.White,
                    IsSelected = true
                });
            }

            // Other palettes
            ThemePreviews.Add(new ThemePreview
            {
                CardName = "Catppuccin",
                AccentBrush = ThemeManager.GetAccentBrush("Catppuccin"),
                BackgroundBrush = mode == "Dark" ? Brushes.DarkGray : Brushes.White
            });

            ThemePreviews.Add(new ThemePreview
            {
                CardName = "GreenApple",
                AccentBrush = ThemeManager.GetAccentBrush("GreenApple"),
                BackgroundBrush = mode == "Dark" ? Brushes.DarkGray : Brushes.White
            });

            ThemePreviews.Add(new ThemePreview
            {
                CardName = "Lavender",
                AccentBrush = ThemeManager.GetAccentBrush("Lavender"),
                BackgroundBrush = mode == "Dark" ? Brushes.DarkGray : Brushes.White
            });
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class ThemePreview
    {
        public string CardName { get; set; }
        public Brush AccentBrush { get; set; }
        public Brush BackgroundBrush { get; set; }
        public bool IsSelected { get; set; }

        // Highlight border/glow for selected card
        public Brush HighlightBrush => IsSelected ? Brushes.DeepSkyBlue : Brushes.Transparent;
    }
}

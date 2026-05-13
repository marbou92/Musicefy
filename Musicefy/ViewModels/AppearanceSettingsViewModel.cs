using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
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

            // Palettes (real ones from Themes/Palettes)
            ThemePreviews = new ObservableCollection<ThemePreview>
            {
                new ThemePreview { Name = "Default", AccentBrush = ThemeManager.GetAccentBrush("Default") },
                new ThemePreview { Name = "Catppuccin", AccentBrush = ThemeManager.GetAccentBrush("Catppuccin") },
                new ThemePreview { Name = "GreenApple", AccentBrush = ThemeManager.GetAccentBrush("GreenApple") },
                new ThemePreview { Name = "Lavender", AccentBrush = ThemeManager.GetAccentBrush("Lavender") }
            };

            // Date formats
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
                    ApplyTheme(); // immediately re-apply theme when toggled
                }
            }
        }

        public void SelectPalette(string paletteName)
        {
            ThemeManager.ApplyTheme(GetModeFromIndex(_selectedThemeIndex), paletteName);
        }

        public void Save()
        {
            string themeString = $"{GetModeFromIndex(_selectedThemeIndex)}|{GetCurrentPalette()}";
            ThemeManager.SaveTheme(themeString);
            Musicefy.Properties.Settings.Default.DateFormat = _selectedDateFormat;
            Musicefy.Properties.Settings.Default.Save();
        }

        public void Cancel()
        {
            string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";
            ThemeManager.ApplyThemeFromString(savedTheme);
        }

        private string GetModeFromIndex(int index) =>
            index switch { 0 => "System", 1 => "Light", _ => "Dark" };

        private string GetCurrentPalette() =>
            Musicefy.Properties.Settings.Default.Theme?.Split('|').Length > 1
                ? Musicefy.Properties.Settings.Default.Theme.Split('|')[1]
                : "Default";

        private void ApplyTheme()
        {
            string mode = GetModeFromIndex(_selectedThemeIndex);
            string palette = GetCurrentPalette();

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new Uri("/Themes/Base.xaml", UriKind.Relative) });

            if (mode.Equals("System", System.StringComparison.OrdinalIgnoreCase))
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

            if (palette.Equals("Default", System.StringComparison.OrdinalIgnoreCase))
            {
                string paletteFile = mode == "Dark" ? "Default.Dark.xaml" : "Default.Light.xaml";
                Application.Current.Resources.MergedDictionaries.Add(
                    new ResourceDictionary { Source = new Uri($"/Themes/Palettes/{paletteFile}", UriKind.Relative) });
            }
            else
            {
                Application.Current.Resources.MergedDictionaries.Add(
                    new ResourceDictionary { Source = new Uri($"/Themes/Palettes/{palette}.xaml", UriKind.Relative) });
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class ThemePreview
    {
        public string Name { get; set; }
        public System.Windows.Media.Brush AccentBrush { get; set; }
    }
}

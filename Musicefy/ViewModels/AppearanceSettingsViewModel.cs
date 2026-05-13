using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Musicefy.Services;

namespace Musicefy.ViewModels
{
    public class AppearanceSettingsViewModel : INotifyPropertyChanged
    {
        private string _mode;
        private string _palette;
        private int _selectedThemeIndex;
        private bool _pureBlackMode;
        private bool _relativeTimestamps;
        private bool _renderImages;
        private bool _showUpdates;
        private string _selectedDateFormat;

        public event PropertyChangedEventHandler PropertyChanged;

        public AppearanceSettingsViewModel()
        {
            // Load saved theme string
            string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";
            var parts = savedTheme.Split('|');
            _mode = parts.Length > 0 ? parts[0] : "Dark";
            _palette = parts.Length > 1 ? parts[1] : "Default";

            // Map mode to tab index
            _selectedThemeIndex = _mode switch
            {
                "System" => 0,
                "Light" => 1,
                _ => 2
            };

            // Example toggles (could be extended)
            _pureBlackMode = false;
            _relativeTimestamps = true;
            _renderImages = true;
            _showUpdates = true;

            // Example date formats
            DateFormats = new ObservableCollection<string> { "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM-dd" };
            _selectedDateFormat = DateFormats[0];

            // Theme previews
            ThemePreviews = new ObservableCollection<ThemePreview>
            {
                new ThemePreview { Name = "Default", AccentBrush = ThemeManager.GetAccentBrush("Default") },
                new ThemePreview { Name = "Blue", AccentBrush = ThemeManager.GetAccentBrush("Blue") },
                new ThemePreview { Name = "Red", AccentBrush = ThemeManager.GetAccentBrush("Red") }
            };
        }

        public int SelectedThemeIndex
        {
            get => _selectedThemeIndex;
            set
            {
                if (_selectedThemeIndex != value)
                {
                    _selectedThemeIndex = value;
                    _mode = value switch
                    {
                        0 => "System",
                        1 => "Light",
                        _ => "Dark"
                    };
                    ApplyTheme();
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<ThemePreview> ThemePreviews { get; }

        public bool PureBlackMode
        {
            get => _pureBlackMode;
            set { _pureBlackMode = value; OnPropertyChanged(); }
        }

        public bool RelativeTimestamps
        {
            get => _relativeTimestamps;
            set { _relativeTimestamps = value; OnPropertyChanged(); }
        }

        public bool RenderImages
        {
            get => _renderImages;
            set { _renderImages = value; OnPropertyChanged(); }
        }

        public bool ShowUpdates
        {
            get => _showUpdates;
            set { _showUpdates = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> DateFormats { get; }
        public string SelectedDateFormat
        {
            get => _selectedDateFormat;
            set { _selectedDateFormat = value; OnPropertyChanged(); }
        }

        public void SelectPalette(string paletteName)
        {
            _palette = paletteName;
            ApplyTheme();
        }

        public void Save()
        {
            string themeString = $"{_mode}|{_palette}";
            ThemeManager.SaveTheme(themeString);
            Musicefy.Properties.Settings.Default.Theme = themeString;
            Musicefy.Properties.Settings.Default.Save();
        }

        public void Cancel()
        {
            string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";
            var parts = savedTheme.Split('|');
            _mode = parts.Length > 0 ? parts[0] : "Dark";
            _palette = parts.Length > 1 ? parts[1] : "Default";
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            ThemeManager.ApplyTheme(_mode, _palette);
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

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Musicefy.Services;

namespace Musicefy.Views
{
    public partial class SettingsWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<string> DateFormats { get; } =
            new ObservableCollection<string> { "Default (12/05/2026)", "MM/dd/yyyy", "dd/MM/yyyy" };

        public ObservableCollection<ThemePreview> ThemePreviews { get; } =
            new ObservableCollection<ThemePreview>
            {
                new ThemePreview("Default", System.Windows.Media.Brushes.Orange),
                new ThemePreview("Catppuccin", System.Windows.Media.Brushes.Pink),
                new ThemePreview("Green Apple", System.Windows.Media.Brushes.Green),
                new ThemePreview("Lavender", System.Windows.Media.Brushes.MediumPurple)
            };

        private int _selectedThemeIndex;
        private string _currentMode = "Dark";
        private string _currentPalette = "Default";

        public int SelectedThemeIndex
        {
            get => _selectedThemeIndex;
            set { _selectedThemeIndex = value; OnPropertyChanged(); }
        }

        private bool _pureBlackMode;
        public bool PureBlackMode
        {
            get => _pureBlackMode;
            set { _pureBlackMode = value; OnPropertyChanged(); }
        }

        private bool _relativeTimestamps;
        public bool RelativeTimestamps
        {
            get => _relativeTimestamps;
            set { _relativeTimestamps = value; OnPropertyChanged(); }
        }

        private bool _renderImages;
        public bool RenderImages
        {
            get => _renderImages;
            set { _renderImages = value; OnPropertyChanged(); }
        }

        private bool _showUpdates;
        public bool ShowUpdates
        {
            get => _showUpdates;
            set { _showUpdates = value; OnPropertyChanged(); }
        }

        private string _selectedDateFormat;
        public string SelectedDateFormat
        {
            get => _selectedDateFormat;
            set { _selectedDateFormat = value; OnPropertyChanged(); }
        }

        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Load saved theme
            string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";
            ApplyThemeFromString(savedTheme);
        }

        private void ThemeTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(sender is TabControl tab)) return;

            switch (tab.SelectedIndex)
            {
                case 0: _currentMode = "System"; break;
                case 1: _currentMode = "Light"; break;
                case 2: _currentMode = "Dark"; break;
            }

            ThemeManager.ApplyTheme(_currentMode, _currentPalette);
        }

        private void PalettePreview_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.DataContext is ThemePreview preview)) return;

            _currentPalette = preview.Name;
            ThemeManager.ApplyTheme(_currentMode, _currentPalette);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string themeString = $"{_currentMode}|{_currentPalette}";
            ThemeManager.SaveTheme(themeString);
            Musicefy.Properties.Settings.Default.Theme = themeString;
            Musicefy.Properties.Settings.Default.Save();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";
            ApplyThemeFromString(savedTheme);
            DialogResult = false;
            Close();
        }

        private void ApplyThemeFromString(string themeString)
        {
            var parts = themeString.Split('|');
            _currentMode = parts.Length > 0 ? parts[0] : "Dark";
            _currentPalette = parts.Length > 1 ? parts[1] : "Default";

            ThemeManager.ApplyTheme(_currentMode, _currentPalette);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class ThemePreview
    {
        public string Name { get; }
        public System.Windows.Media.Brush AccentBrush { get; }

        public ThemePreview(string name, System.Windows.Media.Brush accent)
        {
            Name = name;
            AccentBrush = accent;
        }
    }
}

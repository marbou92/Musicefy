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
        public ObservableCollection<string> AvailableThemes { get; }
        private string _selectedTheme;
        private string _originalTheme;

        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (_selectedTheme != value)
                {
                    _selectedTheme = value;
                    OnPropertyChanged();
                }
            }
        }

        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Load available themes (mode + palette combos)
            AvailableThemes = new ObservableCollection<string>(ThemeManager.GetAvailableThemes());

            // Restore saved theme
            _originalTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";
            SelectedTheme = _originalTheme;

            // Apply saved theme immediately
            ApplyThemeFromString(_originalTheme);
        }

        // Preview theme on hover
        private void ThemeCombo_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (ThemeCombo.SelectedItem is string hoveredTheme)
            {
                ApplyThemeFromString(hoveredTheme);
            }
        }

        // Save selected theme permanently
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.SaveTheme(SelectedTheme);
            Musicefy.Properties.Settings.Default.Theme = SelectedTheme;
            Musicefy.Properties.Settings.Default.Save();
            DialogResult = true;
            Close();
        }

        // Cancel and revert to original theme
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            ApplyThemeFromString(_originalTheme);
            DialogResult = false;
            Close();
        }

        // Helper: parse "Mode|Palette" string and apply
        private void ApplyThemeFromString(string themeString)
        {
            var parts = themeString.Split('|');
            string mode = parts.Length > 0 ? parts[0] : "Dark";
            string palette = parts.Length > 1 ? parts[1] : "Default";

            ThemeManager.ApplyTheme(mode, palette);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

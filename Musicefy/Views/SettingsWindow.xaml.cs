using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
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

            AvailableThemes = new ObservableCollection<string>(ThemeManager.GetAvailableThemes());

            _originalTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark";
            SelectedTheme = _originalTheme;

            ThemeManager.ApplyTheme(_originalTheme);
        }

        private void ThemeCombo_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (ThemeCombo.SelectedItem is string hoveredTheme)
            {
                ThemeManager.ApplyTheme(hoveredTheme);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.SaveTheme(SelectedTheme);
            Musicefy.Properties.Settings.Default.Theme = SelectedTheme;
            Musicefy.Properties.Settings.Default.Save();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ApplyTheme(_originalTheme);
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

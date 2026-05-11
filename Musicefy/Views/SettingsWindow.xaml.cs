using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Musicefy.Core; // ThemeManager

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

            // Save the original theme so we can revert if Cancel is clicked
            _originalTheme = Properties.Settings.Default.SelectedTheme ?? "Dark";
            SelectedTheme = _originalTheme;

            // Apply the original theme immediately
            ThemeManager.ApplyTheme(_originalTheme);
        }

        // Hover preview
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
            this.DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Revert to original theme if user cancels
            ThemeManager.ApplyTheme(_originalTheme);
            this.DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

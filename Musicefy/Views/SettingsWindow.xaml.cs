using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Musicefy.Core; // ThemeManager

namespace Musicefy.Views
{
    public partial class SettingsWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<string> AvailableThemes { get; }
        private string _selectedTheme;

        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (_selectedTheme != value)
                {
                    _selectedTheme = value;
                    OnPropertyChanged();
                    ThemeManager.ApplyTheme(_selectedTheme);
                    ThemeManager.SaveTheme(_selectedTheme);
                }
            }
        }

        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Load available themes dynamically
            AvailableThemes = new ObservableCollection<string>(ThemeManager.GetAvailableThemes());

            // Set initial selection
            SelectedTheme = Properties.Settings.Default.SelectedTheme ?? "Dark";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

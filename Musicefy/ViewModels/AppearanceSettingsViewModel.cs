using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Musicefy.ViewModels
{
    public class AppearanceSettingsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private int _selectedThemeIndex;
        private bool _pureBlackMode;
        private bool _relativeTimestamps;
        private bool _renderImages;
        private bool _showUpdates;
        private string _selectedDateFormat;

        public int SelectedThemeIndex
        {
            get => _selectedThemeIndex;
            set { _selectedThemeIndex = value; OnPropertyChanged(); }
        }

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

        public ObservableCollection<string> DateFormats { get; } =
            new ObservableCollection<string> { "Default (12/05/2026)", "MM/dd/yyyy", "dd/MM/yyyy" };

        public string SelectedDateFormat
        {
            get => _selectedDateFormat;
            set { _selectedDateFormat = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ThemePreview> ThemePreviews { get; } =
            new ObservableCollection<ThemePreview>
            {
                new ThemePreview("Default", Brushes.Orange),
                new ThemePreview("Catppuccin", Brushes.Pink),
                new ThemePreview("Green Apple", Brushes.Green),
                new ThemePreview("Lavender", Brushes.MediumPurple),
                new ThemePreview("Midnight Dusk", Brushes.DarkSlateBlue)
            };

        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ThemePreview
    {
        public string Name { get; }
        public Brush AccentBrush { get; }

        public ThemePreview(string name, Brush accent)
        {
            Name = name;
            AccentBrush = accent;
        }
    }
}

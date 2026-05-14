using System.Windows;
using System.Windows.Controls;
using Musicefy.Views;
using Musicefy.Services;
using Musicefy.ViewModels;

namespace Musicefy
{
    public partial class MainWindow : Window
    {
        private readonly PlaybackService _playback;
        private readonly MainViewModel _mainViewModel;

        public MainWindow()
        {
            InitializeComponent();
            _mainViewModel = new MainViewModel();
            this.DataContext = _mainViewModel;
            _playback = new PlaybackService();

            // Load Home by default
            MainContent.Content = new HomeControl(_playback, _mainViewModel);
        }

        private void Sidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SidebarList?.SelectedItem == null) return;

            if (SidebarList.SelectedItem == SettingsItem)
            {
                new SettingsWindow { Owner = this }.ShowDialog();
                SidebarList.SelectedIndex = 0;
                return;
            }

            switch (SidebarList.SelectedIndex)
            {
                case 0: MainContent.Content = new HomeControl(_playback, _mainViewModel); break;
                case 1: MainContent.Content = new SearchControl(_playback); break;
                case 2: MainContent.Content = new LibraryControl(_playback); break;
            }
        }
    }
}

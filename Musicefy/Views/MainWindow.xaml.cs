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
            // 1. Initialize data and services FIRST
            _mainViewModel = new MainViewModel();
            _playback = new PlaybackService();
            
            // 2. Set DataContext before UI loads
            this.DataContext = _mainViewModel;

            // 3. Initialize UI (This will trigger Sidebar_SelectionChanged because of IsSelected="True")
            InitializeComponent();

            // 4. Set initial content safely
            MainContent.Content = new HomeControl(_playback, _mainViewModel);
        }

        private void Sidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // CRITICAL GUARD: Stop execution if the window is still loading
            if (_mainViewModel == null || _playback == null || SidebarList?.SelectedItem == null) 
                return;

            if (SidebarList.SelectedItem == SettingsItem)
            {
                new SettingsWindow { Owner = this }.ShowDialog();
                SidebarList.SelectedIndex = 0; // Reset to Home
                return;
            }

            switch (SidebarList.SelectedIndex)
            {
                case 0: 
                    MainContent.Content = new HomeControl(_playback, _mainViewModel); 
                    break;
                case 1: 
                    MainContent.Content = new SearchControl(_playback); 
                    break;
                case 2: 
                    MainContent.Content = new LibraryControl(_playback); 
                    break;
            }
        }
    }
}

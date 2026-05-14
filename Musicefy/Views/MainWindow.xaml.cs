using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
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
            
            // 1. Initialize View Model and set DataContext for the Window
            _mainViewModel = new MainViewModel();
            this.DataContext = _mainViewModel;

            // 2. Initialize Services
            _playback = new PlaybackService();

            // 3. Load the initial View (Home) on startup
            MainContent.Content = new HomeControl(_playback, _mainViewModel); 
        }

        /// <summary>
        /// Handles navigation when a sidebar icon is clicked.
        /// </summary>
        private void Sidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Security check to ensure we have a selection
            if (SidebarList == null || SidebarList.SelectedItem is not ListBoxItem selectedItem)
                return;

            // Special case for Settings which is an explicit named item in XAML
            if (selectedItem == SettingsItem)
            {
                OpenSettings();
                return;
            }

            // Navigation logic based on ListBox Index
            // Index 0: Home, 1: Search, 2: Library
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

        private void OpenSettings()
        {
            var win = new SettingsWindow { Owner = this };
            win.ShowDialog();

            // After closing settings, we default back to the Home tab 
            // so the "Settings" icon doesn't stay highlighted.
            SidebarList.SelectedIndex = 0;
        }

        /* =========================================================================
           LEGACY PLAYER CODE (FOR FUTURE REFERENCE)
           You can re-enable this if you add a 'Now Playing' bar back to the UI.
        =========================================================================
        
        private void OnTrackChanged(Musicefy.Core.Models.MusicFile track)
        {
            // Logic for updating UI elements when a song changes
        }

        private void OnProgressChanged(TimeSpan current, TimeSpan total)
        {
            // Logic for updating seek bars
        }
        */
    }
}

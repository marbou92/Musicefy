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
        private bool _isInitializing = true;

        public MainWindow()
        {
            // 1. Initialize data and services FIRST
            _mainViewModel = new MainViewModel();
            _playback = new PlaybackService();
            
            this.DataContext = _mainViewModel;
            
            Musicefy.Services.ThemeManager.ApplyTheme("Dark", "Default");
            
            // 2. Load the UI components
            InitializeComponent();

            // 3. Mark initialization as complete
            _isInitializing = false;

            // 4. Manually trigger initial view to avoid early trigger crash
            SidebarList.SelectedIndex = 0;
        }

        private void Sidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // CRITICAL GUARD: Stop if UI is still being built or services are missing
            if (_isInitializing || MainContent == null || _mainViewModel == null || _playback == null) 
                return;

            if (SidebarList.SelectedItem == null) return;

            // Handle Settings
            if (SidebarList.SelectedItem == SettingsItem)
            {
                new SettingsWindow { Owner = this }.ShowDialog();
                SidebarList.SelectedIndex = 0; // Return focus to Home
                return;
            }

            // Create the next view based on index
            UserControl nextView = null;
            switch (SidebarList.SelectedIndex)
            {
                case 0: nextView = new HomeControl(_playback, _mainViewModel); break;
                case 1: nextView = new SearchControl(_playback); break;
                case 2: nextView = new LibraryControl(_playback); break;
            }

            if (nextView != null)
            {
                NavigateWithFade(nextView);
            }
        }

        /// <summary>
        /// Adds a smooth Echo-style fade transition between pages
        /// </summary>
        private void NavigateWithFade(UserControl newContent)
        {
            DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s, e) =>
            {
                MainContent.Content = newContent;
                DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                MainContent.BeginAnimation(OpacityProperty, fadeIn);
            };
            MainContent.BeginAnimation(OpacityProperty, fadeOut);
        }
    }
}

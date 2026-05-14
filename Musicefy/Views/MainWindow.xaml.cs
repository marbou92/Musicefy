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
            
            // 1. Initialize View Model and set DataContext
            _mainViewModel = new MainViewModel();
            this.DataContext = _mainViewModel;

            // 2. Initialize Services
            _playback = new PlaybackService();

            // 3. Load initial View
            MainContent.Content = new HomeControl(_playback, _mainViewModel); 
        }

        private void Sidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Fixed: Changed 'is not' to classic null check for C# 8.0 compatibility
            if (SidebarList == null || SidebarList.SelectedItem == null)
                return;

            ListBoxItem selectedItem = SidebarList.SelectedItem as ListBoxItem;
            if (selectedItem == null)
                return;

            // Special case for Settings
            if (selectedItem == SettingsItem)
            {
                OpenSettings();
                return;
            }

            // Navigation logic based on ListBox Index
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

            // Reset selection so the settings icon doesn't stay highlighted
            if (SidebarList != null)
            {
                SidebarList.SelectedIndex = 0;
            }
        }
    }
}

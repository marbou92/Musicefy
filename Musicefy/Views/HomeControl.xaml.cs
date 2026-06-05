using System;
using System.Windows.Controls;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    /// <summary>
    /// Home dashboard control implementing Echo Music's two-phase loading pattern.
    /// Displays personalized content sections from all connected music sources.
    /// </summary>
    public partial class HomeControl : UserControl
    {
        private HomeViewModel _viewModel;

        public HomeControl()
        {
            InitializeComponent();
            Loaded += HomeControl_Loaded;
        }

        /// <summary>
        /// Constructor with DI-injected ViewModel. Used when resolved from ServiceCollection.
        /// </summary>
        public HomeControl(HomeViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private async void HomeControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            UpdateGreeting();

            if (DataContext is HomeViewModel vm)
            {
                _viewModel = vm;
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                UpdateVisualState();

                // Reload if never loaded or if stale (> 5 min since last refresh).
                // This ensures Home picks up newly added sources when the user
                // navigates back from Settings after adding one.
                bool isStale = vm.LastRefreshed == null ||
                    (DateTime.UtcNow - vm.LastRefreshed.Value).TotalMinutes > 5;

                if (vm.LoadState == Core.Models.HomeLoadState.NotStarted || isStale)
                {
                    await vm.LoadAsync();
                }
            }
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(HomeViewModel.LoadState):
                case nameof(HomeViewModel.IsEmpty):
                    UpdateVisualState();
                    break;
            }
        }

        private void UpdateVisualState()
        {
            if (_viewModel == null) return;

            LoadingSkeleton.Visibility = _viewModel.IsLoading
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

            ErrorPanel.Visibility = _viewModel.HasError
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

            EmptyPanel.Visibility = _viewModel.IsEmpty
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

            MainScrollViewer.Visibility = _viewModel.IsLoaded && !_viewModel.IsEmpty
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

            // ChipBar visibility is now data-bound to AvailableChips.Count
            // via NonZeroToVisibilityConverter in XAML — no manual update needed.

            if (_viewModel.HasError)
            {
                ErrorText.Text = _viewModel.ErrorMessage ?? "An unexpected error occurred.";
            }
        }

        private void UpdateGreeting()
        {
            var hour = DateTime.Now.Hour;
            string greeting;
            if (hour < 6) greeting = "Good Night";
            else if (hour < 12) greeting = "Good Morning";
            else if (hour < 18) greeting = "Good Afternoon";
            else greeting = "Good Evening";

            GreetingText.Text = greeting;
            SubGreetingText.Text = "What would you like to listen to?";
        }

        private async void RetryButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_viewModel != null)
                await _viewModel.LoadAsync();
        }

        private async void RefreshButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_viewModel != null && _viewModel.RefreshCommand.CanExecute(null))
                _viewModel.RefreshCommand.Execute(null);
        }

        /// <summary>
        /// Handles the SeeAllRequested routed event from HomeSectionControl.
        /// Routes to HomeViewModel's SeeAllForSectionCommand.
        /// </summary>
        private void HomeSection_SeeAllRequested(object sender, System.Windows.RoutedEventArgs e)
        {
            if (e.OriginalSource is Controls.HomeSectionControl sectionControl &&
                sectionControl.DataContext is Musicefy.Core.Models.HomeSection section &&
                _viewModel != null)
            {
                _viewModel.SeeAllForSectionCommand.Execute(section);
            }
            e.Handled = true;
        }
    }
}

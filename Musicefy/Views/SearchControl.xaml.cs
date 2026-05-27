using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Musicefy.Core.Models;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class SearchControl : UserControl
    {
        private SearchViewModel ViewModel => DataContext as SearchViewModel;
        private Storyboard _spinnerStoryboard;

        public SearchControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
                ViewModel.PropertyChanged += OnSearchPropertyChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
                ViewModel.PropertyChanged -= OnSearchPropertyChanged;
            StopSpinner();
        }

        private void OnSearchPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SearchViewModel.IsSearching))
            {
                if (ViewModel.IsSearching)
                    StartSpinner();
                else
                    StopSpinner();
            }
        }

        private void StartSpinner()
        {
            SearchSpinner.Visibility = Visibility.Visible;
            if (_spinnerStoryboard != null) return;
            _spinnerStoryboard = new Storyboard();
            var rotate = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = new Duration(TimeSpan.FromSeconds(1.2)),
                RepeatBehavior = RepeatBehavior.Forever
            };
            Storyboard.SetTargetName(rotate, "SpinnerArc");
            Storyboard.SetTargetProperty(rotate, new PropertyPath("(Path.RenderTransform).(RotateTransform.Angle)"));
            _spinnerStoryboard.Children.Add(rotate);
            _spinnerStoryboard.Begin(SearchSpinner);
        }

        private void StopSpinner()
        {
            if (_spinnerStoryboard == null) return;
            _spinnerStoryboard.Stop(SearchSpinner);
            _spinnerStoryboard = null;
            SearchSpinner.Visibility = Visibility.Collapsed;
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchBox.Text == "Search...")
            {
                SearchBox.Text = "";
                SearchBox.Foreground = (System.Windows.Media.Brush)FindResource("TextBrush");
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                SearchBox.Text = "Search...";
                SearchBox.Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush");
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ViewModel != null)
                ViewModel.SearchQuery = SearchBox.Text == "Search..." ? "" : SearchBox.Text;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "Search...";
            SearchBox.Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush");
            if (ViewModel != null)
                ViewModel.SearchQuery = "";
            Keyboard.Focus(SearchBox);
        }

        private void SearchResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SearchResultsListView.SelectedItem is MusicFile track && ViewModel != null)
            {
                ViewModel.SelectedResult = track;
                if (ViewModel.PlayTrackCommand.CanExecute(track))
                    ViewModel.PlayTrackCommand.Execute(track);
            }
        }

        private void SearchBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!SearchBox.IsKeyboardFocusWithin)
                Keyboard.Focus(SearchBox);
        }
    }
}

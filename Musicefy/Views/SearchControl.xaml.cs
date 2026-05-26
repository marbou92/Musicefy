using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Musicefy.Core.Models;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class SearchControl : UserControl
    {
        private SearchViewModel ViewModel => DataContext as SearchViewModel;

        public SearchControl()
        {
            InitializeComponent();
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
                ViewModel.SelectedResult = track;
        }

        private void SearchBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!SearchBox.IsKeyboardFocusWithin)
                Keyboard.Focus(SearchBox);
        }
    }
}

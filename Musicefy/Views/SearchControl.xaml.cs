using System.Windows.Controls;

namespace Musicefy.Views
{
    public partial class SearchControl : UserControl
    {
        public SearchControl()
        {
            InitializeComponent();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Hook into your source manager to filter tracks
        }
    }
}

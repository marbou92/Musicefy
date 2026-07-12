using System.Windows.Controls;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class HistoryControl : UserControl
    {
        public HistoryControl()
        {
            InitializeComponent();
            if (DataContext == null)
            {
                try { DataContext = App.Services?.GetService<HistoryViewModel>(); }
                catch { }
            }
        }
    }
}

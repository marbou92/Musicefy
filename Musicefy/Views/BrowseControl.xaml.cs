using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class BrowseControl : UserControl
    {
        public BrowseControl()
        {
            InitializeComponent();
            if (DataContext == null)
            {
                try { DataContext = App.Services?.GetService<BrowseViewModel>(); }
                catch { }
            }
        }
    }
}

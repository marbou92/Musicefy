using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class StatsControl : UserControl
    {
        public StatsControl()
        {
            InitializeComponent();
            if (DataContext == null)
            {
                try { DataContext = App.Services?.GetService<StatsViewModel>(); }
                catch { }
            }
        }
    }
}

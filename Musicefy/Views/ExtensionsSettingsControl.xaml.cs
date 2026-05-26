using System.Windows.Controls;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class ExtensionsSettingsControl : UserControl
    {
        public ExtensionsSettingsControl()
        {
            InitializeComponent();
            DataContext = new ExtensionsSettingsViewModel();
        }
    }
}

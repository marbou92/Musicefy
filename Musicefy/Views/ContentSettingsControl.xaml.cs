using System.Windows.Controls;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class ContentSettingsControl : UserControl
    {
        public ContentSettingsControl()
        {
            InitializeComponent();
            if (DataContext == null)
                DataContext = new ContentSettingsViewModel();
        }
    }
}

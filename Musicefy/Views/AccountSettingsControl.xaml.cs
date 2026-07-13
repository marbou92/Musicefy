using System.Windows.Controls;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class AccountSettingsControl : UserControl
    {
        public AccountSettingsControl()
        {
            InitializeComponent();
            if (DataContext == null)
                DataContext = new AccountSettingsViewModel();
        }
    }
}

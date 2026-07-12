using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class IntegrationsSettingsControl : UserControl
    {
        public IntegrationsSettingsControl()
        {
            InitializeComponent();
            if (DataContext == null)
            {
                try { DataContext = App.Services?.GetService<IntegrationsViewModel>(); }
                catch { }
            }
        }

        private void LastFmPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is IntegrationsViewModel vm && sender is PasswordBox pb)
            {
                vm.LastFmPassword = pb.Password;
            }
        }
    }
}

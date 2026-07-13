using System.Windows.Controls;
using System.Windows.Input;
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

        private void DiscordRow_Click(object sender, MouseButtonEventArgs e)
        {
            (DataContext as AccountSettingsViewModel)?.NavigateToDiscordCommand.Execute(null);
        }

        private void LastFmRow_Click(object sender, MouseButtonEventArgs e)
        {
            (DataContext as AccountSettingsViewModel)?.NavigateToLastFmCommand.Execute(null);
        }

        private void LastFmPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is AccountSettingsViewModel vm && sender is PasswordBox pb)
                vm.LastFmPassword = pb.Password;
        }
    }
}

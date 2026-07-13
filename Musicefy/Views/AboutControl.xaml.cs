using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;

namespace Musicefy.Views
{
    public partial class AboutControl : UserControl
    {
        public AboutControl()
        {
            InitializeComponent();
        }

        private void GitHubLink_Click(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = "https://github.com/marbou92/Musicefy", UseShellExecute = true });
        }

        private void WebsiteLink_Click(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = "https://github.com/marbou92", UseShellExecute = true });
        }

        private void DonateLink_Click(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = "https://buymeacoffee.com/marbou92", UseShellExecute = true });
        }

        private void LicenseLink_Click(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = "https://github.com/marbou92/Musicefy/blob/main/LICENSE", UseShellExecute = true });
        }
    }
}

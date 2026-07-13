using System.Windows.Controls;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class PlayerAudioSettingsControl : UserControl
    {
        public PlayerAudioSettingsControl()
        {
            InitializeComponent();
            if (DataContext == null)
                DataContext = new PlayerAudioSettingsViewModel();
        }
    }
}

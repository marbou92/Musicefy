using System.Windows.Controls;
using Musicefy.Core.Interfaces;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class DiscoverSettingsControl : UserControl, ISettingsControl
    {
        private readonly DiscoverSettingsViewModel _viewModel;

        public DiscoverSettingsControl()
        {
            InitializeComponent();
            _viewModel = new DiscoverSettingsViewModel();
            DataContext = _viewModel;
        }

        public void Save()
        {
            _viewModel.Save();
        }

        public void Cancel()
        {
            _viewModel.Cancel();
        }
    }
}

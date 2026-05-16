using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Musicefy.Core.Interfaces;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class AppearanceSettingsControl : UserControl, ISettingsControl
    {
        private readonly AppearanceSettingsViewModel _viewModel;

        public AppearanceSettingsControl()
        {
            InitializeComponent();
            _viewModel = new AppearanceSettingsViewModel();
            this.DataContext = _viewModel;
        }

        // ISettingsControl Implementation
        public void Save()
        {
            _viewModel.Save();
        }

        public void Cancel()
        {
            _viewModel.Cancel();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Save();
            MessageBox.Show("Appearance settings saved.", "Musicefy", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Cancel();
        }

        private void PaletteCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ThemePreview preview)
            {
                _viewModel.SelectPalette(preview.CardName);
            }
        }
    }
}

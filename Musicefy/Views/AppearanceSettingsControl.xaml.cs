using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class AppearanceSettingsControl : UserControl
    {
        private readonly AppearanceSettingsViewModel _viewModel;

        public AppearanceSettingsControl()
        {
            InitializeComponent();
            _viewModel = new AppearanceSettingsViewModel();
            this.DataContext = _viewModel;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Save();
            MessageBox.Show("Appearance settings saved.", "Musicefy", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Cancel();
        }

        private void PaletteCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is ThemePreview preview)
            {
                _viewModel.SelectPalette(preview.CardName);
            }
        }
    }
}

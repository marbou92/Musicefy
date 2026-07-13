using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Theme;
using Musicefy.Services;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class AppearanceSettingsControl : UserControl, ISettingsControl
    {
        public AppearanceSettingsControl()
        {
            InitializeComponent();
        }

        public void Save()
        {
            if (DataContext is AppearanceSettingsViewModel vm)
                vm.Save();
        }

        public void Cancel()
        {
            if (DataContext is AppearanceSettingsViewModel vm)
                vm.Cancel();
        }

        private void PaletteCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is AppThemePreview clickedPalette)
            {
                if (this.DataContext is AppearanceSettingsViewModel viewModel)
                {
                    viewModel.SelectAppTheme(clickedPalette.Theme);
                }
            }
        }

        /// <summary>
        /// Sprint 9.1: Navigate to the Theme sub-view when the Theme row is clicked.
        /// </summary>
        private void ThemeRow_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is AppearanceSettingsViewModel vm)
            {
                vm.NavigateToThemeCommand.Execute(null);
            }
        }

        private void BtnResetPalette_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AppearanceSettingsViewModel viewModel)
            {
                viewModel.SelectAppTheme(AppTheme.Default);
            }
        }
    }
}

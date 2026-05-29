using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Musicefy.Core.Interfaces;
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
            if (sender is FrameworkElement element && element.DataContext is ThemePreview clickedPalette)
            {
                if (this.DataContext is AppearanceSettingsViewModel viewModel)
                {
                    viewModel.SelectPalette(clickedPalette.CardName);
                }
            }
        }

        private void BtnDefaultPalette_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AppearanceSettingsViewModel viewModel)
            {
                viewModel.SelectPalette("Default");
            }
        }
    }
}

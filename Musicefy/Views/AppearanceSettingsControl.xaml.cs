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

        /// <summary>
        /// Captures click inputs on the phone-mockup palette cards to swap app color accents instantly.
        /// </summary>
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
            // Walk the visual tree step to extract the bound data context model item safely
            if (sender is FrameworkElement element && element.DataContext is ThemePreview clickedPalette)
            {
                if (this.DataContext is AppearanceSettingsViewModel viewModel)
                {
                    // Pass selected card name into your MVVM selection engine orchestrator pass
                    viewModel.SelectPalette(clickedPalette.CardName);
                }
            }
        }
    }
}

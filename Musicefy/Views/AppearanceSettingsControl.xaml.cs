using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Musicefy.Core.Interfaces;
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

        private void ColorPaletteButton_Click(object sender, MouseButtonEventArgs e)
        {
            ThemeManager.PauseDynamicColors();
            if (DataContext is AppearanceSettingsViewModel vm)
            {
                vm.IsPaletteSubspaceOpen = true;
            }
        }

        private void PaletteBackButton_Click(object sender, MouseButtonEventArgs e)
        {
            ThemeManager.ResumeDynamicColors();
            if (DataContext is AppearanceSettingsViewModel vm)
            {
                vm.IsPaletteSubspaceOpen = false;
                vm.IsCustomThemeEditorOpen = false;
            }
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

        private void BtnResetPalette_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AppearanceSettingsViewModel viewModel)
            {
                var firstGroup = viewModel.FamilyGroups?.FirstOrDefault();
                var firstPreview = firstGroup?.Previews?.FirstOrDefault();
                if (firstPreview != null)
                    viewModel.SelectPalette(firstPreview.CardName);
            }
        }

        private void CustomThemeButton_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is AppearanceSettingsViewModel vm)
            {
                vm.IsCustomThemeEditorOpen = true;
                vm.SelectedSeedRole = 0;
            }
        }

        private void ImportThemeButton_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is AppearanceSettingsViewModel vm)
            {
                vm.ImportThemeCommand.Execute(null);
            }
        }

        /// <summary>
        /// Converts vertical mouse-wheel delta to horizontal scroll on horizontal-only ScrollViewers.
        /// When the horizontal ScrollViewer reaches its scroll limit, the event is allowed to
        /// bubble up to the parent vertical ScrollViewer so the page still scrolls.
        /// </summary>
        private void HorizontalScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var sv = (ScrollViewer)sender;

            if (sv.ScrollableWidth > 0)
            {
                double newOffset = sv.HorizontalOffset - e.Delta;
                newOffset = System.Math.Max(0, System.Math.Min(newOffset, sv.ScrollableWidth));

                if (System.Math.Abs(newOffset - sv.HorizontalOffset) > 0.5)
                {
                    sv.ScrollToHorizontalOffset(newOffset);
                    e.Handled = true;
                }
            }
        }
    }
}

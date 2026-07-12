using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    /// <summary>
    /// Interaction logic for SourceCard.xaml
    /// A UserControl displaying a single source card with health status,
    /// connection info, action buttons, and home-screen visibility toggle.
    /// </summary>
    public partial class SourceCard : UserControl
    {
        public SourceCard()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateConfigItems();
        }

        private void UpdateConfigItems()
        {
            if (ConfigItems == null) return;

            var vm = DataContext as SourceViewModel;
            if (vm?.Source?.Configuration == null)
            {
                ConfigItems.ItemsSource = null;
                return;
            }

            var items = new List<KeyValuePair<string, string>>();
            foreach (var kvp in vm.Source.Configuration)
            {
                var value = kvp.Key.ToLowerInvariant().Contains("password")
                    ? "••••••••"
                    : kvp.Value ?? "";
                items.Add(new KeyValuePair<string, string>(kvp.Key, value));
            }

            ConfigItems.ItemsSource = items;
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as SourceViewModel;
            if (vm == null) return;

            // Don't attempt a test if the provider is missing (orphaned source).
            if (vm.IsProviderMissing)
            {
                System.Windows.MessageBox.Show(
                    "The provider for this source is no longer available. Please remove this source.",
                    "Provider Missing",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var parent = FindParent<SourcesSettingsControl>(this);
            parent?.TestSourceConnection(vm);
        }

        private void ExpandButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as SourceViewModel;
            if (vm == null) return;

            vm.IsExpanded = !vm.IsExpanded;
            ExpandButton.Content = vm.IsExpanded ? "▲" : "▼";

            if (vm.IsExpanded)
            {
                UpdateConfigItems();
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as SourceViewModel;
            if (vm == null) return;

            var parent = FindParent<SourcesSettingsControl>(this);
            parent?.RemoveSource(vm);
        }

        /// <summary>
        /// Handle the "Show on Home" checkbox toggle.
        /// Delegates to SourcesSettingsViewModel which persists the setting
        /// and refreshes all sources of the same type.
        /// </summary>
        private void HomeVisibility_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as SourceViewModel;
            if (vm == null) return;

            var parent = FindParent<SourcesSettingsControl>(this);
            parent?.ToggleHomeVisibility(vm);
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T result)
                    return result;
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            return null;
        }
    }
}

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    /// <summary>
    /// Interaction logic for SourceCard.xaml
    /// A UserControl displaying a single source card with health status,
    /// connection info, and action buttons.
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
                // Mask password values
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

            // Find the parent SourcesSettingsControl to relay the command
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

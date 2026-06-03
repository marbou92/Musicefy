using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    /// <summary>
    /// Dialog for adding/editing streaming sources.
    /// Step 1: Select source type from available providers.
    /// Step 2: Dynamic configuration form rendered from SourceConfigField definitions.
    /// Includes Test Connection button with inline result display.
    /// </summary>
    public partial class AddSourceDialog : Window
    {
        private readonly SourcesSettingsViewModel _viewModel;
        private readonly List<IMusicSourceProvider> _providers;
        private IMusicSourceProvider _selectedProvider;
        private int _currentStep = 1;
        private Dictionary<string, string> _currentConfig = new Dictionary<string, string>();
        private bool _isEditMode;

        public bool SourceAdded { get; private set; }

        public AddSourceDialog(SourcesSettingsViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _providers = viewModel.AvailableProviders.ToList();

            ProviderList.ItemsSource = _providers;
            ShowStep(1);
        }

        public AddSourceDialog(SourcesSettingsViewModel viewModel, SourceViewModel sourceToEdit)
            : this(viewModel)
        {
            if (sourceToEdit == null) return;

            _isEditMode = true;
            Title = "Edit Source";

            // Pre-select the provider
            _selectedProvider = _providers.FirstOrDefault(p => p.SourceType == sourceToEdit.Type);
            if (_selectedProvider != null)
            {
                SourceNameBox.Text = sourceToEdit.Name;
                ShowStep(2);
                PopulateConfigFields(sourceToEdit.Source.Configuration);
            }
        }

        private void ShowStep(int step)
        {
            _currentStep = step;
            StepNumber.Text = step.ToString();

            Step1Panel.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
            BackButton.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
            TestConnectionButton.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
            SaveButton.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;

            StepDescription.Text = step == 1 ? "Select Source Type" : "Configure Source";
        }

        private void ProviderItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.Tag is IMusicSourceProvider provider)
            {
                _selectedProvider = provider;

                // Set default name from provider display name
                if (string.IsNullOrEmpty(SourceNameBox.Text))
                    SourceNameBox.Text = provider.DisplayName;

                PopulateConfigFields(null);
                ShowStep(2);
            }
        }

        private void PopulateConfigFields(Dictionary<string, string> existingConfig)
        {
            if (_selectedProvider == null) return;

            var fields = new List<ConfigFieldViewModel>();
            foreach (var field in _selectedProvider.ConfigurationFields)
            {
                var value = existingConfig != null && existingConfig.TryGetValue(field.Key, out var existingValue)
                    ? existingValue
                    : field.DefaultValue ?? "";

                fields.Add(new ConfigFieldViewModel
                {
                    Key = field.Key,
                    Label = field.Label,
                    Description = field.Description,
                    IsRequired = field.IsRequired,
                    IsPassword = field.IsPassword,
                    IsNotPassword = !field.IsPassword,
                    Value = value
                });
            }

            ConfigFieldsPanel.ItemsSource = fields;
        }

        private Dictionary<string, string> CollectConfigValues()
        {
            var config = new Dictionary<string, string>();

            foreach (var item in ConfigFieldsPanel.Items)
            {
                var fieldVm = item as ConfigFieldViewModel;
                if (fieldVm == null) continue;

                // Find the corresponding container
                var container = ConfigFieldsPanel.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                if (container == null) continue;

                // Try to find the PasswordBox or TextBox
                var passwordBox = FindVisualChild<PasswordBox>(container);
                var textBox = FindVisualChild<TextBox>(container);

                string value = null;
                if (fieldVm.IsPassword && passwordBox != null)
                {
                    value = passwordBox.Password;
                }
                else if (!fieldVm.IsPassword && textBox != null)
                {
                    value = textBox.Text;
                }
                else
                {
                    value = fieldVm.Value;
                }

                config[fieldVm.Key] = value ?? "";
            }

            return config;
        }

        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProvider == null) return;

            var config = CollectConfigValues();
            _currentConfig = config;

            try
            {
                LoadingPanel.Visibility = Visibility.Visible;
                TestConnectionButton.IsEnabled = false;

                var success = await _selectedProvider.TestConnectionAsync(config);

                TestResultPanel.Visibility = Visibility.Visible;
                TestResultText.Text = success ? "Connection successful!" : "Connection failed. Please check your settings.";
                TestResultBorder.Background = success
                    ? new SolidColorBrush(Color.FromArgb(40, 76, 175, 80))
                    : new SolidColorBrush(Color.FromArgb(40, 244, 67, 54));
                TestResultText.Foreground = success
                    ? new SolidColorBrush(Color.FromRgb(76, 175, 80))
                    : new SolidColorBrush(Color.FromRgb(244, 67, 54));
            }
            catch (Exception ex)
            {
                TestResultPanel.Visibility = Visibility.Visible;
                TestResultText.Text = $"Connection error: {ex.Message}";
                TestResultBorder.Background = new SolidColorBrush(Color.FromArgb(40, 244, 67, 54));
                TestResultText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            }
            finally
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                TestConnectionButton.IsEnabled = true;
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProvider == null) return;

            var name = SourceNameBox.Text?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Please enter a source name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var config = CollectConfigValues();

            // Validate required fields
            foreach (var field in _selectedProvider.ConfigurationFields)
            {
                string fieldValue;
                config.TryGetValue(field.Key, out fieldValue);
                if (field.IsRequired && string.IsNullOrEmpty(fieldValue))
                {
                    MessageBox.Show($"Please fill in the required field: {field.Label}", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            try
            {
                SaveButton.IsEnabled = false;

                if (_isEditMode)
                {
                    // For edit mode, we remove the old source and add a new one
                    // This is a simple approach - a more sophisticated approach would
                    // update in place
                    var existingSource = _viewModel.Sources.FirstOrDefault(s => s.Provider == _selectedProvider);
                    if (existingSource != null)
                    {
                        _viewModel.RemoveSourceCommand.Execute(existingSource);
                    }
                }

                var success = await _viewModel.AddSourceAsync(name, _selectedProvider, config);

                if (success)
                {
                    SourceAdded = true;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Failed to add source. Please check your configuration.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving source: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SaveButton.IsEnabled = true;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            ShowStep(1);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;

                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                    return descendant;
            }

            return null;
        }

        /// <summary>
        /// ViewModel for a single configuration field in the dialog form.
        /// </summary>
        private class ConfigFieldViewModel : INotifyPropertyChanged
        {
            public string Key { get; set; }
            public string Label { get; set; }
            public string Description { get; set; }
            public bool IsRequired { get; set; }
            public bool IsPassword { get; set; }
            public bool IsNotPassword { get; set; }
            public string Value { get; set; }

            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}

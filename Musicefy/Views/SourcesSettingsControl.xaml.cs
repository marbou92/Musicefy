using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class SourcesSettingsControl : UserControl
    {
        private readonly SourcesSettingsViewModel _viewModel;
        private readonly Dictionary<string, string> _fieldValues = new Dictionary<string, string>();

        public SourcesSettingsControl()
        {
            InitializeComponent();
            _viewModel = App.Services.GetService<SourcesSettingsViewModel>();
            DataContext = _viewModel;

            SourceTypeCombo.ItemsSource = _viewModel.Providers;
            SourcesList.ItemsSource = _viewModel.Sources;

            _viewModel.ProviderChanged += OnProviderChanged;
            _viewModel.TestConnectionRequested += OnTestConnectionRequested;
            _viewModel.AddSourceRequested += OnAddSourceRequested;

            TestButton.Command = _viewModel.TestConnectionCommand;
            AddButton.Command = _viewModel.AddSourceCommand;

            if (_viewModel.Providers.Count > 0)
                SourceTypeCombo.SelectedIndex = 0;
        }

        private void OnProviderChanged(IMusicSourceProvider provider)
        {
            _fieldValues.Clear();
            DynamicFieldsStack.Children.Clear();

            if (provider == null) return;

            foreach (var field in provider.ConfigurationFields)
            {
                var label = new TextBlock
                {
                    Text = field.Label,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 6),
                    FontSize = 13,
                    Foreground = TryFindResource("ForegroundBrush") as Brush
                };

                var stackPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
                stackPanel.Children.Add(label);

                var fieldBorder = new Border
                {
                    Background = TryFindResource("SecondaryBackgroundBrush") as Brush,
                    BorderBrush = TryFindResource("BorderBrush") as Brush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Height = 38
                };

                if (field.IsPassword)
                {
                    var pwBox = new PasswordBox
                    {
                        Tag = field.Key,
                        ToolTip = field.Placeholder ?? field.Description,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    pwBox.PasswordChanged += (s, args) =>
                    {
                        _fieldValues[field.Key] = pwBox.Password;
                    };
                    fieldBorder.Child = pwBox;
                    _fieldValues[field.Key] = field.DefaultValue ?? "";
                }
                else
                {
                    var textBox = new TextBox
                    {
                        Text = field.DefaultValue ?? "",
                        Tag = field.Key,
                        ToolTip = field.Placeholder ?? field.Description,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    textBox.TextChanged += (s, args) =>
                    {
                        _fieldValues[field.Key] = textBox.Text;
                    };
                    fieldBorder.Child = textBox;
                    _fieldValues[field.Key] = field.DefaultValue ?? "";
                }

                stackPanel.Children.Add(fieldBorder);
                DynamicFieldsStack.Children.Add(stackPanel);
            }
        }

        private async Task<bool> OnTestConnectionRequested(IMusicSourceProvider provider)
        {
            var requiredFields = provider.ConfigurationFields
                .Where(f => f.IsRequired)
                .Select(f => f.Key)
                .ToList();

            var missingRequired = requiredFields
                .Where(key => !_fieldValues.ContainsKey(key) || string.IsNullOrEmpty(_fieldValues[key]))
                .ToList();

            if (missingRequired.Count > 0)
            {
                SetTestStatus($"Fill required fields: {string.Join(", ", missingRequired)}", false);
                return false;
            }

            try
            {
                return await provider.TestConnectionAsync(_fieldValues);
            }
            catch (Exception ex)
            {
                SetTestStatus($"Failed: {ex.Message}", false);
                return false;
            }
        }

        private async Task<bool> OnAddSourceRequested(string name, string sourceType, IMusicSourceProvider provider)
        {
            var requiredFields = provider.ConfigurationFields
                .Where(f => f.IsRequired)
                .Select(f => f.Key)
                .ToList();

            var missingRequired = requiredFields
                .Where(key => !_fieldValues.ContainsKey(key) || string.IsNullOrEmpty(_fieldValues[key]))
                .ToList();

            if (missingRequired.Count > 0)
            {
                MessageBox.Show($"Fill required fields: {string.Join(", ", missingRequired)}", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please enter a connection name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var source = new StreamingSource
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Type = sourceType,
                Configuration = new Dictionary<string, string>(_fieldValues)
            };

            try
            {
                bool added = await ((IStreamingSourceManager)App.Services.GetService<IStreamingSourceManager>()).AddSourceAsync(source);
                if (added)
                {
                    MessageBox.Show($"Source '{name}' added successfully.", "Musicefy", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to add source. Check your settings.", "Musicefy", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return added;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding source: {ex.Message}", "Musicefy", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private void SourceTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SourceTypeCombo.SelectedItem is IMusicSourceProvider provider)
                _viewModel.SelectedProvider = provider;
        }

        private void RemoveSource_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string sourceId)
            {
                _viewModel.RemoveSourceCommand.Execute(sourceId);
            }
        }

        private void SetTestStatus(string text, bool isSuccess)
        {
            _viewModel.TestStatusText = text;
            _viewModel.IsTestSuccess = isSuccess;
            _viewModel.IsTestVisible = true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;

namespace Musicefy.Views
{
    public partial class SourcesSettingsControl : UserControl
    {
        private readonly IStreamingSourceManager _sourceManager;
        private readonly IExtensionManager _extensionManager;
        private readonly Dictionary<string, IMusicSourceProvider> _providers = new Dictionary<string, IMusicSourceProvider>();
        private readonly Dictionary<string, string> _fieldValues = new Dictionary<string, string>();
        private IMusicSourceProvider _selectedProvider;

        public SourcesSettingsControl()
        {
            InitializeComponent();

            _sourceManager = App.Services.GetService<IStreamingSourceManager>();
            _extensionManager = App.Services.GetService<IExtensionManager>();

            LoadProviders();
            LoadSources();
        }

        private void SetTestStatus(string text, bool isSuccess)
        {
            TestStatus.Visibility = Visibility.Visible;
            TestStatus.Text = text;
            TestStatus.Foreground = isSuccess
                ? new SolidColorBrush(Color.FromRgb(39, 174, 96))
                : new SolidColorBrush(Color.FromRgb(231, 76, 60));
            TestButton.IsEnabled = true;
        }

        private void ClearTestStatus()
        {
            TestStatus.Visibility = Visibility.Collapsed;
            TestStatus.Text = "";
            TestButton.IsEnabled = true;
        }

        private void LoadProviders()
        {
            SourceTypeCombo.Items.Clear();
            _providers.Clear();

            var builtInProviders = App.Services.GetServices<IMusicSourceProvider>();
            var installedExtensions = _extensionManager.GetInstalledExtensions();
            var installedSourceTypes = new HashSet<string>(installedExtensions.Select(e => e.SourceType));

            foreach (var p in builtInProviders)
            {
                if (p.SourceType == "Local" || installedSourceTypes.Contains(p.SourceType))
                    _providers[p.SourceType] = p;
            }

            foreach (var p in _extensionManager.ExtensionProviders)
            {
                if (!_providers.ContainsKey(p.SourceType))
                    _providers[p.SourceType] = p;
            }

            foreach (var provider in _providers.Values)
            {
                SourceTypeCombo.Items.Add(provider);
            }

            if (SourceTypeCombo.Items.Count > 0)
                SourceTypeCombo.SelectedIndex = 0;
        }

        private void LoadSources()
        {
            var displayItems = _sourceManager.Sources.Select(s => new SourceDisplayItem
            {
                Id = s.Id,
                Name = s.Name,
                Type = s.Type,
                DisplayType = s.Type,
                IsConnected = s.IsConnected
            }).ToList();

            SourcesList.ItemsSource = displayItems;
        }

        private void SourceTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SourceTypeCombo.SelectedItem is IMusicSourceProvider provider)
            {
                _selectedProvider = provider;
                NameTextBox.Text = $"{provider.DisplayName} Source";
                _fieldValues.Clear();
                DynamicFieldsStack.Children.Clear();

                foreach (var field in provider.ConfigurationFields)
                {
                    var label = new TextBlock
                    {
                        Text = field.Label,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 0, 6),
                        FontSize = 13,
                        Foreground = TryFindResource("ForegroundBrush") as System.Windows.Media.Brush
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
                        stackPanel.Children.Add(fieldBorder);
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
                        stackPanel.Children.Add(fieldBorder);
                        _fieldValues[field.Key] = field.DefaultValue ?? "";
                    }

                    DynamicFieldsStack.Children.Add(stackPanel);
                }
            }
        }

        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProvider == null)
            {
                SetTestStatus("Select a source type first", false);
                return;
            }

            var requiredFields = _selectedProvider.ConfigurationFields
                .Where(f => f.IsRequired)
                .Select(f => f.Key)
                .ToList();

            var missingRequired = requiredFields
                .Where(key => !_fieldValues.ContainsKey(key) || string.IsNullOrEmpty(_fieldValues[key]))
                .ToList();

            if (missingRequired.Count > 0)
            {
                SetTestStatus($"Fill required fields: {string.Join(", ", missingRequired)}", false);
                return;
            }

            TestButton.IsEnabled = false;
            TestStatus.Visibility = Visibility.Visible;
            TestStatus.Text = "Testing...";
            TestStatus.Foreground = TryFindResource("MutedTextBrush") as Brush ?? new SolidColorBrush(Colors.Gray);

            try
            {
                bool connected = await _selectedProvider.TestConnectionAsync(_fieldValues);
                SetTestStatus(connected ? "Connected!" : "Failed", connected);
            }
            catch (Exception ex)
            {
                SetTestStatus($"Failed: {ex.Message}", false);
            }
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProvider == null)
            {
                MessageBox.Show("Please select a source type.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var requiredFields = _selectedProvider.ConfigurationFields
                .Where(f => f.IsRequired)
                .Select(f => f.Key)
                .ToList();

            var missingRequired = requiredFields
                .Where(key => !_fieldValues.ContainsKey(key) || string.IsNullOrEmpty(_fieldValues[key]))
                .ToList();

            if (missingRequired.Count > 0)
            {
                MessageBox.Show($"Fill required fields: {string.Join(", ", missingRequired)}", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string name = NameTextBox.Text;

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please enter a connection name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var source = new StreamingSource
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Type = _selectedProvider.SourceType,
                Configuration = new Dictionary<string, string>(_fieldValues)
            };

            try
            {
                bool added = await _sourceManager.AddSourceAsync(source);
                if (added)
                {
                    MessageBox.Show($"Source '{name}' added successfully.", "Musicefy", MessageBoxButton.OK, MessageBoxImage.Information);
                    NameTextBox.Clear();
                    _fieldValues.Clear();
                    DynamicFieldsStack.Children.Clear();
                    if (SourceTypeCombo.SelectedIndex >= 0)
                        SourceTypeCombo_SelectionChanged(null, null);
                    LoadSources();
                }
                else
                {
                    MessageBox.Show("Failed to add source. Check your settings.", "Musicefy", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding source: {ex.Message}", "Musicefy", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RemoveSource_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string sourceId)
            {
                _sourceManager.RemoveSource(sourceId);
                LoadSources();
            }
        }

        private class SourceDisplayItem
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
            public string DisplayType { get; set; }
            public bool IsConnected { get; set; }
        }
    }
}

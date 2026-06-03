using System.Windows;
using System.Windows.Controls;
using Musicefy.Core.Interfaces;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    /// <summary>
    /// Main sources settings page control.
    /// Displays a scrollable list of SourceCard controls and provides
    /// an "Add Source" button for adding new sources.
    /// </summary>
    public partial class SourcesSettingsControl : UserControl, ISettingsControl
    {
        private SourcesSettingsViewModel _viewModel;

        public SourcesSettingsControl()
        {
            InitializeComponent();
        }

        public SourcesSettingsControl(SourcesSettingsViewModel viewModel) : this()
        {
            Initialize(viewModel);
        }

        public void Initialize(SourcesSettingsViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = _viewModel;
            SourcesList.ItemsSource = _viewModel.Sources;

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateEmptyState();
        }

        private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SourcesSettingsViewModel.HasSources) ||
                e.PropertyName == nameof(SourcesSettingsViewModel.Sources))
            {
                UpdateEmptyState();
            }
        }

        private void UpdateEmptyState()
        {
            if (_viewModel == null) return;

            var hasSources = _viewModel.Sources.Count > 0;
            EmptyState.Visibility = hasSources ? Visibility.Collapsed : Visibility.Visible;
            SourcesList.Visibility = hasSources ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AddSourceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            var dialog = new AddSourceDialog(_viewModel)
            {
                Owner = Window.GetWindow(this)
            };

            dialog.ShowDialog();

            if (dialog.SourceAdded)
            {
                _viewModel.RefreshSources();
                UpdateEmptyState();
            }
        }

        public async void TestSourceConnection(SourceViewModel source)
        {
            if (_viewModel == null || source == null) return;

            try
            {
                source.IsConnecting = true;

                var sourceManager = App.Services?.GetService(typeof(IStreamingSourceManager)) as IStreamingSourceManager;
                if (sourceManager != null)
                {
                    var success = await sourceManager.TestConnectionAsync(source.Id);

                    source.IsConnected = success;
                    source.HealthStatus = success ? Core.Models.SourceHealthStatus.Healthy : Core.Models.SourceHealthStatus.Unhealthy;
                    source.ErrorMessage = success ? null : "Connection test failed";
                }
            }
            catch (System.Exception ex)
            {
                source.ErrorMessage = ex.Message;
                source.HealthStatus = Core.Models.SourceHealthStatus.Unhealthy;
            }
            finally
            {
                source.IsConnecting = false;
            }
        }

        public void RemoveSource(SourceViewModel source)
        {
            if (_viewModel == null || source == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to remove '{source.Name}'?",
                "Remove Source",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.RemoveSourceCommand.Execute(source);
                UpdateEmptyState();
            }
        }

        public void Save()
        {
            // Sources are saved immediately on add/remove.
        }

        public void Cancel()
        {
            // No pending changes to cancel.
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.Core.Services;
using Musicefy.Properties;
using Newtonsoft.Json;
using static Musicefy.Core.SourceTypes;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// Main ViewModel for source management.
    /// Provides CRUD operations for streaming sources, connection testing,
    /// real-time health status, and home-screen visibility toggles
    /// (absorbed from the former Discover settings tab).
    /// </summary>
    public class SourcesSettingsViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IStreamingSourceManager _sourceManager;
        private readonly IHealthCheckService _healthCheckService;

        private bool _isAddingSource;
        private bool _isTestingConnection;
        private IMusicSourceProvider _selectedProvider;
        private string _testResult;
        private bool _testResultSuccess;
        private SourceViewModel _selectedSource;

        public ObservableCollection<SourceViewModel> Sources { get; } = new ObservableCollection<SourceViewModel>();
        public ObservableCollection<IMusicSourceProvider> AvailableProviders { get; } = new ObservableCollection<IMusicSourceProvider>();

        public bool IsAddingSource
        {
            get => _isAddingSource;
            set => SetProperty(ref _isAddingSource, value);
        }

        public bool IsTestingConnection
        {
            get => _isTestingConnection;
            set => SetProperty(ref _isTestingConnection, value);
        }

        public IMusicSourceProvider SelectedProvider
        {
            get => _selectedProvider;
            set => SetProperty(ref _selectedProvider, value);
        }

        public string TestResult
        {
            get => _testResult;
            set => SetProperty(ref _testResult, value);
        }

        public bool TestResultSuccess
        {
            get => _testResultSuccess;
            set => SetProperty(ref _testResultSuccess, value);
        }

        public SourceViewModel SelectedSource
        {
            get => _selectedSource;
            set => SetProperty(ref _selectedSource, value);
        }

        public bool HasSources => Sources.Count > 0;

        public ICommand AddSourceCommand { get; }
        public ICommand RemoveSourceCommand { get; }
        public ICommand TestConnectionCommand { get; }
        public ICommand EditSourceCommand { get; }

        public SourcesSettingsViewModel(
            IStreamingSourceManager sourceManager,
            IHealthCheckService healthCheckService)
        {
            _sourceManager = sourceManager ?? throw new ArgumentNullException(nameof(sourceManager));
            _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));

            AddSourceCommand = new DelegateCommand(ExecuteAddSource, CanAddSource);
            RemoveSourceCommand = new DelegateCommand<SourceViewModel>(ExecuteRemoveSource, CanRemoveSource);
            TestConnectionCommand = new DelegateCommand<SourceViewModel>(ExecuteTestConnection, CanTestConnection);
            EditSourceCommand = new DelegateCommand<SourceViewModel>(ExecuteEditSource, CanEditSource);

            LoadSources();
            LoadProviders();

            _healthCheckService.SourceHealthChanged += OnSourceHealthChanged;
        }

        public SourcesSettingsViewModel(IStreamingSourceManager sourceManager)
            : this(sourceManager, new HealthCheckService(sourceManager))
        {
        }

        private void LoadSources()
        {
            Sources.Clear();
            foreach (var source in _sourceManager.Sources)
            {
                var provider = AvailableProviders.FirstOrDefault(p => p.SourceType == source.Type);
                var vm = new SourceViewModel(source, provider);

                var healthState = _healthCheckService.GetHealthState(source.Id);
                if (healthState != null)
                {
                    vm.UpdateHealthState(healthState);
                }

                Sources.Add(vm);
            }

            OnPropertyChanged(nameof(HasSources));
        }

        /// <summary>
        /// Load all registered IMusicSourceProvider implementations from DI.
        /// With the extension system removed, this is always Local + YouTube.
        /// </summary>
        private void LoadProviders()
        {
            AvailableProviders.Clear();

            try
            {
                var providers = App.Services?.GetService(typeof(IEnumerable<IMusicSourceProvider>)) as System.Collections.IEnumerable;
                if (providers != null)
                {
                    foreach (IMusicSourceProvider provider in providers)
                    {
                        AvailableProviders.Add(provider);
                    }
                }
            }
            catch
            {
                // If DI isn't available yet (designer), leave the list empty.
            }
        }

        private bool CanAddSource() => !IsAddingSource && AvailableProviders.Count > 0;

        private void ExecuteAddSource()
        {
            if (SelectedProvider == null && AvailableProviders.Count > 0)
                SelectedProvider = AvailableProviders[0];

            IsAddingSource = true;
        }

        public async Task<bool> AddSourceAsync(string name, IMusicSourceProvider provider, Dictionary<string, string> configuration)
        {
            if (provider == null || configuration == null)
                return false;

            try
            {
                IsAddingSource = true;

                var source = new StreamingSource
                {
                    Name = name,
                    Type = provider.SourceType,
                    Configuration = configuration
                };

                if (configuration.TryGetValue("url", out var url))
                    source.Url = url;
                if (configuration.TryGetValue("username", out var username))
                    source.Username = username;
                if (configuration.TryGetValue("password", out var password))
                    source.Password = password;
                if (configuration.TryGetValue("folderPath", out var folderPath) && provider.SourceType == Local)
                    source.Url = folderPath;

                var result = await _sourceManager.AddSourceAsync(source);

                if (result)
                {
                    var vm = new SourceViewModel(source, provider);
                    Sources.Add(vm);
                    OnPropertyChanged(nameof(HasSources));
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                TestResult = $"Failed to add source: {ex.Message}";
                TestResultSuccess = false;
                return false;
            }
            finally
            {
                IsAddingSource = false;
            }
        }

        public void CancelAddSource()
        {
            IsAddingSource = false;
            SelectedProvider = null;
        }

        private bool CanRemoveSource(SourceViewModel source) => source != null;

        private void ExecuteRemoveSource(SourceViewModel source)
        {
            if (source == null) return;

            var result = MessageBox.Show(
                $"Are you sure you want to remove '{source.Name}'?",
                "Remove Source",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _sourceManager.RemoveSource(source.Id);
                Sources.Remove(source);
                OnPropertyChanged(nameof(HasSources));
            }
        }

        private bool CanTestConnection(SourceViewModel source) => source != null && !IsTestingConnection;

        private async void ExecuteTestConnection(SourceViewModel source)
        {
            if (source == null) return;

            try
            {
                IsTestingConnection = true;
                source.IsConnecting = true;
                TestResult = null;

                var success = await _sourceManager.TestConnectionAsync(source.Id);

                TestResult = success ? "Connection successful!" : "Connection failed.";
                TestResultSuccess = success;

                if (success)
                {
                    source.IsConnected = true;
                    source.HealthStatus = SourceHealthStatus.Healthy;
                    source.ErrorMessage = null;
                }
                else
                {
                    source.HealthStatus = SourceHealthStatus.Unhealthy;
                    source.ErrorMessage = "Connection test failed";
                }
            }
            catch (Exception ex)
            {
                TestResult = $"Connection error: {ex.Message}";
                TestResultSuccess = false;
                source.ErrorMessage = ex.Message;
            }
            finally
            {
                IsTestingConnection = false;
                source.IsConnecting = false;
            }
        }

        private bool CanEditSource(SourceViewModel source) => source != null;

        private void ExecuteEditSource(SourceViewModel source)
        {
            if (source == null) return;
            source.IsExpanded = !source.IsExpanded;
        }

        /// <summary>
        /// Toggle whether a source type is shown on the Home screen.
        /// Persisted via Settings.Default.Discover* properties
        /// (same keys the former Discover tab used).
        /// </summary>
        public void ToggleHomeVisibility(SourceViewModel source)
        {
            if (source == null || string.IsNullOrEmpty(source.Type)) return;

            var newValue = !IsHomeEnabled(source.Type);
            SetHomeEnabled(source.Type, newValue);

            // Refresh the bindable property on all sources of the same type
            foreach (var s in Sources.Where(s => s.Type == source.Type))
            {
                s.RefreshHomeEnabled();
            }
        }

        /// <summary>
        /// Returns true if the given source type should appear on the Home screen.
        /// </summary>
        public static bool IsHomeEnabled(string sourceType)
        {
            return sourceType switch
            {
                Local => Settings.Default.DiscoverLibrary,
                YouTube => Settings.Default.DiscoverYouTube,
                Subsonic => Settings.Default.DiscoverSubsonic,
                _ => GetEnabledExtraSources().Contains(sourceType)
            };
        }

        /// <summary>
        /// Persists the Home-visibility setting for a source type.
        /// </summary>
        public static void SetHomeEnabled(string sourceType, bool enabled)
        {
            switch (sourceType)
            {
                case Local:
                    Settings.Default.DiscoverLibrary = enabled;
                    break;
                case YouTube:
                    Settings.Default.DiscoverYouTube = enabled;
                    break;
                case Subsonic:
                    Settings.Default.DiscoverSubsonic = enabled;
                    break;
                default:
                    var extra = GetEnabledExtraSources();
                    if (enabled) extra.Add(sourceType);
                    else extra.Remove(sourceType);
                    Settings.Default.DiscoverExtraSources = JsonConvert.SerializeObject(extra.ToList());
                    break;
            }
            Settings.Default.Save();
        }

        private static HashSet<string> GetEnabledExtraSources()
        {
            var json = Settings.Default.DiscoverExtraSources;
            if (string.IsNullOrEmpty(json)) return new HashSet<string>();
            try
            {
                var list = JsonConvert.DeserializeObject<List<string>>(json);
                return list != null ? new HashSet<string>(list) : new HashSet<string>();
            }
            catch
            {
                return new HashSet<string>();
            }
        }

        private void OnSourceHealthChanged(object sender, SourceHealthEventArgs e)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                var sourceVm = Sources.FirstOrDefault(s => s.Id == e.SourceId);
                if (sourceVm != null)
                {
                    var healthState = _healthCheckService.GetHealthState(e.SourceId);
                    sourceVm.UpdateHealthState(healthState);
                }
            });
        }

        public void RefreshSources()
        {
            LoadSources();
        }

        public void Dispose()
        {
            _healthCheckService.SourceHealthChanged -= OnSourceHealthChanged;
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        #region Delegate Command Implementations

        private class DelegateCommand : ICommand
        {
            private readonly Action _execute;
            private readonly Func<bool> _canExecute;

            public DelegateCommand(Action execute, Func<bool> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }

            public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
            public void Execute(object parameter) => _execute();
        }

        private class DelegateCommand<T> : ICommand
        {
            private readonly Action<T> _execute;
            private readonly Func<T, bool> _canExecute;

            public DelegateCommand(Action<T> execute, Func<T, bool> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }

            public bool CanExecute(object parameter) => _canExecute?.Invoke((T)parameter) ?? true;
            public void Execute(object parameter) => _execute((T)parameter);
        }

        #endregion
    }
}

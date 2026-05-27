using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.Properties;

namespace Musicefy.ViewModels
{
    public class SourcesSettingsViewModel : ViewModelBase
    {
        private readonly IStreamingSourceManager _sourceManager;
        private readonly IExtensionManager _extensionManager;
        private readonly IServiceProvider _serviceProvider;

        private IMusicSourceProvider _selectedProvider;
        private string _sourceName;
        private string _testStatusText;
        private bool _isTestSuccess;
        private bool _isTestVisible;
        private bool _isTestEnabled = true;

        public ObservableCollection<IMusicSourceProvider> Providers { get; } = new ObservableCollection<IMusicSourceProvider>();

        public IMusicSourceProvider SelectedProvider
        {
            get => _selectedProvider;
            set
            {
                if (SetProperty(ref _selectedProvider, value))
                    OnProviderChanged();
            }
        }

        public string SourceName
        {
            get => _sourceName;
            set => SetProperty(ref _sourceName, value);
        }

        public string TestStatusText
        {
            get => _testStatusText;
            set => SetProperty(ref _testStatusText, value);
        }

        public bool IsTestVisible
        {
            get => _isTestVisible;
            set => SetProperty(ref _isTestVisible, value);
        }

        public bool IsTestEnabled
        {
            get => _isTestEnabled;
            set => SetProperty(ref _isTestEnabled, value);
        }

        public bool IsTestSuccess
        {
            get => _isTestSuccess;
            set => SetProperty(ref _isTestSuccess, value);
        }

        public ObservableCollection<SourceDisplayItem> Sources { get; } = new ObservableCollection<SourceDisplayItem>();

        public ICommand TestConnectionCommand { get; }
        public ICommand AddSourceCommand { get; }
        public ICommand RemoveSourceCommand { get; }

        public event Action<IMusicSourceProvider> ProviderChanged;
        public event Func<IMusicSourceProvider, Task<bool>> TestConnectionRequested;
        public event Func<string, string, IMusicSourceProvider, Task<bool>> AddSourceRequested;

        public SourcesSettingsViewModel(IStreamingSourceManager sourceManager, IExtensionManager extensionManager, IServiceProvider serviceProvider)
        {
            _sourceManager = sourceManager;
            _extensionManager = extensionManager;
            _serviceProvider = serviceProvider;

            TestConnectionCommand = new RelayCommand(async _ => await TestConnectionAsync());
            AddSourceCommand = new RelayCommand(async _ => await AddSourceAsync());
            RemoveSourceCommand = new RelayCommand(ExecuteRemoveSource);

            LoadProviders();
            LoadSources();
        }

        public SourcesSettingsViewModel() : this(
            App.Services.GetService<IStreamingSourceManager>(),
            App.Services.GetService<IExtensionManager>(),
            App.Services)
        {
        }

        public void LoadProviders()
        {
            Providers.Clear();

            var builtInProviders = _serviceProvider.GetServices<IMusicSourceProvider>();
            var installedExtensions = _extensionManager.GetInstalledExtensions();
            var installedSourceTypes = new System.Collections.Generic.HashSet<string>(installedExtensions.Select(e => e.SourceType));

            foreach (var p in builtInProviders)
            {
                if (p.SourceType == SourceTypes.Local || installedSourceTypes.Contains(p.SourceType))
                    Providers.Add(p);
            }

            foreach (var p in _extensionManager.ExtensionProviders)
            {
                if (!Providers.Any(x => x.SourceType == p.SourceType))
                    Providers.Add(p);
            }

            if (Providers.Count > 0)
                SelectedProvider = Providers[0];
        }

        public void LoadSources()
        {
            Sources.Clear();
            foreach (var s in _sourceManager.Sources)
            {
                Sources.Add(new SourceDisplayItem
                {
                    Id = s.Id,
                    Name = s.Name,
                    Type = s.Type,
                    DisplayType = s.Type,
                    IsConnected = s.IsConnected
                });
            }
        }

        private void OnProviderChanged()
        {
            SourceName = SelectedProvider != null ? $"{SelectedProvider.DisplayName} Source" : "";
            ProviderChanged?.Invoke(SelectedProvider);
        }

        private async Task TestConnectionAsync()
        {
            if (SelectedProvider == null) return;

            IsTestEnabled = false;
            IsTestVisible = true;
            TestStatusText = "Testing...";

            try
            {
                if (TestConnectionRequested != null)
                {
                    IsTestSuccess = await TestConnectionRequested(SelectedProvider);
                    TestStatusText = IsTestSuccess ? "Connected!" : "Failed";
                }
            }
            catch (Exception ex)
            {
                IsTestSuccess = false;
                TestStatusText = $"Failed: {ex.Message}";
            }
            finally
            {
                IsTestEnabled = true;
            }
        }

        private async Task AddSourceAsync()
        {
            if (SelectedProvider == null) return;

            if (AddSourceRequested != null)
            {
                await AddSourceRequested(SourceName, SelectedProvider.SourceType, SelectedProvider);
                SourceName = SelectedProvider != null ? $"{SelectedProvider.DisplayName} Source" : "";
                LoadSources();
            }
        }

        private void ExecuteRemoveSource(object parameter)
        {
            if (parameter is string sourceId)
            {
                _sourceManager.RemoveSource(sourceId);
                LoadSources();
            }
        }
    }

    public class SourceDisplayItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string DisplayType { get; set; }
        public bool IsConnected { get; set; }
    }
}

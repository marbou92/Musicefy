using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Musicefy.ViewModels
{
    public class ExtensionsSettingsViewModel : ViewModelBase
    {
        private readonly IExtensionManager _extensionManager;
        private bool _isLoading;
        private ObservableCollection<IMusicSourceProvider> _builtInProviders = new ObservableCollection<IMusicSourceProvider>();
        private ObservableCollection<ExtensionManifest> _installedExtensions = new ObservableCollection<ExtensionManifest>();
        private ObservableCollection<ExtensionManifest> _availableExtensions = new ObservableCollection<ExtensionManifest>();

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ObservableCollection<IMusicSourceProvider> BuiltInProviders
        {
            get => _builtInProviders;
            set => SetProperty(ref _builtInProviders, value);
        }

        public ObservableCollection<ExtensionManifest> InstalledExtensions
        {
            get => _installedExtensions;
            set => SetProperty(ref _installedExtensions, value);
        }

        public ObservableCollection<ExtensionManifest> AvailableExtensions
        {
            get => _availableExtensions;
            set => SetProperty(ref _availableExtensions, value);
        }

        public ICommand InstallCommand { get; }
        public ICommand UninstallCommand { get; }
        public ICommand RefreshCommand { get; }

        public ExtensionsSettingsViewModel()
        {
            _extensionManager = App.Services.GetService<IExtensionManager>();

            InstallCommand = new RelayCommand(async _ => await InstallAsync(_));
            UninstallCommand = new RelayCommand(async _ => await UninstallAsync(_));
            RefreshCommand = new RelayCommand(async _ => await LoadAsync());

            LoadBuiltInProviders();
            LoadAvailableExtensions();
            _ = LoadAsync();
        }

        private void LoadBuiltInProviders()
        {
            var providers = App.Services.GetServices<IMusicSourceProvider>();
            var local = providers.FirstOrDefault(p => p.SourceType == "Local");
            BuiltInProviders = new ObservableCollection<IMusicSourceProvider>();
            if (local != null)
                BuiltInProviders.Add(local);
        }

        private void LoadAvailableExtensions()
        {
            var providers = App.Services.GetServices<IMusicSourceProvider>();
            var installed = _extensionManager.GetInstalledExtensions();
            var installedIds = new System.Collections.Generic.HashSet<string>(installed.Select(e => e.Id));

            var available = new ObservableCollection<ExtensionManifest>();
            foreach (var p in providers)
            {
                if (p.SourceType == "Local")
                    continue;

                var id = $"builtin_{p.SourceType.ToLower()}";
                if (installedIds.Contains(id))
                    continue;

                available.Add(new ExtensionManifest
                {
                    Id = id,
                    Name = p.DisplayName,
                    SourceType = p.SourceType,
                    Version = "1.0.0",
                    Author = "Musicefy",
                    Description = p.Description
                });
            }
            AvailableExtensions = available;
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var installed = _extensionManager.GetInstalledExtensions();
                InstalledExtensions = new ObservableCollection<ExtensionManifest>(installed);
                LoadAvailableExtensions();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task InstallAsync(object parameter)
        {
            if (parameter is ExtensionManifest extension)
            {
                try
                {
                    if (extension.Id.StartsWith("builtin_"))
                        _extensionManager.MarkBuiltInAsInstalled(extension.SourceType, extension.Name);
                    else
                        await _extensionManager.InstallExtensionAsync(extension);

                    System.Windows.MessageBox.Show($"Extension '{extension.Name}' installed successfully.", "Musicefy",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to install extension: {ex.Message}", "Error",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }
        }

        private async Task UninstallAsync(object parameter)
        {
            if (parameter is ExtensionManifest extension)
            {
                try
                {
                    if (extension.Id.StartsWith("builtin_"))
                        _extensionManager.MarkBuiltInAsUninstalled(extension.Id);
                    else
                        await _extensionManager.UninstallExtensionAsync(extension.Id);

                    System.Windows.MessageBox.Show($"Extension '{extension.Name}' uninstalled.", "Musicefy",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to uninstall extension: {ex.Message}", "Error",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }
        }
    }
}

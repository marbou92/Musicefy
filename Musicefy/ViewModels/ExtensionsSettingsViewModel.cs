using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using static Musicefy.Core.SourceTypes;

namespace Musicefy.ViewModels
{
    public class ExtensionsSettingsViewModel : ViewModelBase
    {
        private readonly IExtensionManager _extensionManager;
        private readonly IServiceProvider _serviceProvider;
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

        public ExtensionsSettingsViewModel(IExtensionManager extensionManager, IServiceProvider serviceProvider)
        {
            _extensionManager = extensionManager;
            _serviceProvider = serviceProvider;

            InstallCommand = new RelayCommand(async _ => await InstallAsync(_));
            UninstallCommand = new RelayCommand(async _ => await UninstallAsync(_));
            RefreshCommand = new RelayCommand(async _ => await LoadAsync());

            LoadBuiltInProviders();
            LoadAvailableExtensions();
            _ = LoadAsync();
        }

        public ExtensionsSettingsViewModel() : this(
            App.Services.GetService<IExtensionManager>(),
            App.Services)
        {
        }

        /// <summary>
        /// True if the given extension manifest corresponds to a protected
        /// source type that cannot be uninstalled (e.g. Local).
        /// Used by the UI to hide the Uninstall button.
        /// </summary>
        public bool IsProtected(ExtensionManifest extension)
        {
            if (extension == null) return false;
            return _extensionManager.IsProtectedSourceType(extension.SourceType);
        }

        /// <summary>
        /// True if the given built-in provider corresponds to a protected
        /// source type that cannot be uninstalled (e.g. Local).
        /// </summary>
        public bool IsProtected(IMusicSourceProvider provider)
        {
            if (provider == null) return false;
            return _extensionManager.IsProtectedSourceType(provider.SourceType);
        }

        private void LoadBuiltInProviders()
        {
            var providers = _serviceProvider.GetServices<IMusicSourceProvider>();
            // Only Local is permanently pre-installed. Subsonic/YouTube are
            // installable extensions that live in AvailableExtensions.
            var local = providers.FirstOrDefault(p => p.SourceType == Local);
            BuiltInProviders = new ObservableCollection<IMusicSourceProvider>();
            if (local != null)
                BuiltInProviders.Add(local);
        }

        private void LoadAvailableExtensions()
        {
            var providers = _serviceProvider.GetServices<IMusicSourceProvider>();
            var installed = _extensionManager.GetInstalledExtensions();
            var installedIds = new HashSet<string>(installed.Select(e => e.Id));

            var available = new ObservableCollection<ExtensionManifest>();
            foreach (var p in providers)
            {
                // Local is pre-installed, not in AvailableExtensions.
                if (p.SourceType == Local)
                    continue;

                var id = $"builtin_{p.SourceType.ToLower()}";
                // If the user has already installed this built-in extension,
                // it shows up in InstalledExtensions — don't also show it here.
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
                // Never show protected source types (e.g. Local) in the
                // InstalledExtensions list — they appear in BuiltInProviders
                // with a "Protected" badge and no Uninstall button. This is
                // defense-in-depth at the UI layer: even if a stale manifest
                // exists on disk, it never surfaces an uninstallable entry.
                var visible = installed
                    .Where(e => !_extensionManager.IsProtectedSourceType(e.SourceType))
                    .ToList();
                InstalledExtensions = new ObservableCollection<ExtensionManifest>(visible);
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
                // UI-level guard: never offer to uninstall protected source types.
                // Service-level guard also enforces this in ExtensionManagerImpl —
                // this is defense-in-depth.
                if (_extensionManager.IsProtectedSourceType(extension.SourceType))
                {
                    System.Windows.MessageBox.Show(
                        $"The '{extension.Name}' extension is required by the application and cannot be uninstalled.",
                        "Musicefy",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    return;
                }

                var confirm = System.Windows.MessageBox.Show(
                    $"Are you sure you want to uninstall '{extension.Name}'?",
                    "Uninstall Extension",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (confirm != System.Windows.MessageBoxResult.Yes)
                    return;

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

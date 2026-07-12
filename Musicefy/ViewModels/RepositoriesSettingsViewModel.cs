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
    public class RepositoriesSettingsViewModel : ViewModelBase
    {
        private readonly IExtensionManager _extensionManager;
        private string _repoUrlInput;
        private bool _isLoading;
        private ObservableCollection<ExtensionRepoManifest> _repos = new ObservableCollection<ExtensionRepoManifest>();

        public string RepoUrlInput
        {
            get => _repoUrlInput;
            set => SetProperty(ref _repoUrlInput, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ObservableCollection<ExtensionRepoManifest> Repos
        {
            get => _repos;
            set => SetProperty(ref _repos, value);
        }

        public ICommand AddRepoCommand { get; }
        public ICommand RemoveRepoCommand { get; }
        public ICommand InstallCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand OpenDeveloperGuideCommand { get; }

        public RepositoriesSettingsViewModel(IExtensionManager extensionManager)
        {
            _extensionManager = extensionManager;

            AddRepoCommand = new RelayCommand(async _ => await AddRepoAsync());
            RemoveRepoCommand = new RelayCommand(async _ => await RemoveRepoAsync(_));
            InstallCommand = new RelayCommand(async _ => await InstallAsync(_));
            RefreshCommand = new RelayCommand(async _ => await LoadAsync());
            OpenDeveloperGuideCommand = new RelayCommand(_ => OpenDeveloperGuide());

            _ = LoadAsync();
        }

        public RepositoriesSettingsViewModel() : this(
            App.Services.GetService<IExtensionManager>())
        {
        }

        /// <summary>
        /// Returns true if the given extension manifest is currently installed
        /// in the IExtensionManager. Used by the UI to show an "Installed" badge
        /// instead of the Install button.
        /// </summary>
        public bool IsExtensionInstalled(ExtensionManifest extension)
        {
            if (extension == null) return false;
            return _extensionManager.GetInstalledExtensions()
                .Any(e => string.Equals(e.Id, extension.Id, StringComparison.OrdinalIgnoreCase));
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var repos = await _extensionManager.FetchReposAsync();
                Repos = new ObservableCollection<ExtensionRepoManifest>(repos);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task AddRepoAsync()
        {
            if (string.IsNullOrWhiteSpace(RepoUrlInput))
                return;

            try
            {
                await _extensionManager.AddRepoAsync(RepoUrlInput.Trim());
                RepoUrlInput = "";
                await LoadAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to add repo: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        private async Task RemoveRepoAsync(object parameter)
        {
            if (parameter is ExtensionRepoManifest repo)
            {
                bool removed = _extensionManager.RemoveRepo(repo.Url);
                if (!removed)
                {
                    System.Windows.MessageBox.Show("The official repository cannot be removed.", "Musicefy",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }
                await LoadAsync();
            }
        }

        private async Task InstallAsync(object parameter)
        {
            if (parameter is ExtensionManifest extension)
            {
                // If already installed, just inform the user.
                if (IsExtensionInstalled(extension))
                {
                    System.Windows.MessageBox.Show($"Extension '{extension.Name}' is already installed.",
                        "Musicefy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    return;
                }

                try
                {
                    await _extensionManager.InstallExtensionAsync(extension);
                    System.Windows.MessageBox.Show($"Extension '{extension.Name}' installed.", "Musicefy",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to install: {ex.Message}", "Error",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }
        }

        private void OpenDeveloperGuide()
        {
            // The developer guide lives in the dedicated Musicefy-Extensions repo
            // on GitHub. Open it in the user's default browser.
            const string guideUrl = "https://github.com/marbou92/Musicefy-Extensions";

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = guideUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Could not open the developer guide in your browser.\n\nURL: {guideUrl}\n\nError: {ex.Message}",
                    "Musicefy", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
    }
}

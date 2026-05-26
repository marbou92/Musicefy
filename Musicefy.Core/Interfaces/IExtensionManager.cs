using System.Collections.Generic;
using System.Threading.Tasks;
using Musicefy.Core.Models;

namespace Musicefy.Core.Interfaces
{
    public interface IExtensionManager
    {
        IReadOnlyList<IMusicSourceProvider> ExtensionProviders { get; }

        IReadOnlyList<string> RepoUrls { get; }
        Task AddRepoAsync(string repoUrl);
        bool RemoveRepo(string repoUrl);
        Task<List<ExtensionRepoManifest>> FetchReposAsync();

        Task InstallExtensionAsync(ExtensionManifest extension);
        Task UninstallExtensionAsync(string extensionId);
        IReadOnlyList<ExtensionManifest> GetInstalledExtensions();
        void LoadExtensions();
        void MarkBuiltInAsInstalled(string sourceType, string displayName);
        void MarkBuiltInAsUninstalled(string extensionId);
    }
}

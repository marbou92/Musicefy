using System.Collections.Generic;
using System.Threading.Tasks;
using Musicefy.Core.Models;

namespace Musicefy.Core.Interfaces
{
    public interface IExtensionManager
    {
        /// <summary>
        /// Source types that are always installed and cannot be uninstalled.
        /// Currently only <c>Musicefy.Core.SourceTypes.Local</c>.
        /// </summary>
        IReadOnlyList<string> ProtectedSourceTypes { get; }

        /// <summary>
        /// All providers that are currently enabled — i.e. visible in the
        /// "Add Source" dialog and usable for creating new sources.
        /// Local is always enabled; other built-in providers (Subsonic,
        /// YouTube) are enabled only after their <c>builtin_&lt;type&gt;</c>
        /// extension manifest is marked installed. Extension DLL providers
        /// are enabled as soon as they are installed.
        /// </summary>
        IReadOnlyList<IMusicSourceProvider> EnabledProviders { get; }

        /// <summary>
        /// All loaded providers (built-in DI providers + extension DLL providers).
        /// Use <see cref="EnabledProviders"/> for the user-visible filter.
        /// </summary>
        IReadOnlyList<IMusicSourceProvider> ExtensionProviders { get; }

        IReadOnlyList<string> RepoUrls { get; }
        Task AddRepoAsync(string repoUrl);
        bool RemoveRepo(string repoUrl);
        Task<List<ExtensionRepoManifest>> FetchReposAsync();

        Task InstallExtensionAsync(ExtensionManifest extension);
        Task UninstallExtensionAsync(string extensionId);

        /// <summary>
        /// Returns true if a provider with the given source type is currently
        /// enabled (visible to the user as an addable source type).
        /// Local is always enabled.
        /// </summary>
        bool IsProviderEnabled(string sourceType);

        /// <summary>
        /// Returns true if a source type is protected and cannot be uninstalled
        /// (currently only <c>Musicefy.Core.SourceTypes.Local</c>).
        /// </summary>
        bool IsProtectedSourceType(string sourceType);

        IReadOnlyList<ExtensionManifest> GetInstalledExtensions();
        void LoadExtensions();
        void MarkBuiltInAsInstalled(string sourceType, string displayName);
        void MarkBuiltInAsUninstalled(string extensionId);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Newtonsoft.Json;
using static Musicefy.Core.SourceTypes;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Manages extension repositories, installed extension manifests, and the
    /// runtime loading of extension DLLs.
    ///
    /// Provider visibility model
    /// -------------------------
    ///   * <see cref="Local"/> is ALWAYS enabled and CANNOT be uninstalled
    ///     (it is the in-app local-library provider and is required for the
    ///      app to function). This is enforced in
    ///     <see cref="IsProtectedSourceType"/> and
    ///     <see cref="MarkBuiltInAsUninstalled"/> / <see cref="UninstallExtensionAsync"/>.
    ///   * Other built-in providers (Subsonic, YouTube) are loaded from DI
    ///     but are NOT enabled until their <c>builtin_&lt;type&gt;</c>
    ///     extension manifest is marked installed via
    ///     <see cref="MarkBuiltInAsInstalled"/>.
    ///   * Extension DLL providers are enabled as soon as the corresponding
    ///     extension is installed.
    /// </summary>
    public class ExtensionManagerImpl : IExtensionManager
    {
        private const string OfficialRepoUrl = "https://raw.githubusercontent.com/marbou92/Musicefy-Extensions/main/repo.json";

        private readonly HttpClient _httpClient;
        private readonly string _extensionsDir;
        private readonly string _reposFilePath;
        private readonly List<string> _repoUrls = new List<string>();
        private readonly List<IMusicSourceProvider> _extensionProviders = new List<IMusicSourceProvider>();
        private readonly List<ExtensionManifest> _installed = new List<ExtensionManifest>();
        private readonly HashSet<string> _enabledSourceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly Lazy<IEnumerable<IMusicSourceProvider>> _diProvidersLazy;
        private readonly IServiceProvider _serviceProvider;

        public IReadOnlyList<string> ProtectedSourceTypes { get; } = new List<string> { Local };

        public ExtensionManagerImpl() : this(null)
        {
        }

        public ExtensionManagerImpl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Musicefy/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var musicefyDir = Path.Combine(appData, "Musicefy");
            _extensionsDir = Path.Combine(musicefyDir, "Extensions");
            _reposFilePath = Path.Combine(musicefyDir, "repos.json");

            if (!Directory.Exists(_extensionsDir))
                Directory.CreateDirectory(_extensionsDir);

            // Local is ALWAYS enabled — non-bypassable.
            _enabledSourceTypes.Add(Local);

            _diProvidersLazy = new Lazy<IEnumerable<IMusicSourceProvider>>(() =>
            {
                try
                {
                    return _serviceProvider?.GetServices<IMusicSourceProvider>()
                        ?? Enumerable.Empty<IMusicSourceProvider>();
                }
                catch
                {
                    return Enumerable.Empty<IMusicSourceProvider>();
                }
            }, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

            LoadRepos();
            LoadInstalledExtensions();
            RebuildEnabledSet();
        }

        public IReadOnlyList<IMusicSourceProvider> ExtensionProviders => _extensionProviders.AsReadOnly();

        public IReadOnlyList<IMusicSourceProvider> EnabledProviders
        {
            get
            {
                var result = new List<IMusicSourceProvider>();
                foreach (var p in _diProvidersLazy.Value)
                {
                    if (_enabledSourceTypes.Contains(p.SourceType))
                        result.Add(p);
                }
                foreach (var p in _extensionProviders)
                {
                    if (_enabledSourceTypes.Contains(p.SourceType) &&
                        !result.Any(r => string.Equals(r.SourceType, p.SourceType, StringComparison.OrdinalIgnoreCase)))
                        result.Add(p);
                }
                return result.AsReadOnly();
            }
        }

        public IReadOnlyList<string> RepoUrls => _repoUrls.AsReadOnly();

        public bool IsProviderEnabled(string sourceType)
        {
            if (string.IsNullOrEmpty(sourceType)) return false;
            // Local is always enabled — non-bypassable.
            if (string.Equals(sourceType, Local, StringComparison.OrdinalIgnoreCase))
                return true;
            return _enabledSourceTypes.Contains(sourceType);
        }

        public bool IsProtectedSourceType(string sourceType)
        {
            return ProtectedSourceTypes.Any(p =>
                string.Equals(p, sourceType, StringComparison.OrdinalIgnoreCase));
        }

        public async Task AddRepoAsync(string repoUrl)
        {
            if (string.IsNullOrWhiteSpace(repoUrl))
                throw new ArgumentException("Repo URL is required.");

            if (!Uri.TryCreate(repoUrl, UriKind.Absolute, out var uri) ||
                (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Repo URL must be a valid HTTP or HTTPS URL.");

            if (_repoUrls.Contains(repoUrl, StringComparer.OrdinalIgnoreCase))
                return;

            _repoUrls.Add(repoUrl);
            SaveRepos();
            await Task.CompletedTask;
        }

        public bool RemoveRepo(string repoUrl)
        {
            if (string.Equals(repoUrl, OfficialRepoUrl, StringComparison.OrdinalIgnoreCase))
                return false;

            _repoUrls.Remove(repoUrl);
            SaveRepos();
            return true;
        }

        public async Task<List<ExtensionRepoManifest>> FetchReposAsync()
        {
            var tasks = _repoUrls.Select(url => FetchSingleRepoAsync(url));
            var results = await Task.WhenAll(tasks);
            return results.Where(r => r != null).ToList();
        }

        private async Task<ExtensionRepoManifest> FetchSingleRepoAsync(string url)
        {
            try
            {
                var response = await _httpClient.GetStringAsync(url);
                var manifest = JsonConvert.DeserializeObject<ExtensionRepoManifest>(response);
                if (manifest != null)
                    manifest.Url = url;
                return manifest;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to fetch repo {url}: {ex.Message}");
                return null;
            }
        }

        public async Task InstallExtensionAsync(ExtensionManifest extension)
        {
            if (string.IsNullOrWhiteSpace(extension.Id) || string.IsNullOrWhiteSpace(extension.DownloadUrl))
                throw new ArgumentException("Extension must have an Id and DownloadUrl.");

            var safeId = SanitizeExtensionId(extension.Id);
            var extDir = Path.Combine(_extensionsDir, safeId);
            if (!Directory.Exists(extDir))
                Directory.CreateDirectory(extDir);

            var dllPath = Path.Combine(extDir, $"{safeId}.dll");

            var response = await _httpClient.GetAsync(extension.DownloadUrl);
            response.EnsureSuccessStatusCode();
            var dllBytes = await response.Content.ReadAsByteArrayAsync();

            if (!string.IsNullOrEmpty(extension.Hash))
            {
                using (var sha256 = SHA256.Create())
                {
                    var computedHash = sha256.ComputeHash(dllBytes);
                    var computedHex = BitConverter.ToString(computedHash).Replace("-", "").ToLower();
                    if (!string.Equals(computedHex, extension.Hash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Extension hash verification failed.");
                }
            }

            File.WriteAllBytes(dllPath, dllBytes);

            var metaPath = Path.Combine(extDir, "manifest.json");
            File.WriteAllText(metaPath, JsonConvert.SerializeObject(extension, Formatting.Indented));

            if (!_installed.Any(e => e.Id == extension.Id))
                _installed.Add(extension);

            LoadExtensionAssembly(extDir);

            if (!string.IsNullOrEmpty(extension.SourceType))
                _enabledSourceTypes.Add(extension.SourceType);
        }

        public async Task UninstallExtensionAsync(string extensionId)
        {
            if (string.IsNullOrEmpty(extensionId))
                throw new ArgumentException("Extension ID is required.", nameof(extensionId));

            // Defense-in-depth: never allow uninstalling a protected source type's extension.
            var manifest = _installed.FirstOrDefault(e => e.Id == extensionId);
            if (manifest != null && IsProtectedSourceType(manifest.SourceType))
                throw new InvalidOperationException(
                    $"The '{manifest.SourceType}' source type is protected and cannot be uninstalled.");

            var safeId = SanitizeExtensionId(extensionId);
            var extDir = Path.Combine(_extensionsDir, safeId);
            if (Directory.Exists(extDir))
            {
                try { Directory.Delete(extDir, true); }
                catch (IOException) { /* file may be locked; ignore */ }
            }

            _installed.RemoveAll(e => e.Id == extensionId);

            if (manifest != null && !string.IsNullOrEmpty(manifest.SourceType))
            {
                _extensionProviders.RemoveAll(p =>
                    string.Equals(p.SourceType, manifest.SourceType, StringComparison.OrdinalIgnoreCase));
                _enabledSourceTypes.Remove(manifest.SourceType);
            }

            await Task.CompletedTask;
        }

        public void MarkBuiltInAsInstalled(string sourceType, string displayName)
        {
            if (string.IsNullOrWhiteSpace(sourceType))
                throw new ArgumentException("Source type is required.", nameof(sourceType));

            var id = $"builtin_{sourceType.ToLower()}";
            if (_installed.Any(e => e.Id == id))
            {
                _enabledSourceTypes.Add(sourceType);
                return;
            }

            var manifest = new ExtensionManifest
            {
                Id = id,
                Name = displayName,
                SourceType = sourceType,
                Version = "1.0.0",
                Author = "Musicefy",
                Description = $"Built-in {displayName} provider"
            };

            var extDir = Path.Combine(_extensionsDir, id);
            if (!Directory.Exists(extDir))
                Directory.CreateDirectory(extDir);

            var metaPath = Path.Combine(extDir, "manifest.json");
            File.WriteAllText(metaPath, JsonConvert.SerializeObject(manifest, Formatting.Indented));

            if (!_installed.Any(e => e.Id == id))
                _installed.Add(manifest);

            _enabledSourceTypes.Add(sourceType);
        }

        public void MarkBuiltInAsUninstalled(string extensionId)
        {
            if (string.IsNullOrEmpty(extensionId) || !extensionId.StartsWith("builtin_"))
                return;

            var sourceType = extensionId.Substring("builtin_".Length);

            // Non-bypassable: Local cannot be uninstalled.
            if (IsProtectedSourceType(sourceType))
                throw new InvalidOperationException(
                    $"The '{sourceType}' source type is protected and cannot be uninstalled.");

            var safeId = SanitizeExtensionId(extensionId);
            var extDir = Path.Combine(_extensionsDir, safeId);
            if (Directory.Exists(extDir))
            {
                try { Directory.Delete(extDir, true); }
                catch (IOException) { /* ignore */ }
            }

            _installed.RemoveAll(e => e.Id == extensionId);
            _enabledSourceTypes.Remove(sourceType);
        }

        public IReadOnlyList<ExtensionManifest> GetInstalledExtensions()
        {
            return _installed.AsReadOnly();
        }

        public void LoadExtensions()
        {
            if (!Directory.Exists(_extensionsDir))
                return;

            foreach (var extDir in Directory.GetDirectories(_extensionsDir))
            {
                LoadExtensionAssembly(extDir);
            }

            LoadInstalledExtensions();
            RebuildEnabledSet();
        }

        private void LoadExtensionAssembly(string extDir)
        {
            try
            {
                foreach (var dll in Directory.GetFiles(extDir, "*.dll"))
                {
                    var assembly = Assembly.Load(File.ReadAllBytes(dll));
                    foreach (var type in assembly.GetTypes())
                    {
                        if (typeof(IMusicSourceProvider).IsAssignableFrom(type) && !type.IsAbstract && type.IsClass)
                        {
                            var provider = (IMusicSourceProvider)Activator.CreateInstance(type);
                            _extensionProviders.Add(provider);
                            if (!string.IsNullOrEmpty(provider.SourceType))
                                _enabledSourceTypes.Add(provider.SourceType);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load extension from {extDir}: {ex.Message}");
            }
        }

        private void LoadInstalledExtensions()
        {
            if (!Directory.Exists(_extensionsDir))
                return;

            foreach (var extDir in Directory.GetDirectories(_extensionsDir))
            {
                var metaPath = Path.Combine(extDir, "manifest.json");
                if (File.Exists(metaPath))
                {
                    try
                    {
                        var manifest = JsonConvert.DeserializeObject<ExtensionManifest>(File.ReadAllText(metaPath));
                        if (manifest != null && !_installed.Any(e => e.Id == manifest.Id))
                            _installed.Add(manifest);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to load extension manifest {metaPath}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Rebuilds the enabled-source-types set from the installed manifests.
        /// Local is always re-added (protected).
        /// </summary>
        private void RebuildEnabledSet()
        {
            _enabledSourceTypes.Clear();
            _enabledSourceTypes.Add(Local); // non-bypassable

            foreach (var manifest in _installed)
            {
                if (!string.IsNullOrEmpty(manifest.SourceType))
                    _enabledSourceTypes.Add(manifest.SourceType);
            }

            // Extension DLL providers that are loaded are also implicitly enabled.
            foreach (var provider in _extensionProviders)
            {
                if (!string.IsNullOrEmpty(provider.SourceType))
                    _enabledSourceTypes.Add(provider.SourceType);
            }
        }

        private void LoadRepos()
        {
            try
            {
                if (!File.Exists(_reposFilePath))
                {
                    _repoUrls.Add(OfficialRepoUrl);
                    return;
                }

                var json = File.ReadAllText(_reposFilePath);
                var urls = JsonConvert.DeserializeObject<List<string>>(json);
                if (urls != null)
                {
                    _repoUrls.Clear();
                    _repoUrls.AddRange(urls);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load repos: {ex.Message}");
            }

            if (!_repoUrls.Contains(OfficialRepoUrl, StringComparer.OrdinalIgnoreCase))
                _repoUrls.Add(OfficialRepoUrl);
        }

        private void SaveRepos()
        {
            try
            {
                var urlsToSave = _repoUrls
                    .Where(u => !string.Equals(u, OfficialRepoUrl, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var json = JsonConvert.SerializeObject(urlsToSave, Formatting.Indented);
                File.WriteAllText(_reposFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save repos: {ex.Message}");
            }
        }

        private static string SanitizeExtensionId(string extensionId)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var sanitized = new string(extensionId.Where(c => !invalid.Contains(c)).ToArray());
            if (sanitized.Length == 0)
                throw new ArgumentException("Extension ID contains no valid characters.");
            return sanitized;
        }
    }
}

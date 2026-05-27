using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Newtonsoft.Json;

namespace Musicefy.Core.Services
{
    public class ExtensionManagerImpl : IExtensionManager
    {
        private readonly HttpClient _httpClient;
        private readonly string _extensionsDir;
        private readonly string _reposFilePath;
        private readonly List<string> _repoUrls = new List<string>();
        private readonly List<IMusicSourceProvider> _extensionProviders = new List<IMusicSourceProvider>();
        private readonly List<ExtensionManifest> _installed = new List<ExtensionManifest>();

        public IReadOnlyList<IMusicSourceProvider> ExtensionProviders => _extensionProviders.AsReadOnly();
        public IReadOnlyList<string> RepoUrls => _repoUrls.AsReadOnly();

        public ExtensionManagerImpl()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Musicefy/1.0");

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var musicefyDir = Path.Combine(appData, "Musicefy");
            _extensionsDir = Path.Combine(musicefyDir, "Extensions");
            _reposFilePath = Path.Combine(musicefyDir, "repos.json");

            if (!Directory.Exists(_extensionsDir))
                Directory.CreateDirectory(_extensionsDir);

            LoadRepos();
            LoadInstalledExtensions();
        }

        public async Task AddRepoAsync(string repoUrl)
        {
            if (string.IsNullOrWhiteSpace(repoUrl))
                throw new ArgumentException("Repo URL is required.");

            if (_repoUrls.Contains(repoUrl, StringComparer.OrdinalIgnoreCase))
                return;

            _repoUrls.Add(repoUrl);
            await SaveReposAsync();
        }

        public bool RemoveRepo(string repoUrl)
        {
            const string officialRepoUrl = "https://raw.githubusercontent.com/marbou92/Musicefy-Extensions/main/repo.json";

            if (string.Equals(repoUrl, officialRepoUrl, StringComparison.OrdinalIgnoreCase))
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

            var extDir = Path.Combine(_extensionsDir, extension.Id);
            if (!Directory.Exists(extDir))
                Directory.CreateDirectory(extDir);

            var dllPath = Path.Combine(extDir, $"{extension.Id}.dll");

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
        }

        public async Task UninstallExtensionAsync(string extensionId)
        {
            var extDir = Path.Combine(_extensionsDir, extensionId);
            if (Directory.Exists(extDir))
            {
                Directory.Delete(extDir, true);
            }

            _installed.RemoveAll(e => e.Id == extensionId);
            _extensionProviders.RemoveAll(p =>
                _installed.All(i => i.SourceType != p.SourceType));

            await Task.CompletedTask;
        }

        public void MarkBuiltInAsInstalled(string sourceType, string displayName)
        {
            var id = $"builtin_{sourceType.ToLower()}";
            if (_installed.Any(e => e.Id == id))
                return;

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
        }

        public void MarkBuiltInAsUninstalled(string extensionId)
        {
            if (!extensionId.StartsWith("builtin_"))
                return;

            var extDir = Path.Combine(_extensionsDir, extensionId);
            if (Directory.Exists(extDir))
                Directory.Delete(extDir, true);

            _installed.RemoveAll(e => e.Id == extensionId);
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

        private void LoadRepos()
        {
            const string officialRepoUrl = "https://raw.githubusercontent.com/marbou92/Musicefy-Extensions/main/repo.json";
            try
            {
                if (!File.Exists(_reposFilePath))
                {
                    _repoUrls.Add(officialRepoUrl);
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

            if (!_repoUrls.Contains(officialRepoUrl, StringComparer.OrdinalIgnoreCase))
                _repoUrls.Add(officialRepoUrl);
        }

        private void SaveRepos()
        {
            const string officialRepoUrl = "https://raw.githubusercontent.com/marbou92/Musicefy-Extensions/main/repo.json";
            try
            {
                var urlsToSave = _repoUrls
                    .Where(u => !string.Equals(u, officialRepoUrl, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var json = JsonConvert.SerializeObject(urlsToSave, Formatting.Indented);
                File.WriteAllText(_reposFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save repos: {ex.Message}");
            }
        }

        private async Task SaveReposAsync()
        {
            SaveRepos();
            await Task.CompletedTask;
        }
    }
}

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.Properties;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Musicefy.ViewModels
{
    public class DiscoverSourceItem : ViewModelBase
    {
        private bool _isEnabled;
        public string SourceType { get; set; }
        public string DisplayName { get; set; }
        public string IconGlyph { get; set; }
        public string Description { get; set; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }
    }

    public class DiscoverSettingsViewModel : ViewModelBase
    {
        public ObservableCollection<DiscoverSourceItem> Sources { get; } = new ObservableCollection<DiscoverSourceItem>();

        public ICommand SaveCommand { get; }
        public ICommand ResetCommand { get; }

        public DiscoverSettingsViewModel()
        {
            Load();
            SaveCommand = new RelayCommand(_ => Save());
            ResetCommand = new RelayCommand(_ => ResetDefaults());
        }

        public void Load()
        {
            Sources.Clear();

            var providers = App.Services.GetServices<IMusicSourceProvider>();
            var installed = App.Services.GetService<IExtensionManager>()?.GetInstalledExtensions() ?? new List<ExtensionManifest>();
            var installedTypes = new HashSet<string>(installed.Select(e => e.SourceType));

            var enabledExtra = GetEnabledExtraSources();

            foreach (var provider in providers)
            {
                bool isInstalled = provider.SourceType == "Local" || installedTypes.Contains(provider.SourceType) ||
                    _providersAlwaysShow.Contains(provider.SourceType);

                if (!isInstalled) continue;

                bool enabled = provider.SourceType switch
                {
                    "Local" => Settings.Default.DiscoverLibrary,
                    "YouTube" => Settings.Default.DiscoverYouTube,
                    "Subsonic" => Settings.Default.DiscoverSubsonic,
                    _ => enabledExtra.Contains(provider.SourceType)
                };

                Sources.Add(new DiscoverSourceItem
                {
                    SourceType = provider.SourceType,
                    DisplayName = provider.DisplayName,
                    IconGlyph = provider.IconGlyph,
                    Description = provider.Description,
                    IsEnabled = enabled
                });
            }

            // Also show providers from extension manager that aren't built-in
            foreach (var extProvider in App.Services.GetService<IExtensionManager>()?.ExtensionProviders ?? new List<IMusicSourceProvider>())
            {
                if (Sources.Any(s => s.SourceType == extProvider.SourceType)) continue;

                Sources.Add(new DiscoverSourceItem
                {
                    SourceType = extProvider.SourceType,
                    DisplayName = extProvider.DisplayName,
                    IconGlyph = extProvider.IconGlyph,
                    Description = extProvider.Description,
                    IsEnabled = enabledExtra.Contains(extProvider.SourceType)
                });
            }
        }

        public void Save()
        {
            var extraEnabled = new List<string>();

            foreach (var source in Sources)
            {
                switch (source.SourceType)
                {
                    case "Local":
                        Settings.Default.DiscoverLibrary = source.IsEnabled;
                        break;
                    case "YouTube":
                        Settings.Default.DiscoverYouTube = source.IsEnabled;
                        break;
                    case "Subsonic":
                        Settings.Default.DiscoverSubsonic = source.IsEnabled;
                        break;
                    default:
                        if (source.IsEnabled)
                            extraEnabled.Add(source.SourceType);
                        break;
                }
            }

            Settings.Default.DiscoverExtraSources = JsonConvert.SerializeObject(extraEnabled);
            Settings.Default.Save();
        }

        public void Cancel()
        {
            Load();
        }

        private void ResetDefaults()
        {
            foreach (var source in Sources)
                source.IsEnabled = true;
        }

        private static HashSet<string> GetEnabledExtraSources()
        {
            var json = Settings.Default.DiscoverExtraSources;
            if (string.IsNullOrEmpty(json)) return new HashSet<string>();
            try { var list = JsonConvert.DeserializeObject<List<string>>(json); return list != null ? new HashSet<string>(list) : new HashSet<string>(); }
            catch { return new HashSet<string>(); }
        }

        private static readonly HashSet<string> _providersAlwaysShow = new HashSet<string>
        {
            "YouTube", "Subsonic"
        };
    }
}

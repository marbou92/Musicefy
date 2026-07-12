using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core;
using Musicefy.Core.Configuration;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Library;
using Musicefy.Core.Models;
using Musicefy.Core.Services;
using Musicefy.Core.Theme;
using Musicefy.Services;
using Musicefy.ViewModels;
using Musicefy.Views;

namespace Musicefy
{
    public partial class App : Application
    {
        private IServiceProvider _serviceProvider;

        public static IServiceProvider Services => ((App)Current)._serviceProvider;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ConfigureServices();

            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            try
            {
                // Migrate old "Dark|Default" + PureBlackMode settings to the new
                // AppTheme + ThemeMode model before loading the theme.
                MigrateThemeSettings();

                var (appTheme, themeMode) = ThemeManager.LoadPreferences();

                // Apply the saved theme. This sets ALL brushes (accent + surface) from the
                // pre-authored MusicefyColorScheme for the chosen AppTheme.
                ThemeManager.ApplyTheme(appTheme, themeMode);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ThemeManager.AnimateWindowsFade();
                }), DispatcherPriority.ApplicationIdle);

                ThemeManager.StartSystemThemeWatcher();
                _ = InitializeLibraryAsync();
            }
            catch (Exception ex)
            {
                string logPath = LogException(ex);
                MessageBox.Show($"Theme load error: {ex.Message}\n\nCrash log written to:\n{logPath}",
                    "Musicefy Theme Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// One-time migration from the old fused "Theme" string ("Dark|Default")
        /// and PureBlackMode boolean to the new separate AppTheme + ThemeMode settings.
        /// Runs only when the new AppTheme setting is empty (first launch after upgrade).
        /// </summary>
        private static void MigrateThemeSettings()
        {
            // If AppTheme is already set, migration has already run
            if (!string.IsNullOrEmpty(Musicefy.Properties.Settings.Default.AppTheme))
                return;

            var old = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";
            var parts = old.Split('|');
            var modeStr = parts.Length > 0 ? parts[0] : "Dark";
            var palStr  = parts.Length > 1 ? parts[1] : "Default";

            // Map old PureBlackMode → ThemeMode.Amoled
            ThemeMode mode = Musicefy.Properties.Settings.Default.PureBlackMode && modeStr == "Dark"
                ? ThemeMode.Amoled
                : modeStr switch
                {
                    "Light"  => ThemeMode.Light,
                    "System" => ThemeMode.System,
                    _        => ThemeMode.Dark,
                };

            // Map old SeedPalette names to new AppTheme values
            AppTheme theme = ThemeManager.MapOldPaletteName(palStr);

            // If DynamicColorsEnabled was true, map to AppTheme.Dynamic
            if (Musicefy.Properties.Settings.Default.DynamicColorsEnabled && palStr.Equals("Default", StringComparison.OrdinalIgnoreCase))
                theme = AppTheme.Dynamic;

            Musicefy.Properties.Settings.Default.AppTheme  = theme.ToString();
            Musicefy.Properties.Settings.Default.ThemeMode = mode.ToString();
            Musicefy.Properties.Settings.Default.Save();
        }

        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<ILibraryService>(sp =>
                new LibraryScanner(DatabaseConfig.ConnectionString));

            services.AddSingleton<IQueueManager, QueueManager>();
            services.AddSingleton<IDownloadService, DownloadServiceImpl>();
            services.AddSingleton<IFolderDataProvider>(sp =>
                new SqliteFolderDataProvider(DatabaseConfig.ConnectionString));

            // Built-in music source providers: Local + YouTube only.
            // Subsonic and the extension system have been removed.
            // LocalSourceProvider needs ILibraryService and IServiceProvider (for lazy ArtistAlbumService resolution)
            // to avoid circular dependency: ArtistAlbumService -> IStreamingSourceManager -> IMusicSourceProvider -> LocalSourceProvider
            services.AddSingleton<IMusicSourceProvider>(sp => new LocalSourceProvider(
                sp.GetService<ILibraryService>(),
                sp));
            services.AddSingleton<IMusicSourceProvider, YouTubeSourceProvider>();

            services.AddSingleton<IStreamingSourceManager>(sp =>
                new StreamingSourceManagerImpl(sp, sp.GetServices<IMusicSourceProvider>()));

            // Sprint 4: LrcLib lyrics service (free, no API key)
            services.AddSingleton<Musicefy.Core.Services.LrcLibService>();

            // Sprint 4: SponsorBlock service (free, no API key)
            services.AddSingleton<Musicefy.Core.Services.SponsorBlockService>();

            // Sprint 5: History + Stats service
            services.AddSingleton<Musicefy.Core.Services.HistoryService>(sp =>
                new Musicefy.Core.Services.HistoryService(DatabaseConfig.ConnectionString));

            // Sprint 6: Backup & Restore service
            services.AddSingleton<Musicefy.Core.Services.BackupService>();

            // Sprint 7: AI Lyrics Translation
            services.AddSingleton<Musicefy.Core.Services.AiTranslationService>();

            // Sprint 7: Last.fm scrobbling
            services.AddSingleton<Musicefy.Core.Services.LastFmService>();

            // Sprint 7: Discord Rich Presence
            services.AddSingleton<Musicefy.Core.Services.DiscordRpcService>();

            // Sprint 7: YouTube Mood/Genres/Charts browsing
            services.AddSingleton<Musicefy.Core.Services.YouTubeBrowseService>(sp =>
                new Musicefy.Core.Services.YouTubeBrowseService(
                    sp.GetService<IStreamingSourceManager>()));

            services.AddSingleton<PlaybackService>(sp => new PlaybackService(
                sp.GetService<IQueueManager>(),
                sp.GetService<IStreamingSourceManager>(),
                sp.GetService<ILibraryService>(),
                sp));
            services.AddSingleton<IAudioPlayer>(sp => sp.GetService<PlaybackService>());

            services.AddSingleton<NavigationService>();

            // Phase 3: Health Check and Browse services
            services.AddSingleton<IHealthCheckService, HealthCheckService>();
            services.AddSingleton<IBrowseService, UnifiedBrowseService>();

            // Phase 2: Search History service
            services.AddSingleton<ISearchHistoryService>(sp =>
                new SearchHistoryService(DatabaseConfig.ConnectionString));

            // Phase 1: Home ViewModel (singleton for state persistence across navigation)
            services.AddSingleton<HomeViewModel>();

            // Pages are Singleton so they're created once and reused.
            // Transient caused lag — every navigation created a new page
            // from scratch (re-parsing XAML, re-loading data, re-decoding images).
            services.AddSingleton<HomeControl>();
            services.AddSingleton<SearchControl>();
            services.AddSingleton<LibraryControl>();

            services.AddSingleton<MainViewModel>();
            services.AddSingleton<SearchViewModel>();
            services.AddSingleton<LibraryViewModel>();
            services.AddSingleton<AppearanceSettingsViewModel>();
            services.AddSingleton<DownloadsSettingsViewModel>();
            // Sprint 5: History + Stats ViewModels
            services.AddTransient<HistoryViewModel>();
            services.AddTransient<StatsViewModel>();
            // Sprint 6: Backup & Restore ViewModel
            services.AddTransient<BackupRestoreViewModel>();
            // Sprint 7: Integrations ViewModel (AI translation, Last.fm, Discord)
            services.AddTransient<IntegrationsViewModel>();
            // Sprint 7: Browse ViewModel (Mood/Genres/Charts)
            services.AddTransient<BrowseViewModel>();

            services.AddSingleton<ArtistAlbumService>();
            services.AddTransient<ArtistViewModel>();
            services.AddTransient<AlbumViewModel>();

            // Phase 5: Playlist ViewModel
            services.AddTransient<PlaylistViewModel>();

            _serviceProvider = services.BuildServiceProvider();
        }

        private async Task InitializeLibraryAsync()
        {
            try
            {
                var libraryService = _serviceProvider.GetService<ILibraryService>();
                if (libraryService != null)
                    await libraryService.EnsureSchemaAsync();

                // Sprint 4: Auto-provision Local + YouTube sources on first launch.
                // No more "Add Source" dialog — these are always available.
                EnsureDefaultSources();

                // Phase 6: Restore queue state from previous session
                var playback = _serviceProvider.GetService<PlaybackService>();
                if (playback != null)
                    await playback.RestoreQueueStateAsync();
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }

        /// <summary>
        /// Sprint 4: Ensures the Local and YouTube sources always exist in
        /// sources.json. No user action required — these are the app's two
        /// built-in providers and are auto-provisioned on first launch.
        ///
        /// Local source uses the first folder from Settings.LocalMusicFolders,
        /// or the user's Music folder as a default.
        /// YouTube source uses Settings.YouTubeApiKey + YouTubeCookie if set.
        /// </summary>
        private void EnsureDefaultSources()
        {
            try
            {
                var sourceManager = _serviceProvider.GetService<IStreamingSourceManager>();
                if (sourceManager == null) return;

                var existing = sourceManager.Sources;

                // ── Ensure Local source exists ───────────────────────────────
                if (!existing.Any(s => string.Equals(s.Type, SourceTypes.Local, StringComparison.OrdinalIgnoreCase)))
                {
                    var folders = Musicefy.Properties.Settings.Default.LocalMusicFolders?
                        .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(f => f.Trim())
                        .Where(f => !string.IsNullOrEmpty(f) && System.IO.Directory.Exists(f))
                        .ToList() ?? new List<string>();

                    // Default to the user's Music folder if no folders configured
                    if (folders.Count == 0)
                    {
                        var musicFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                        if (!string.IsNullOrEmpty(musicFolder) && System.IO.Directory.Exists(musicFolder))
                            folders.Add(musicFolder);
                    }

                    if (folders.Count > 0)
                    {
                        var localSource = new StreamingSource
                        {
                            Name = "Local Music",
                            Type = SourceTypes.Local,
                            IsConnected = true,
                            Configuration = new Dictionary<string, string>
                            {
                                ["folderPath"] = folders[0]
                            },
                            Url = folders[0]
                        };
                        try { sourceManager.AddSourceAsync(localSource).GetAwaiter().GetResult(); }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[App] Failed to auto-create Local source: {ex.Message}"); }
                    }
                }

                // ── Ensure YouTube source exists (if enabled) ────────────────
                if (Musicefy.Properties.Settings.Default.YouTubeEnabled &&
                    !existing.Any(s => string.Equals(s.Type, SourceTypes.YouTube, StringComparison.OrdinalIgnoreCase)))
                {
                    var ytSource = new StreamingSource
                    {
                        Name = "YouTube Music",
                        Type = SourceTypes.YouTube,
                        IsConnected = true,
                        Configuration = new Dictionary<string, string>
                        {
                            ["apiKey"] = Musicefy.Properties.Settings.Default.YouTubeApiKey ?? "",
                            ["cookie"] = Musicefy.Properties.Settings.Default.YouTubeCookie ?? "",
                            ["audioQuality"] = Musicefy.Properties.Settings.Default.YouTubeAudioQuality ?? "opus"
                        }
                    };
                    try { sourceManager.AddSourceAsync(ytSource).GetAwaiter().GetResult(); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[App] Failed to auto-create YouTube source: {ex.Message}"); }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] EnsureDefaultSources failed: {ex.Message}");
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string logPath = LogException(e.Exception);
            MessageBox.Show($"Unexpected error: {e.Exception.GetType().Name}\n{e.Exception.Message}\n\nCrash log written to:\n{logPath}",
                "Musicefy Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                string logPath = LogException(ex);
                try
                {
                    MessageBox.Show($"Fatal error: {ex.GetType().Name}\n{ex.Message}\n\nCrash log written to:\n{logPath}",
                        "Musicefy Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { }
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            string logPath = LogException(e.Exception);
            e.SetObserved();
            try
            {
                MessageBox.Show($"Unhandled task error: {e.Exception.GetType().Name}\n{e.Exception.Message}\n\nCrash log written to:\n{logPath}",
                    "Musicefy Task Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        }

        private string LogException(Exception ex)
        {
            string[] candidates =
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Musicefy", "crash.log"),
                Path.Combine(Path.GetTempPath(), "Musicefy", "crash.log")
            };

            foreach (var logPath in candidates)
            {
                try
                {
                    string dir = Path.GetDirectoryName(logPath);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.AppendAllText(logPath, $"[{DateTime.Now}] {ex}\n---------------------------------\n");
                    return logPath;
                }
                catch { }
            }

            return candidates[0];
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Phase 6: Save queue state before exit.
            // Use GetAwaiter().GetResult() instead of .Wait() to avoid
            // AggregateException wrapping. Wrap in try-catch so a hang
            // or failure doesn't prevent the app from exiting.
            try
            {
                var playback = _serviceProvider.GetService<PlaybackService>();
                playback?.SaveQueueStateAsync().GetAwaiter().GetResult();
            }
            catch { }

            try
            {
                if (_serviceProvider is IDisposable disposable)
                    disposable.Dispose();
            }
            catch { }

            base.OnExit(e);

            // Force-kill the process if anything is still alive (NAudio
            // threads, image cache, etc. can keep the process running).
            // This guarantees the app disappears from Task Manager.
            Environment.Exit(0);
        }
    }
}

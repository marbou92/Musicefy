using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Configuration;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Library;
using Musicefy.Core.Services;
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
                string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";

                var parts = savedTheme.Split('|');
                string mode = parts.Length > 0 ? parts[0] : "Dark";
                string palette = parts.Length > 1 ? parts[1] : "Default";

                // Apply the saved theme. This sets ALL brushes (accent + surface) from the
                // seed palette's DynamicScheme. DO NOT call ApplyDynamicColors here — that
                // would immediately overwrite the surfaces with an album-art-tinted scheme
                // even before any music has played, which is what caused the cyan/lavender
                // surfaces in light mode.
                ThemeManager.ApplyTheme(mode, palette);

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

        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<ILibraryService>(sp =>
                new LibraryScanner(DatabaseConfig.ConnectionString));

            services.AddSingleton<IQueueManager, QueueManager>();
            services.AddSingleton<IDownloadService, DownloadServiceImpl>();
            services.AddSingleton<IFolderDataProvider>(sp =>
                new SqliteFolderDataProvider(DatabaseConfig.ConnectionString));

            services.AddSingleton<IExtensionManager, ExtensionManagerImpl>();

            services.AddSingleton<IMusicSourceProvider, SubsonicSourceProvider>();
            services.AddSingleton<IMusicSourceProvider, LocalSourceProvider>();
            services.AddSingleton<IMusicSourceProvider, YouTubeSourceProvider>();

            services.AddSingleton<IStreamingSourceManager>(sp =>
                new StreamingSourceManagerImpl(sp, sp.GetServices<IMusicSourceProvider>()));

            services.AddSingleton<PlaybackService>();
            services.AddSingleton<IAudioPlayer>(sp => sp.GetService<PlaybackService>());

            services.AddSingleton<NavigationService>();

            services.AddTransient<HomeControl>();
            services.AddTransient<SearchControl>();
            services.AddTransient<LibraryControl>();

            services.AddSingleton<MainViewModel>();
            services.AddSingleton<SearchViewModel>();
            services.AddSingleton<LibraryViewModel>();
            services.AddSingleton<AppearanceSettingsViewModel>();
            services.AddSingleton<DownloadsSettingsViewModel>();
            services.AddTransient<ExtensionsSettingsViewModel>();
            services.AddTransient<RepositoriesSettingsViewModel>();
            services.AddTransient<DiscoverSettingsViewModel>();
            services.AddTransient<SourcesSettingsViewModel>();

            services.AddSingleton<ArtistAlbumService>();
            services.AddTransient<ArtistViewModel>();
            services.AddTransient<AlbumViewModel>();

            _serviceProvider = services.BuildServiceProvider();

            var extManager = _serviceProvider.GetService<IExtensionManager>();
            extManager?.LoadExtensions();
        }

        private async Task InitializeLibraryAsync()
        {
            try
            {
                var libraryService = _serviceProvider.GetService<ILibraryService>();
                if (libraryService != null)
                    await libraryService.EnsureSchemaAsync();
            }
            catch (Exception ex)
            {
                LogException(ex);
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
            if (_serviceProvider is IDisposable disposable)
                disposable.Dispose();

            base.OnExit(e);
        }
    }
}

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

            try
            {
                string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";
                bool isPureBlack = Musicefy.Properties.Settings.Default.PureBlackMode;

                var parts = savedTheme.Split('|');
                string mode = parts.Length > 0 ? parts[0] : "Dark";
                string palette = parts.Length > 1 ? parts[1] : "Default";

                if (mode.Equals("Dark", StringComparison.OrdinalIgnoreCase) && isPureBlack)
                    mode = "DarkPure";

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
                LogException(ex);
                MessageBox.Show($"Theme load error: {ex.Message}\nSee crash.log for details.",
                    "Musicefy Theme Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            // Core services via interfaces
            services.AddSingleton<ILibraryService>(sp =>
                new LibraryScanner(DatabaseConfig.ConnectionString));

            services.AddSingleton<IQueueManager, QueueManager>();
            services.AddSingleton<IDownloadService, DownloadServiceImpl>();
            services.AddSingleton<IFolderDataProvider>(sp =>
                new SqliteFolderDataProvider(DatabaseConfig.ConnectionString));

            // Extension manager (loads extension DLLs from disk)
            services.AddSingleton<IExtensionManager, ExtensionManagerImpl>();

            // Built-in music source providers
            services.AddSingleton<IMusicSourceProvider, SubsonicSourceProvider>();
            services.AddSingleton<IMusicSourceProvider, LocalSourceProvider>();
            services.AddSingleton<IMusicSourceProvider, YouTubeSourceProvider>();

            // Streaming source manager — uses provider registry
            services.AddSingleton<IStreamingSourceManager>(sp =>
                new StreamingSourceManagerImpl(sp, sp.GetServices<IMusicSourceProvider>()));

            // PlaybackService — single instance shared across all ViewModels
            services.AddSingleton<PlaybackService>();
            services.AddSingleton<IAudioPlayer>(sp => sp.GetService<PlaybackService>());

            // Navigation service (decouples ViewModel from View creation)
            services.AddSingleton<NavigationService>();

            // Views registered for DI resolution
            services.AddTransient<HomeControl>();
            services.AddTransient<SearchControl>();
            services.AddTransient<LibraryControl>();

            // ViewModels (singleton so state persists across navigation)
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<SearchViewModel>();
            services.AddSingleton<LibraryViewModel>();
            services.AddSingleton<AppearanceSettingsViewModel>();
            services.AddSingleton<DownloadsSettingsViewModel>();
            services.AddTransient<ExtensionsSettingsViewModel>();
            services.AddTransient<RepositoriesSettingsViewModel>();
            services.AddTransient<DiscoverSettingsViewModel>();
            services.AddTransient<SourcesSettingsViewModel>();

            _serviceProvider = services.BuildServiceProvider();

            // Load extension DLLs after container is built
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
            LogException(e.Exception);
            MessageBox.Show($"Unexpected error: {e.Exception.Message}\nSee crash.log for details.",
                "Musicefy Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                LogException(ex);
        }

        private void LogException(Exception ex)
        {
            try
            {
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Musicefy");
                if (!Directory.Exists(appDataPath))
                    Directory.CreateDirectory(appDataPath);
                string logPath = Path.Combine(appDataPath, "crash.log");
                File.AppendAllText(logPath, $"[{DateTime.Now}] {ex}\n---------------------------------\n");
            }
            catch (Exception logEx)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Failed to write crash log: {logEx.Message}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_serviceProvider is IDisposable disposable)
                disposable.Dispose();

            base.OnExit(e);
        }
    }
}

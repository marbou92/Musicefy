using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Musicefy.Services;

namespace Musicefy
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            try
            {
                string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default.Dark";
                ThemeManager.ApplyThemeFromString(savedTheme);

                // Run fade AFTER UI is ready
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ThemeManager.AnimateWindowsFade();
                }), DispatcherPriority.ApplicationIdle);

                // Start watching for system theme changes
                ThemeManager.StartSystemThemeWatcher();
            }
            catch (Exception ex)
            {
                LogException(ex);
                MessageBox.Show($"Theme load error: {ex.Message}\nSee crash.log for details.",
                                "Musicefy Theme Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);
            MessageBox.Show($"Unexpected error: {e.Exception.Message}\nSee crash.log for details.",
                            "Musicefy Crash",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogException(ex);
            }
        }

        private void LogException(Exception ex)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                File.AppendAllText(logPath,
                    $"[{DateTime.Now}] {ex}\n---------------------------------\n");
            }
            catch { }
        }
    }
}

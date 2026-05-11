using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Musicefy.Services; // <-- Needed for ThemeManager

namespace Musicefy
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Global exception handlers
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Apply saved theme at startup
            try
            {
                string savedTheme = Musicefy.Properties.Settings.Default.Theme;
                if (!string.IsNullOrEmpty(savedTheme))
                {
                    ThemeManager.ApplyTheme(savedTheme);
                }
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
            e.Handled = true; // prevent silent crash
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
            catch
            {
                // ignore logging errors
            }
        }
    }
}

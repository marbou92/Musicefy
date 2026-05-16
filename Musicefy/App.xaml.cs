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

            // Bind systemic global fail-safes to handle unhandled runtime vectors gracefully
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            try
            {
                // 1. Unpack your saved user settings token matrix ("Mode|Palette" or default fallback)
                string savedTheme = Musicefy.Properties.Settings.Default.Theme ?? "Dark|Default";
                bool isPureBlack = Musicefy.Properties.Settings.Default.PureBlackMode;

                var parts = savedTheme.Split('|');
                string mode = parts.Length > 0 ? parts[0] : "Dark";
                string palette = parts.Length > 1 ? parts[1] : "Default";

                // 2. Force evaluate PureBlack overrides immediately to ensure correct startup dictionary mapping
                if (mode.Equals("Dark", StringComparison.OrdinalIgnoreCase) && isPureBlack)
                {
                    mode = "DarkPure";
                }

                // 3. Rehydrate and apply the saved theme state directly to the global application resources dictionary
                ThemeManager.ApplyTheme(mode, palette);

                // 4. Initialize fade window transitions once the underlying device redraw handles settle idle
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ThemeManager.AnimateWindowsFade();
                }), DispatcherPriority.ApplicationIdle);

                // 5. Engage background subsystem watcher routines to map live OS theme modifications
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
                File.AppendAllText(logPath, $"[{DateTime.Now}] {ex}\n---------------------------------\n");
            }
            catch
            {
                // Fail silently to prevent cascade logging deadlocks
            }
        }
    }
}

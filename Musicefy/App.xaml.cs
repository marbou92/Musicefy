using System.Windows;
using Musicefy.Services;

namespace Musicefy
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ThemeManager.LoadSavedTheme();
        }

        public static void ApplyTheme(string themeName)
        {
            ThemeManager.ApplyTheme(themeName);
        }
    }
}

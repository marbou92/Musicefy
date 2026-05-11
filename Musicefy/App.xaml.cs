using System.Windows;
using Musicefy.Services;

namespace Musicefy
{
    public partial class App : Application
    {
        public static void ApplyTheme(string themeName)
        {
            ThemeManager.ApplyTheme(themeName);
        }
    }
}

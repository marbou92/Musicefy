using System;
using System.Windows;
using Musicefy.Services;

namespace Musicefy
{
    public partial class App : Application
    {
        // Forward ApplyTheme calls to ThemeManager
        public static void ApplyTheme(string themeName)
        {
            ThemeManager.ApplyTheme(themeName);
        }
    }
}

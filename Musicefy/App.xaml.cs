using System;
using System.Windows;
using Musicefy.Services; // ThemeManager

namespace Musicefy
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Load saved theme or default
            ThemeManager.LoadSavedTheme();

            // Future: Initialize logging, analytics, or dependency injection here
        }
    }
}

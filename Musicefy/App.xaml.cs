using System;
using System.Windows;

namespace Musicefy
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Load user preferences (theme, etc.)
            string savedTheme = Properties.Settings.Default.Theme;
            if (!string.IsNullOrEmpty(savedTheme))
            {
                ApplyTheme(savedTheme);
            }

            // Future: Initialize logging, analytics, or dependency injection here
        }

        public static void ApplyTheme(string themeName)
        {
            string themePath = themeName switch
            {
                "Light" => "Themes/Light.xaml",
                "Dark" => "Themes/Dark.xaml",
                _ => "Themes/Dark.xaml"
            };

            var dict = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };
            Current.Resources.MergedDictionaries.Clear();
            Current.Resources.MergedDictionaries.Add(dict);

            // Persist choice
            Properties.Settings.Default.Theme = themeName;
            Properties.Settings.Default.Save();
        }
    }
}

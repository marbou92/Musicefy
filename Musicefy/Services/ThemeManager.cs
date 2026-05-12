using System.Windows;

namespace Musicefy.Services
{
    public static class ThemeManager
    {
        public static void ApplyTheme(string mode, string palette)
        {
            Application.Current.Resources.MergedDictionaries.Clear();

            // Always include base styles
            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new System.Uri("/Themes/Base.xaml", System.UriKind.Relative) });

            // Mode (Light/Dark)
            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new System.Uri($"/Themes/Modes/{mode}.xaml", System.UriKind.Relative) });

            // Palette (Default/Catppuccin/GreenApple/Lavender)
            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary { Source = new System.Uri($"/Themes/Palettes/{palette}.xaml", System.UriKind.Relative) });
        }
    }
}

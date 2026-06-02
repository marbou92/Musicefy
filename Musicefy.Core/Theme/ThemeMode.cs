namespace Musicefy.Core.Theme
{
    /// <summary>
    /// Brightness mode — entirely separate from the palette choice (AppTheme).
    /// Mirrors Aniyomi/Mihon's ThemeMode model.
    /// </summary>
    public enum ThemeMode
    {
        /// <summary>Follow the Windows light/dark registry key.</summary>
        System,
        /// <summary>Force light mode.</summary>
        Light,
        /// <summary>Force dark mode.</summary>
        Dark,
        /// <summary>Dark mode with pure-black (#000000) surfaces (AMOLED).</summary>
        Amoled,
    }
}

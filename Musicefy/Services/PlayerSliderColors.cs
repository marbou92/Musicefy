using System.Windows.Media;

namespace Musicefy.Services
{
    /// <summary>
    /// Ported from Echo Music's PlayerSliderColors.kt.
    /// Returns the appropriate inactive track color based on the
    /// player background style and theme mode.
    /// </summary>
    public static class PlayerSliderColors
    {
        /// <summary>
        /// Returns the inactive track color for the player slider.
        /// When the player background is Gradient/Glow/Coloring,
        /// the inactive color is White at 40% opacity (for visibility
        /// over colorful backgrounds). Otherwise it uses Outline at 40%.
        /// </summary>
        public static Color GetInactiveColor(string playerBackgroundStyle, bool isDarkTheme)
        {
            switch (playerBackgroundStyle?.ToUpperInvariant())
            {
                case "GRADIENT":
                case "COLORING":
                case "GLOW":
                    return Color.FromArgb(102, 255, 255, 255); // White at 40%

                default:
                    if (isDarkTheme)
                        return Color.FromArgb(102, 255, 255, 255); // Outline at ~40% in dark
                    else
                        return Color.FromArgb(77, 0, 0, 0); // OnSurface at ~30% in light
            }
        }

        /// <summary>
        /// Returns the active track color (usually the Primary/accent color).
        /// </summary>
        public static Color GetActiveColor(Brush primaryBrush)
        {
            if (primaryBrush is SolidColorBrush scb)
                return scb.Color;
            return Colors.White;
        }
    }
}

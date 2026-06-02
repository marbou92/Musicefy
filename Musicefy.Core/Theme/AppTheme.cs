namespace Musicefy.Core.Theme
{
    /// <summary>
    /// Named, pre-defined palette — mirrors Aniyomi/Mihon's AppTheme model.
    /// Each entry provides two hand-crafted <see cref="MusicefyColorScheme"/> objects
    /// (one for light, one for dark) with all hex values authored directly — no HCT
    /// math at runtime. The enum name (not ordinal) is persisted to settings, so
    /// reordering is safe.
    /// </summary>
    public enum AppTheme
    {
        Default,
        GreenApple,
        Lavender,
        StrawberryDaiquiri,
        MidnightDusk,
        Tako,
        TealTurquoise,
        TidalWave,
        CottonCandy,
        Cloudflare,
        Doom,
        Mocha,
        Sapphire,
        Nord,
        YinAndYang,
        Yotsuba,
        Monochrome,
        /// <summary>
        /// Album-art dynamic theming (equivalent to Aniyomi's MONET).
        /// Handled specially by ThemeManager — uses HCT extraction from album art.
        /// </summary>
        Dynamic,
    }
}

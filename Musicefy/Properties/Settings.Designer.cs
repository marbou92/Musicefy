namespace Musicefy.Properties {

    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.0.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {

        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));

        public static Settings Default => defaultInstance;

        // ── NEW: Aniyomi-style separate AppTheme + ThemeMode settings ──────────
        // Replaces the old fused "Theme" string ("Dark|Default") and PureBlackMode boolean.

        /// <summary>
        /// The named palette (AppTheme enum value name), e.g. "Default", "GreenApple", "Lavender".
        /// When this is "Dynamic", album-art color extraction is active.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Default")]
        public string AppTheme
        {
            get => ((string)(this["AppTheme"]));
            set => this["AppTheme"] = value;
        }

        /// <summary>
        /// The brightness mode (ThemeMode enum value name):
        /// "System", "Light", "Dark", or "Amoled".
        /// Amoled replaces the old PureBlackMode boolean.
        /// </summary>
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Dark")]
        public string ThemeMode
        {
            get => ((string)(this["ThemeMode"]));
            set => this["ThemeMode"] = value;
        }

        // ── LEGACY: kept for migration only ────────────────────────────────────
        // The old "Theme" string of form "Dark|Default" is still present so that
        // MigrateThemeSettings() in App.xaml.cs can read it on first launch after
        // upgrade. It is no longer written to after migration.

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Dark|Default")]
        public string Theme
        {
            get => ((string)(this["Theme"]));
            set => this["Theme"] = value;
        }

        // PureBlackMode kept for migration; no longer used after migration.
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool PureBlackMode
        {
            get => ((bool)(this["PureBlackMode"]));
            set => this["PureBlackMode"] = value;
        }

        // PaletteStyle kept for migration; no longer used after migration.
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("TonalSpot")]
        public string PaletteStyle
        {
            get => ((string)(this["PaletteStyle"]));
            set => this["PaletteStyle"] = value;
        }

        // ExactPalette kept for migration; no longer used after migration.
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool ExactPalette
        {
            get => ((bool)(this["ExactPalette"]));
            set => this["ExactPalette"] = value;
        }

        // DynamicColorsEnabled: now implied by AppTheme == "Dynamic",
        // but kept for migration.
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool DynamicColorsEnabled
        {
            get => ((bool)(this["DynamicColorsEnabled"]));
            set => this["DynamicColorsEnabled"] = value;
        }

        // Accent color (for appearance settings)
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Default")]
        public string AccentColor
        {
            get => ((string)(this["AccentColor"]));
            set => this["AccentColor"] = value;
        }

        // Date format selection
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("MM/dd/yyyy")]
        public string DateFormat
        {
            get => ((string)(this["DateFormat"]));
            set => this["DateFormat"] = value;
        }

        // Downloads path
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string DownloadsPath
        {
            get => ((string)(this["DownloadsPath"]));
            set => this["DownloadsPath"] = value;
        }

        // Auto-clear cache
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AutoClearCache
        {
            get => ((bool)(this["AutoClearCache"]));
            set => this["AutoClearCache"] = value;
        }

        // Limit per-file download size (500 MB toggle)
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool LimitDownloadSize
        {
            get => ((bool)(this["LimitDownloadSize"]));
            set => this["LimitDownloadSize"] = value;
        }

        // Global cache limit (default 2 GB in bytes)
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("2147483648")]
        public long GlobalCacheLimit
        {
            get => ((long)(this["GlobalCacheLimit"]));
            set => this["GlobalCacheLimit"] = value;
        }

        // Cache warning threshold (default 400 MB in bytes)
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("419430400")]
        public long CacheWarningThreshold
        {
            get => ((long)(this["CacheWarningThreshold"]));
            set => this["CacheWarningThreshold"] = value;
        }

        // Player background gradient style (DEFAULT, GRADIENT, COLORING, GLOW)
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("GRADIENT")]
        public string PlayerBackgroundStyle
        {
            get => ((string)(this["PlayerBackgroundStyle"]));
            set => this["PlayerBackgroundStyle"] = value;
        }

        // Discover: show local library on home
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool DiscoverLibrary
        {
            get => ((bool)(this["DiscoverLibrary"]));
            set => this["DiscoverLibrary"] = value;
        }

        // Discover: show YouTube content on home
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool DiscoverYouTube
        {
            get => ((bool)(this["DiscoverYouTube"]));
            set => this["DiscoverYouTube"] = value;
        }

        // Discover: show Subsonic content on home
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool DiscoverSubsonic
        {
            get => ((bool)(this["DiscoverSubsonic"]));
            set => this["DiscoverSubsonic"] = value;
        }

        // Discover: extra source types enabled (JSON array of source type names)
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("[]")]
        public string DiscoverExtraSources
        {
            get => ((string)(this["DiscoverExtraSources"]));
            set => this["DiscoverExtraSources"] = value;
        }

        // ── Sprint 4: Local music folders (semicolon-delimited) ──────────────
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string LocalMusicFolders
        {
            get => ((string)(this["LocalMusicFolders"]));
            set => this["LocalMusicFolders"] = value;
        }

        // ── Sprint 4: YouTube settings (always-on) ───────────────────────────
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string YouTubeApiKey
        {
            get => ((string)(this["YouTubeApiKey"]));
            set => this["YouTubeApiKey"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string YouTubeCookie
        {
            get => ((string)(this["YouTubeCookie"]));
            set => this["YouTubeCookie"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("opus")]
        public string YouTubeAudioQuality
        {
            get => ((string)(this["YouTubeAudioQuality"]));
            set => this["YouTubeAudioQuality"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool YouTubeEnabled
        {
            get => ((bool)(this["YouTubeEnabled"]));
            set => this["YouTubeEnabled"] = value;
        }

        // ── Sprint 4: SponsorBlock ───────────────────────────────────────────
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool SponsorBlockEnabled
        {
            get => ((bool)(this["SponsorBlockEnabled"]));
            set => this["SponsorBlockEnabled"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool SponsorBlockSkipSponsor
        {
            get => ((bool)(this["SponsorBlockSkipSponsor"]));
            set => this["SponsorBlockSkipSponsor"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool SponsorBlockSkipIntro
        {
            get => ((bool)(this["SponsorBlockSkipIntro"]));
            set => this["SponsorBlockSkipIntro"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool SponsorBlockSkipOutro
        {
            get => ((bool)(this["SponsorBlockSkipOutro"]));
            set => this["SponsorBlockSkipOutro"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool SponsorBlockSkipSelfPromo
        {
            get => ((bool)(this["SponsorBlockSkipSelfPromo"]));
            set => this["SponsorBlockSkipSelfPromo"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool SponsorBlockSkipInteraction
        {
            get => ((bool)(this["SponsorBlockSkipInteraction"]));
            set => this["SponsorBlockSkipInteraction"] = value;
        }

        // ── Sprint 4: Lyrics (LrcLib) ────────────────────────────────────────
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool LyricsEnabled
        {
            get => ((bool)(this["LyricsEnabled"]));
            set => this["LyricsEnabled"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("LrcLib")]
        public string LyricsProvider
        {
            get => ((string)(this["LyricsProvider"]));
            set => this["LyricsProvider"] = value;
        }

        // ── Sprint 5: Playback — Skip Silence + Crossfade ────────────────────
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool SkipSilenceEnabled
        {
            get => ((bool)(this["SkipSilenceEnabled"]));
            set => this["SkipSilenceEnabled"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("-40")]
        public int SkipSilenceThresholdDb
        {
            get => ((int)(this["SkipSilenceThresholdDb"]));
            set => this["SkipSilenceThresholdDb"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool CrossfadeEnabled
        {
            get => ((bool)(this["CrossfadeEnabled"]));
            set => this["CrossfadeEnabled"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("5")]
        public double CrossfadeDurationSeconds
        {
            get => ((double)(this["CrossfadeDurationSeconds"]));
            set => this["CrossfadeDurationSeconds"] = value;
        }

        // ── Sprint 6: Backup & Restore ───────────────────────────────────────
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string LastBackupPath
        {
            get => ((string)(this["LastBackupPath"]));
            set => this["LastBackupPath"] = value;
        }

        // ── Sprint 7: AI Lyrics Translation ──────────────────────────────────
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AiTranslationEnabled
        {
            get => ((bool)(this["AiTranslationEnabled"]));
            set => this["AiTranslationEnabled"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("OpenRouter")]
        public string AiTranslationProvider
        {
            get => ((string)(this["AiTranslationProvider"]));
            set => this["AiTranslationProvider"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string AiTranslationApiKey
        {
            get => ((string)(this["AiTranslationApiKey"]));
            set => this["AiTranslationApiKey"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("google/gemini-2.5-flash-lite")]
        public string AiTranslationModel
        {
            get => ((string)(this["AiTranslationModel"]));
            set => this["AiTranslationModel"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("en")]
        public string AiTranslationTargetLang
        {
            get => ((string)(this["AiTranslationTargetLang"]));
            set => this["AiTranslationTargetLang"] = value;
        }

        // ── Sprint 7: Last.fm Scrobbling ─────────────────────────────────────
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool LastFmEnabled
        {
            get => ((bool)(this["LastFmEnabled"]));
            set => this["LastFmEnabled"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string LastFmSessionKey
        {
            get => ((string)(this["LastFmSessionKey"]));
            set => this["LastFmSessionKey"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string LastFmUsername
        {
            get => ((string)(this["LastFmUsername"]));
            set => this["LastFmUsername"] = value;
        }

        // ── Sprint 7: Discord Rich Presence ──────────────────────────────────
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool DiscordRpcEnabled
        {
            get => ((bool)(this["DiscordRpcEnabled"]));
            set => this["DiscordRpcEnabled"] = value;
        }

        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("123456789012345678")]
        public string DiscordClientId
        {
            get => ((string)(this["DiscordClientId"]));
            set => this["DiscordClientId"] = value;
        }
    }
}

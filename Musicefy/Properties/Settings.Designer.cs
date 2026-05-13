namespace Musicefy.Properties {
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute(
        "Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "17.0.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default => defaultInstance;

        // Theme setting (mode|palette)
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Dark|Default")]
        public string Theme {
            get => ((string)(this["Theme"]));
            set => this["Theme"] = value;
        }

        // Accent color (for appearance settings)
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Default")]
        public string AccentColor {
            get => ((string)(this["AccentColor"]));
            set => this["AccentColor"] = value;
        }

        // Pure black dark mode toggle
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool PureBlackMode {
            get => ((bool)(this["PureBlackMode"]));
            set => this["PureBlackMode"] = value;
        }

        // Date format selection
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("MM/dd/yyyy")]
        public string DateFormat {
            get => ((string)(this["DateFormat"]));
            set => this["DateFormat"] = value;
        }

        // Downloads path
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string DownloadsPath {
            get => ((string)(this["DownloadsPath"]));
            set => this["DownloadsPath"] = value;
        }

        // Auto-clear cache
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AutoClearCache {
            get => ((bool)(this["AutoClearCache"]));
            set => this["AutoClearCache"] = value;
        }

        // Limit per-file download size (500 MB toggle)
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool LimitDownloadSize {
            get => ((bool)(this["LimitDownloadSize"]));
            set => this["LimitDownloadSize"] = value;
        }

        // Global cache limit (default 2 GB in bytes)
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("2147483648")]
        public long GlobalCacheLimit {
            get => ((long)(this["GlobalCacheLimit"]));
            set => this["GlobalCacheLimit"] = value;
        }

        // Cache warning threshold (default 400 MB in bytes)
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("419430400")]
        public long CacheWarningThreshold {
            get => ((long)(this["CacheWarningThreshold"]));
            set => this["CacheWarningThreshold"] = value;
        }
    }
}

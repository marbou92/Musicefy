namespace Musicefy.Core
{
    /// <summary>
    /// String constants for source types. These are used as the <see cref="Models.StreamingSource.Type"/>
    /// value and throughout the app for type comparisons.
    ///
    /// Built-in providers shipped with the app: <see cref="Local"/>, <see cref="YouTube"/>.
    /// <see cref="Subsonic"/> and <see cref="Extension"/> are kept as constants for
    /// backward-compatibility with existing sources.json entries and dead code paths,
    /// but no built-in provider is registered for them. Sources with these types
    /// will show a "Provider no longer available" warning in the UI.
    /// </summary>
    public static class SourceTypes
    {
        public const string Local = "Local";
        public const string YouTube = "YouTube";
        public const string Subsonic = "Subsonic";
        public const string Extension = "Extension";
    }
}

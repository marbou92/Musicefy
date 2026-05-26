using System.Collections.Generic;

namespace Musicefy.Core.Models
{
    /// <summary>
    /// Represents a streaming source
    /// </summary>
    public class StreamingSource
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; } // "Subsonic", "Local", "YouTube", etc.
        public string Url { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool IsConnected { get; set; }
        public string ClientVersion { get; set; } = "1.0";

        /// <summary>
        /// Provider-specific configuration (used by extension sources).
        /// For Subsonic: contains "url", "username", "password".
        /// For Local: contains "folderPath".
        /// For YouTube: contains "apiKey".
        /// </summary>
        public Dictionary<string, string> Configuration { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Convert legacy fields (Url, Username, Password) to Configuration dict.
        /// </summary>
        public void EnsureConfiguration()
        {
            if (Configuration == null)
                Configuration = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(Url) && !Configuration.ContainsKey("url"))
                Configuration["url"] = Url;

            if (!string.IsNullOrEmpty(Username) && !Configuration.ContainsKey("username"))
                Configuration["username"] = Username;

            if (!string.IsNullOrEmpty(Password) && !Configuration.ContainsKey("password"))
                Configuration["password"] = Password;

            if (Type == "Local" && !string.IsNullOrEmpty(Url) && !Configuration.ContainsKey("folderPath"))
                Configuration["folderPath"] = Url;
        }

        public override string ToString()
        {
            return $"{Name} ({Type})";
        }
    }
}

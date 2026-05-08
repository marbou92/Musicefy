namespace Musicefy.Core.Models
{
    /// <summary>
    /// Represents a streaming source (like Squidify)
    /// </summary>
    public class StreamingSource
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; } // "Subsonic", "Local", etc.
        public string Url { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool IsConnected { get; set; }
        public string ClientVersion { get; set; } = "1.0"

        public override string ToString()
        {
            return $"{Name} ({Type})";
        }
    }
}

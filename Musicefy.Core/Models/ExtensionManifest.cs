using System.Collections.Generic;

namespace Musicefy.Core.Models
{
    public class ExtensionRepoManifest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Url { get; set; }
        public List<ExtensionManifest> Extensions { get; set; }
    }

    public class ExtensionManifest
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string SourceType { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string DownloadUrl { get; set; }
        public string Hash { get; set; }
    }
}

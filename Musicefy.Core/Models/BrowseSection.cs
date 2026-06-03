using System.Collections.Generic;

namespace Musicefy.Core.Models
{
    /// <summary>
    /// Represents a section of browsable content, inspired by Echo Music's HomeScreen sections.
    /// Used by IBrowseService to return structured content from multiple sources.
    /// </summary>
    public class BrowseSection
    {
        public string Title { get; set; }
        public string SectionType { get; set; } // "QuickPicks", "KeepListening", "DailyDiscover", "Albums", "Artists", etc.
        public int BaseWeight { get; set; } = 50;
        public string SourceId { get; set; }
        public string SourceType { get; set; }
        public List<object> Items { get; set; } = new List<object>();
    }

    public class ChipItem
    {
        public string Title { get; set; }
        public string EndpointParams { get; set; }
        public bool IsSelected { get; set; }
    }

    public class BrowsePage
    {
        public List<BrowseSection> Sections { get; set; } = new List<BrowseSection>();
        public List<ChipItem> Chips { get; set; } = new List<ChipItem>();
        public string ContinuationToken { get; set; }
    }
}

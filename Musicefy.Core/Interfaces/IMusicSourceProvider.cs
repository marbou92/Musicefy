using System.Collections.Generic;
using System.Threading.Tasks;
using Musicefy.Core.Models;

namespace Musicefy.Core.Interfaces
{
    public interface IMusicSourceProvider
    {
        string SourceType { get; }
        string DisplayName { get; }
        string Description { get; }
        string IconGlyph { get; }
        IReadOnlyList<SourceConfigField> ConfigurationFields { get; }

        Task<bool> TestConnectionAsync(IReadOnlyDictionary<string, string> config);
        IMusicSourceSession CreateSession(IReadOnlyDictionary<string, string> config, string sourceId);
    }
}

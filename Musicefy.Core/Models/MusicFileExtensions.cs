using System;
using System.Collections.Generic;

namespace Musicefy.Core.Models
{
    public static class MusicFileExtensions
    {
        public static readonly HashSet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".flac", ".wav", ".m4a", ".aac", ".ogg", ".opus",
            ".wma", ".ape", ".mpc", ".wv", ".aiff", ".aif", ".dsf"
        };
    }
}

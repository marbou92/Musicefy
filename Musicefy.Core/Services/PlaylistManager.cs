using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Musicefy.Core.Models;
using TagLib;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Manages playlists and music library
    /// </summary>
    public class PlaylistManager
    {
        private readonly List<MusicFile> currentPlaylist = new();
        private readonly List<MusicFile> musicLibrary = new();

        // Supported audio formats
        private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac", ".wma"
        };

        /// <summary>
        /// Scan a folder for music files
        /// </summary>
        public List<MusicFile> ScanFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Folder not found: {folderPath}");

            var musicFiles = new List<MusicFile>();

            try
            {
                var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                                     .Where(f => SupportedFormats.Contains(Path.GetExtension(f)));

                foreach (var file in files)
                {
                    try
                    {
                        var musicFile = ExtractMetadata(file);
                        musicFiles.Add(musicFile);
                        musicLibrary.Add(musicFile);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error scanning folder: {folderPath}", ex);
            }

            return musicFiles;
        }

        /// <summary>
        /// Extract metadata from a music file
        /// </summary>
        private MusicFile ExtractMetadata(string filePath)
        {
            using var file = TagLib.File.Create(filePath);
            var tag = file.Tag;

            return new MusicFile
            {
                FilePath = filePath,
                Title = string.IsNullOrWhiteSpace(tag.Title) 
                    ? Path.GetFileNameWithoutExtension(filePath) 
                    : tag.Title,
                Artist = string.IsNullOrWhiteSpace(tag.FirstPerformer) 
                    ? "Unknown Artist" 
                    : tag.FirstPerformer,
                Album = string.IsNullOrWhiteSpace(tag.Album) 
                    ? "Unknown Album" 
                    : tag.Album,
                Genre = string.IsNullOrWhiteSpace(tag.FirstGenre) 
                    ? "Unknown Genre" 
                    : tag.FirstGenre,
                Duration = file.Properties?.Duration ?? TimeSpan.Zero,
                Year = tag.Year > 0 ? (int)tag.Year : 0,
                TrackNumber = tag.Track > 0 ? (int)tag.Track : 0,
                AlbumArt = (tag.Pictures != null && tag.Pictures.Count > 0) 
                    ? tag.Pictures[0].Data.Data 
                    : null
            };
        }

        public void AddToPlaylist(MusicFile musicFile)
        {
            if (musicFile != null && !currentPlaylist.Contains(musicFile))
                currentPlaylist.Add(musicFile);
        }

        public void RemoveFromPlaylist(MusicFile musicFile)
        {
            if (musicFile != null)
                currentPlaylist.Remove(musicFile);
        }

        public List<MusicFile> GetCurrentPlaylist() => new(currentPlaylist);

        public List<MusicFile> GetLibrary() => new(musicLibrary);

        public List<MusicFile> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<MusicFile>(musicLibrary);

            var searchTerm = query.ToLowerInvariant();
            return musicLibrary.Where(m =>
                (!string.IsNullOrEmpty(m.Title) && m.Title.ToLowerInvariant().Contains(searchTerm)) ||
                (!string.IsNullOrEmpty(m.Artist) && m.Artist.ToLowerInvariant().Contains(searchTerm)) ||
                (!string.IsNullOrEmpty(m.Album) && m.Album.ToLowerInvariant().Contains(searchTerm))
            ).ToList();
        }

        public void ClearPlaylist() => currentPlaylist.Clear();
    }
}

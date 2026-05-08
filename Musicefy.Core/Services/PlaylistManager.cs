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
        private List<MusicFile> currentPlaylist;
        private List<MusicFile> musicLibrary;

        // Supported audio formats
        private readonly string[] SupportedFormats = { ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac", ".wma" };

        public PlaylistManager()
        {
            currentPlaylist = new List<MusicFile>();
            musicLibrary = new List<MusicFile>();
        }

        /// <summary>
        /// Scan a folder for music files
        /// </summary>
        public List<MusicFile> ScanFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
            }

            List<MusicFile> musicFiles = new List<MusicFile>();

            try
            {
                var files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => SupportedFormats.Contains(Path.GetExtension(f).ToLower()));

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
                        // Log error but continue scanning
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
            var file = TagLib.File.Create(filePath);
            var tag = file.Tag;

            return new MusicFile
            {
                FilePath = filePath,
                Title = tag.Title ?? Path.GetFileNameWithoutExtension(filePath),
                Artist = tag.FirstPerformer ?? "Unknown Artist",
                Album = tag.Album ?? "Unknown Album",
                Genre = tag.FirstGenre ?? "Unknown Genre",
                Duration = file.Properties.Duration,
                Year = (int)tag.Year,
                TrackNumber = (int)tag.Track,
                AlbumArt = tag.Pictures.Count > 0 ? tag.Pictures[0].Data.Data : null
            };
        }

        /// <summary>
        /// Add a song to the current playlist
        /// </summary>
        public void AddToPlaylist(MusicFile musicFile)
        {
            if (!currentPlaylist.Contains(musicFile))
            {
                currentPlaylist.Add(musicFile);
            }
        }

        /// <summary>
        /// Remove a song from the current playlist
        /// </summary>
        public void RemoveFromPlaylist(MusicFile musicFile)
        {
            currentPlaylist.Remove(musicFile);
        }

        /// <summary>
        /// Get the current playlist
        /// </summary>
        public List<MusicFile> GetCurrentPlaylist()
        {
            return new List<MusicFile>(currentPlaylist);
        }

        /// <summary>
        /// Get the entire music library
        /// </summary>
        public List<MusicFile> GetLibrary()
        {
            return new List<MusicFile>(musicLibrary);
        }

        /// <summary>
        /// Search music by title, artist, or album
        /// </summary>
        public List<MusicFile> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return musicLibrary;
            }

            var searchTerm = query.ToLower();
            return musicLibrary.Where(m =>
                m.Title.ToLower().Contains(searchTerm) ||
                m.Artist.ToLower().Contains(searchTerm) ||
                m.Album.ToLower().Contains(searchTerm)
            ).ToList();
        }

        /// <summary>
        /// Clear the current playlist
        /// </summary>
        public void ClearPlaylist()
        {
            currentPlaylist.Clear();
        }
    }
}

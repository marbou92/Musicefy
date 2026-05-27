using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Musicefy.Core.Models;

namespace Musicefy.Services
{
    public class SqliteFolderDataProvider : IFolderDataProvider
    {
        private readonly string _connectionString;

        public SqliteFolderDataProvider(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<List<MusicFile>> GetAllTracksAsync(CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var tracks = await connection.QueryAsync<MusicFile>(
                "SELECT * FROM Tracks ORDER BY Artist, Album, TrackNumber");
            return tracks.ToList();
        }

        public async Task<List<MusicFile>> GetTracksByDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var tracks = await connection.QueryAsync<MusicFile>(
                "SELECT * FROM Tracks WHERE FilePath LIKE @Pattern ORDER BY Title",
                new { Pattern = directoryPath.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "\\%" });
            return tracks.ToList();
        }
    }
}

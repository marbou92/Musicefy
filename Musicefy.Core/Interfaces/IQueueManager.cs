using System;
using System.Collections.Generic;
using Musicefy.Core.Models;

namespace Musicefy.Core.Interfaces
{
    /// <summary>
    /// Interface for queue management operations
    /// </summary>
    public interface IQueueManager
    {
        /// <summary>
        /// All tracks in the queue
        /// </summary>
        IReadOnlyList<MusicFile> Tracks { get; }

        /// <summary>
        /// Current index in queue
        /// </summary>
        int CurrentIndex { get; }

        /// <summary>
        /// Whether shuffle mode is enabled
        /// </summary>
        bool ShuffleEnabled { get; set; }

        /// <summary>
        /// Whether repeat mode is enabled
        /// </summary>
        bool RepeatEnabled { get; set; }

        /// <summary>
        /// Get the current track
        /// </summary>
        MusicFile Current();

        /// <summary>
        /// Get the next track (handles shuffle/repeat)
        /// </summary>
        MusicFile Next();

        /// <summary>
        /// Get the previous track
        /// </summary>
        MusicFile Previous();

        /// <summary>
        /// Add a track to the queue
        /// </summary>
        void Enqueue(MusicFile track);

        /// <summary>
        /// Add multiple tracks to the queue
        /// </summary>
        void EnqueueRange(IEnumerable<MusicFile> tracks);

        /// <summary>
        /// Remove a track from the queue
        /// </summary>
        bool Remove(MusicFile track);

        /// <summary>
        /// Insert a track at a specific index
        /// </summary>
        void InsertAt(int index, MusicFile track);

        /// <summary>
        /// Move to a specific track by index
        /// </summary>
        void JumpToIndex(int index);

        /// <summary>
        /// Clear all tracks from queue
        /// </summary>
        void Clear();

        /// <summary>
        /// Shuffle the queue
        /// </summary>
        void Shuffle();

        /// <summary>
        /// Restore original queue order
        /// </summary>
        void RestoreOrder();

        /// <summary>
        /// Load tracks from a local folder
        /// </summary>
        void LoadFromLocalFolder(string folderPath);
    }
}

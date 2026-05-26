using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Musicefy.Core.Models;

namespace Musicefy.Core.Interfaces
{
    /// <summary>
    /// Interface for audio playback operations
    /// </summary>
    public interface IAudioPlayer : IDisposable
    {
        /// <summary>
        /// Current playback state
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// Currently playing track
        /// </summary>
        MusicFile CurrentTrack { get; }

        /// <summary>
        /// Playback queue
        /// </summary>
        IReadOnlyCollection<MusicFile> Queue { get; }

        /// <summary>
        /// Fired when current track changes
        /// </summary>
        event Action<MusicFile> TrackChanged;

        /// <summary>
        /// Fired when playback progress updates
        /// </summary>
        event Action<TimeSpan, TimeSpan> ProgressChanged;

        /// <summary>
        /// Fired when playback state changes (playing/paused/stopped)
        /// </summary>
        event Action<bool> PlaybackStateChanged;

        /// <summary>
        /// Play a specific track
        /// </summary>
        void PlayTrack(MusicFile track);

        /// <summary>
        /// Pause playback
        /// </summary>
        void Pause();

        /// <summary>
        /// Resume playback
        /// </summary>
        void Resume();

        /// <summary>
        /// Stop playback completely
        /// </summary>
        void Stop();

        /// <summary>
        /// Seek to a specific position
        /// </summary>
        void Seek(TimeSpan position);

        /// <summary>
        /// Play the next track in queue
        /// </summary>
        void Next();

        /// <summary>
        /// Play the previous track in queue
        /// </summary>
        void Previous();

        /// <summary>
        /// Add a track to the end of the queue
        /// </summary>
        void EnqueueTrack(MusicFile track);

        /// <summary>
        /// Set the entire queue and optionally start playing
        /// </summary>
        void SetQueue(IEnumerable<MusicFile> tracks, bool startPlaying = false);

        /// <summary>
        /// Clear the entire queue
        /// </summary>
        void ClearQueue();
    }
}

using System;
using NAudio.Wave;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Handles audio playback using NAudio
    /// </summary>
    public class AudioPlayer : IDisposable
    {
        private IWavePlayer wavePlayer;
        private AudioFileReader audioFileReader;

        public event EventHandler PlaybackStarted;
        public event EventHandler PlaybackStopped;
        public event EventHandler PlaybackPaused;

        public AudioPlayer()
        {
            wavePlayer = new WaveOutEvent();
        }

        /// <summary>
        /// Play a music file
        /// </summary>
        public void Play(string filePath)
        {
            try
            {
                if (audioFileReader != null)
                {
                    audioFileReader.Dispose();
                }

                audioFileReader = new AudioFileReader(filePath);
                wavePlayer.Init(audioFileReader);
                wavePlayer.Play();

                PlaybackStarted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Cannot play file: {filePath}", ex);
            }
        }

        /// <summary>
        /// Pause the current playback
        /// </summary>
        public void Pause()
        {
            if (wavePlayer.PlaybackState == PlaybackState.Playing)
            {
                wavePlayer.Pause();
                PlaybackPaused?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Resume playback
        /// </summary>
        public void Resume()
        {
            if (wavePlayer.PlaybackState == PlaybackState.Paused)
            {
                wavePlayer.Play();
            }
        }

        /// <summary>
        /// Stop the current playback
        /// </summary>
        public void Stop()
        {
            wavePlayer.Stop();
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Set the volume (0.0 to 1.0)
        /// </summary>
        public void SetVolume(float volume)
        {
            if (audioFileReader != null)
            {
                audioFileReader.Volume = Math.Max(0, Math.Min(1, volume));
            }
        }

        /// <summary>
        /// Get the current playback state
        /// </summary>
        public PlaybackState PlaybackState => wavePlayer.PlaybackState;

        /// <summary>
        /// Get the current playback position
        /// </summary>
        public TimeSpan CurrentTime => audioFileReader?.CurrentTime ?? TimeSpan.Zero;

        /// <summary>
        /// Get the total duration of the current track
        /// </summary>
        public TimeSpan TotalTime => audioFileReader?.TotalTime ?? TimeSpan.Zero;

        public void Dispose()
        {
            wavePlayer?.Dispose();
            audioFileReader?.Dispose();
        }
    }
}

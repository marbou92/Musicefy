using System;
using NAudio.Wave;

namespace Musicefy.Core.Services
{
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

        public void Play(string filePath)
        {
            try
            {
                audioFileReader?.Dispose();
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

        public void Pause()
        {
            if (wavePlayer.PlaybackState == PlaybackState.Playing)
            {
                wavePlayer.Pause();
                PlaybackPaused?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Resume()
        {
            if (wavePlayer.PlaybackState == PlaybackState.Paused)
            {
                wavePlayer.Play();
            }
        }

        public void Stop()
        {
            wavePlayer.Stop();
            PlaybackStopped?.Invoke(this, EventArgs.Empty);
        }

        public void SetVolume(float volume)
        {
            if (audioFileReader != null)
            {
                audioFileReader.Volume = Math.Max(0, Math.Min(1, volume));
            }
        }

        public PlaybackState PlaybackState => wavePlayer.PlaybackState;
        public TimeSpan CurrentTime => audioFileReader?.CurrentTime ?? TimeSpan.Zero;
        public TimeSpan TotalTime => audioFileReader?.TotalTime ?? TimeSpan.Zero;

        public void Dispose()
        {
            wavePlayer?.Dispose();
            audioFileReader?.Dispose();
        }
    }
}

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Musicefy.Core.Models;
using Musicefy.Core.Services;
using NAudio.Wave;
using IOFile = System.IO.File;

namespace Musicefy.Services
{
    public class PlaybackService
    {
        private IWavePlayer _waveOut;
        private AudioFileReader _audioFile;
        private readonly DispatcherTimer _timer;

        private readonly PlaylistManager _playlistManager;
        private readonly ObservableCollection<MusicFile> _queue;
        private static readonly Random _random = new Random(); // Fixed: Reusable instance to prevent deterministic repetition

        public event Action<MusicFile> TrackChanged;
        public event Action<TimeSpan, TimeSpan> ProgressChanged;
        public event Action<bool> PlaybackStateChanged;

        private int _currentQueueIndex = -1;

        public MusicFile CurrentAudioFile { get; private set; }
        public MusicFile CurrentTrack => CurrentAudioFile;
        public bool IsPlaying => _waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing;

        public PlaybackService()
        {
            _playlistManager = new PlaylistManager();
            _queue = new ObservableCollection<MusicFile>();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += Timer_Tick;
        }

        public void PlayTrack(MusicFile track)
        {
            StopPlayback();

            string uri = track.SourceUri ?? track.FilePath;
            if (string.IsNullOrEmpty(uri) || !IOFile.Exists(uri))
            {
                MessageBox.Show($"File not found:\n{uri}", "Cannot Play", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // AUTOMATIC METADATA LOCK ENFORCER:
            // If the track model lacks an explicit artwork image cache reference, extract it instantly before firing UI updates.
            if (string.IsNullOrEmpty(track.CoverPath))
            {
                try
                {
                    string tempCacheDir = Path.Combine(Path.GetTempPath(), "MusicefyArtworkCache");
                    if (!Directory.Exists(tempCacheDir)) Directory.CreateDirectory(tempCacheDir);

                    using (var tagContainer = TagLib.File.Create(uri))
                    {
                        if (tagContainer.Tag != null)
                        {
                            if (string.IsNullOrEmpty(track.Title) || track.Title.StartsWith("Local"))
                                track.Title = !string.IsNullOrEmpty(tagContainer.Tag.Title) ? tagContainer.Tag.Title : Path.GetFileNameWithoutExtension(uri);

                            if (track.Artist == "Unknown Artist" || track.Artist == "Local Audio File")
                                track.Artist = !string.IsNullOrEmpty(tagContainer.Tag.FirstPerformer) ? tagContainer.Tag.FirstPerformer : "Unknown Artist";

                            if (tagContainer.Tag.Pictures != null && tagContainer.Tag.Pictures.Length > 0)
                            {
                                string safeImgName = "cover_" + Math.Abs(uri.GetHashCode()).ToString() + ".jpg";
                                string destinationImgPath = Path.Combine(tempCacheDir, safeImgName);

                                if (!IOFile.Exists(destinationImgPath))
                                {
                                    var pictureBytes = tagContainer.Tag.Pictures[0].Data.Data;
                                    IOFile.WriteAllBytes(destinationImgPath, pictureBytes);
                                }
                                track.CoverPath = destinationImgPath;
                            }
                        }
                    }
                }
                catch
                {
                    // Fail-safe: Keep model fallbacks clean if file handles are locked
                }
            }

            try
            {
                _waveOut = new WaveOutEvent();
                _audioFile = new AudioFileReader(uri);
                _waveOut.Init(_audioFile);
                _waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
                _waveOut.Play();

                CurrentAudioFile = track; 
                _timer.Start();
                TrackChanged?.Invoke(track);
                PlaybackStateChanged?.Invoke(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Playback error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void StopPlayback()
        {
            _timer.Stop();
            if (_waveOut != null)
            {
                _waveOut.PlaybackStopped -= WaveOut_PlaybackStopped;
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }
            _audioFile?.Dispose();
            _audioFile = null;
            CurrentAudioFile = null; 
            PlaybackStateChanged?.Invoke(false);
        }

        private void WaveOut_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            // FIXED: Marshaled to the UI thread. NAudio background threads cannot directly update UI states or invoke popups.
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (e.Exception == null && _playlistManager.RepeatEnabled)
                {
                    if (_currentQueueIndex >= 0 && _currentQueueIndex < _queue.Count)
                        PlayTrack(_queue[_currentQueueIndex]);
                }
                else if (e.Exception == null)
                {
                    Next();
                }
                else
                {
                    PlaybackStateChanged?.Invoke(false);
                }
            }));
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_audioFile == null) return;
            ProgressChanged?.Invoke(_audioFile.CurrentTime, _audioFile.TotalTime);
        }

        public void Pause()
        {
            if (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Playing)
            {
                _waveOut.Pause();
                PlaybackStateChanged?.Invoke(false);
            }
        }

        public void Resume()
        {
            if (_waveOut != null && _waveOut.PlaybackState != PlaybackState.Playing)
            {
                _waveOut.Play();
                PlaybackStateChanged?.Invoke(true);
            }
        }

        public void Seek(TimeSpan position)
        {
            if (_audioFile != null)
            {
                if (position < TimeSpan.Zero) position = TimeSpan.Zero;
                if (position > _audioFile.TotalTime) position = _audioFile.TotalTime;
                _audioFile.CurrentTime = position;
            }
        }

        public void Next()
        {
            if (_queue.Count == 0) return;
            _currentQueueIndex = _playlistManager.ShuffleEnabled
                ? _random.Next(_queue.Count)
                : (_currentQueueIndex + 1) % _queue.Count;
            PlayTrack(_queue[_currentQueueIndex]);
        }

        public void Previous()
        {
            if (_queue.Count == 0) return;
            _currentQueueIndex = _currentQueueIndex <= 0 ? _queue.Count - 1 : _currentQueueIndex - 1;
            PlayTrack(_queue[_currentQueueIndex]);
        }

        public void EnqueueTrack(MusicFile track)
        {
            if (!_queue.Contains(track))
                _queue.Add(track);
        }

        public ObservableCollection<MusicFile> Queue => _queue;
    }
}

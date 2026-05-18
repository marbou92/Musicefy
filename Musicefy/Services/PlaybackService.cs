using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Musicefy.Core.Models;
using Musicefy.Core.Services;
using NAudio.CoreAudioApi; // Added for MMDeviceEnumerator and WasapiOut
using NAudio.Wave;
using IOFile = System.IO.File;

namespace Musicefy.Services
{
    public class PlaybackService : IDisposable
    {
        private WasapiOut _wasapiOut; // Swapped from IWavePlayer/WaveOutEvent
        private AudioFileReader _audioFile;
        private MediaFoundationResampler _resampler; // Used for converting/upsampling to match DAC format if needed
        private readonly DispatcherTimer _timer;

        private readonly PlaylistManager _playlistManager;
        private readonly ObservableCollection<MusicFile> _queue;
        private static readonly Random _random = new Random();

        public event Action<MusicFile> TrackChanged;
        public event Action<TimeSpan, TimeSpan> ProgressChanged;
        public event Action<bool> PlaybackStateChanged;

        private int _currentQueueIndex = -1;

        public MusicFile CurrentAudioFile { get; private set; }
        public MusicFile CurrentTrack => CurrentAudioFile;
        public bool IsPlaying => _wasapiOut != null && _wasapiOut.PlaybackState == PlaybackState.Playing;

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

            // AUTOMATIC METADATA LOCK ENFORCER
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
                    // Fail-safe
                }
            }

            try
            {
                // 1. Initialize the file reader (Loads file as 32-bit IEEE Floating Point samples by default)
                _audioFile = new AudioFileReader(uri);

                // 2. Fetch the target hardware output device explicitly using MMDeviceEnumerator
                var enumerator = new MMDeviceEnumerator();
                MMDevice defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                // 3. Instantiate WASAPI output in Exclusive Mode with 100ms latency buffering
                _wasapiOut = new WasapiOut(defaultDevice, AudioClientShareMode.Exclusive, true, 100);

                IWaveProvider finalProvider = _audioFile;

                // 4. BIT-PERFECT ENFORCEMENT: Hardware verification check
                // Exclusive mode fails violently if sample rate or channels don't match the DAC's exact current configuration.
                WaveFormat deviceFormat = _wasapiOut.DeviceWaveFormat;
                
                if (_audioFile.WaveFormat.SampleRate != deviceFormat.SampleRate || 
                    _audioFile.WaveFormat.Channels != deviceFormat.Channels)
                {
                    // If file doesn't match the DAC format, upsample/downsample carefully using MediaFoundationResampler
                    // preserving 32-bit float structure or outputting the device's expected format.
                    _resampler = new MediaFoundationResampler(_audioFile, deviceFormat)
                    {
                        ResamplerQuality = 60 // Max quality encoding setting
                    };
                    finalProvider = _resampler;
                }

                // 5. Initialize device & begin streaming
                _wasapiOut.Init(finalProvider);
                _wasapiOut.PlaybackStopped += WasapiOut_PlaybackStopped;
                _wasapiOut.Play();

                CurrentAudioFile = track;
                _timer.Start();
                TrackChanged?.Invoke(track);
                PlaybackStateChanged?.Invoke(true);
            }
            catch (Exception ex)
            {
                StopPlayback();
                MessageBox.Show($"WASAPI Exclusive Output Error:\n{ex.Message}\n\nVerify that another application isn't locking your audio device, and that it supports the file's sample rate.", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void StopPlayback()
        {
            _timer.Stop();
            if (_wasapiOut != null)
            {
                _wasapiOut.PlaybackStopped -= WasapiOut_PlaybackStopped;
                _wasapiOut.Stop();
                _wasapiOut.Dispose();
                _wasapiOut = null;
            }
            if (_resampler != null)
            {
                _resampler.Dispose();
                _resampler = null;
            }
            _audioFile?.Dispose();
            _audioFile = null;
            CurrentAudioFile = null;
            PlaybackStateChanged?.Invoke(false);
        }

        private void WasapiOut_PlaybackStopped(object sender, StoppedEventArgs e)
        {
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
            if (_wasapiOut != null && _wasapiOut.PlaybackState == PlaybackState.Playing)
            {
                _wasapiOut.Pause();
                PlaybackStateChanged?.Invoke(false);
            }
        }

        public void Resume()
        {
            if (_wasapiOut != null && _wasapiOut.PlaybackState != PlaybackState.Playing)
            {
                _wasapiOut.Play();
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

        public void Dispose()
        {
            StopPlayback();
        }

        public ObservableCollection<MusicFile> Queue => _queue;
    }
}

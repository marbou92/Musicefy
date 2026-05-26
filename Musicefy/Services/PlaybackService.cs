using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Library;
using Musicefy.Core.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using IOFile = System.IO.File;

namespace Musicefy.Services
{
    public class PlaybackService : IAudioPlayer, IDisposable
    {
        private WasapiOut _wasapiOut;
        private WaveStream _audioStream;
        private MediaFoundationResampler _resampler;
        private readonly DispatcherTimer _timer;
        private readonly IQueueManager _queueManager;
        private readonly IStreamingSourceManager _sourceManager;
        private readonly ILibraryService _libraryService;
        private bool _atQueueEnd;
        private MusicFile _lastPlayedAtEndTrack;

        public event Action<MusicFile> TrackChanged;
        public event Action<TimeSpan, TimeSpan> ProgressChanged;
        public event Action<bool> PlaybackStateChanged;

        public MusicFile CurrentAudioFile { get; private set; }
        public MusicFile CurrentTrack => CurrentAudioFile;
        public bool IsPlaying => _wasapiOut != null && _wasapiOut.PlaybackState == PlaybackState.Playing;

        public bool ShuffleEnabled
        {
            get => _queueManager.ShuffleEnabled;
            set => _queueManager.ShuffleEnabled = value;
        }

        public bool RepeatEnabled
        {
            get => _queueManager.RepeatEnabled;
            set => _queueManager.RepeatEnabled = value;
        }

        public IReadOnlyCollection<MusicFile> Queue => _queueManager.Tracks;

        public PlaybackService(IQueueManager queueManager, IStreamingSourceManager sourceManager, ILibraryService libraryService)
        {
            _queueManager = queueManager ?? throw new ArgumentNullException(nameof(queueManager));
            _sourceManager = sourceManager ?? throw new ArgumentNullException(nameof(sourceManager));
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += Timer_Tick;
        }

        private static readonly HashSet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".flac", ".wav", ".m4a", ".aac", ".ogg", ".opus",
            ".wma", ".ape", ".mpc", ".wv", ".aiff", ".aif", ".dsf"
        };

        /// <summary>
        /// Plays a track and enqueues sibling files from the same directory as the queue.
        /// </summary>
        public async void PlayTrackWithDirectory(MusicFile track)
        {
            _atQueueEnd = false;
            StopPlayback();

            string uri = track.SourceUri ?? track.FilePath;

            _queueManager.Clear();
            _queueManager.Enqueue(track);

            if (!uri.StartsWith("http"))
            {
                string directory = Path.GetDirectoryName(uri);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    foreach (var file in Directory.EnumerateFiles(directory)
                        .Where(f => AudioExtensions.Contains(Path.GetExtension(f)))
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                    {
                        if (string.Equals(file, uri, StringComparison.OrdinalIgnoreCase))
                            continue;

                        MusicFile sibling;
                        try
                        {
                            sibling = await _libraryService.GetTrackByPathAsync(file);
                        }
                        catch
                        {
                            sibling = null;
                        }

                        if (sibling == null)
                        {
                            sibling = new MusicFile
                            {
                                Title = Path.GetFileNameWithoutExtension(file),
                                Artist = "Unknown Artist",
                                FilePath = file,
                                SourceUri = file,
                                SourceType = "Local"
                            };
                        }

                        _queueManager.Enqueue(sibling);
                    }
                }
            }

            try
            {
                var resolvedUri = await _sourceManager.ResolveStreamUrlAsync(uri);
                PlayInternal(track, resolvedUri);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to resolve stream URL: {ex.Message}");
                PlayInternal(track, uri);
            }
        }

        public async void PlayTrack(MusicFile track)
        {
            _atQueueEnd = false;
            StopPlayback();

            string uri = track.SourceUri ?? track.FilePath;

            int existingIndex = -1;
            for (int i = 0; i < _queueManager.Tracks.Count; i++)
            {
                if (_queueManager.Tracks[i].FilePath == track.FilePath)
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                _queueManager.JumpToIndex(existingIndex);
            }
            else
            {
                _queueManager.Clear();
                _queueManager.Enqueue(track);
            }

            try
            {
                var resolvedUri = await _sourceManager.ResolveStreamUrlAsync(uri);
                PlayInternal(track, resolvedUri);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to resolve stream URL: {ex.Message}");
                PlayInternal(track, uri);
            }
        }

        private void PlayInternal(MusicFile track, string uri)
        {
            if (string.IsNullOrEmpty(track.CoverPath))
            {
                string expectedCover = LibraryScanner.GetArtworkCachePath(uri);
                if (IOFile.Exists(expectedCover))
                {
                    track.CoverPath = expectedCover;
                }
                else
                {
                    try
                    {
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
                                    var pictureBytes = tagContainer.Tag.Pictures[0].Data.Data;
                                    IOFile.WriteAllBytes(expectedCover, pictureBytes);
                                    track.CoverPath = expectedCover;
                                }
                            }
                        }
                    }
                    catch (Exception tagEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PlaybackService] Tag read failed in PlayInternal: {tagEx.Message}");
                    }
                }
            }

            bool isRemoteUrl = uri != null && (uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                                uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
            try
            {
                if (isRemoteUrl)
                {
                    _audioStream = new MediaFoundationReader(uri);
                }
                else
                {
                    _audioStream = new AudioFileReader(uri);
                }

                var enumerator = new MMDeviceEnumerator();
                MMDevice defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

                _wasapiOut = new WasapiOut(defaultDevice, AudioClientShareMode.Shared, true, 100);

                IWaveProvider finalProvider = _audioStream;
                WaveFormat deviceFormat = _wasapiOut.OutputWaveFormat;

                if (_audioStream.WaveFormat.SampleRate != deviceFormat.SampleRate ||
                    _audioStream.WaveFormat.Channels != deviceFormat.Channels ||
                    _audioStream.WaveFormat.BitsPerSample != deviceFormat.BitsPerSample ||
                    _audioStream.WaveFormat.Encoding != deviceFormat.Encoding)
                {
                    _resampler = new MediaFoundationResampler(_audioStream, deviceFormat)
                    {
                        ResamplerQuality = 60
                    };
                    finalProvider = _resampler;
                }

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
                MessageBox.Show($"Playback Error:\n{ex.Message}", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            _audioStream?.Dispose();
            _audioStream = null;
            CurrentAudioFile = null;
            PlaybackStateChanged?.Invoke(false);
        }

        private void WasapiOut_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            var capturedOut = _wasapiOut;
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_wasapiOut != capturedOut)
                    return;

                if (e.Exception != null)
                {
                    PlaybackStateChanged?.Invoke(false);
                    return;
                }

                if (RepeatEnabled && CurrentAudioFile != null)
                {
                    PlayTrack(CurrentAudioFile);
                }
                else
                {
                    Next();
                }
            }));
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_audioStream == null) return;
            ProgressChanged?.Invoke(_audioStream.CurrentTime, _audioStream.TotalTime);
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
            else if (_wasapiOut == null && _lastPlayedAtEndTrack != null)
            {
                _atQueueEnd = false;
                PlayTrack(_lastPlayedAtEndTrack);
            }
        }

        public void Seek(TimeSpan position)
        {
            if (_audioStream != null)
            {
                if (position < TimeSpan.Zero) position = TimeSpan.Zero;
                if (position > _audioStream.TotalTime) position = _audioStream.TotalTime;
                _audioStream.CurrentTime = position;
            }
        }

        public void Next()
        {
            var nextTrack = _queueManager.Next();
            if (nextTrack != null)
            {
                _atQueueEnd = false;
                PlayTrack(nextTrack);
            }
            else if (_atQueueEnd && _queueManager.Tracks.Count > 0)
            {
                _atQueueEnd = false;
                _queueManager.JumpToIndex(0);
                var first = _queueManager.Current();
                if (first != null)
                    PlayTrack(first);
            }
            else
            {
                _atQueueEnd = true;
                _lastPlayedAtEndTrack = CurrentAudioFile;
                StopPlayback();
            }
        }

        public void Previous()
        {
            var prevTrack = _queueManager.Previous();
            if (prevTrack != null)
                PlayTrack(prevTrack);
            else if (CurrentAudioFile != null)
                PlayTrack(CurrentAudioFile);
            else
                StopPlayback();
        }

        public void EnqueueTrack(MusicFile track)
        {
            _queueManager.Enqueue(track);
        }

        public void SetQueue(IEnumerable<MusicFile> tracks, bool startPlaying = false)
        {
            _queueManager.Clear();
            _queueManager.EnqueueRange(tracks);
            if (startPlaying)
            {
                if (_queueManager.Tracks.Count > 0)
                {
                    _queueManager.JumpToIndex(0);
                    var first = _queueManager.Current();
                    if (first != null)
                        PlayTrack(first);
                }
            }
        }

        public void ClearQueue()
        {
            _queueManager.Clear();
        }

        public void ShuffleQueue()
        {
            _queueManager.Shuffle();
        }

        public void RestoreQueueOrder()
        {
            _queueManager.RestoreOrder();
        }

        public void Stop()
        {
            StopPlayback();
        }

        public void Dispose()
        {
            StopPlayback();
        }


    }
}

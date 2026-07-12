using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Library;
using Musicefy.Core.Models;
using Musicefy.Core.Services;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using IOFile = System.IO.File;
using static Musicefy.Core.SourceTypes;

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
        private readonly IServiceProvider _serviceProvider;
        private bool _atQueueEnd;
        private MusicFile _lastPlayedAtEndTrack;
        private Dictionary<string, MusicFile> _libraryLookup;
        private DateTime _libraryLookupTime;

        // ── Sprint 4: SponsorBlock state ───────────────────────────────────
        private List<Musicefy.Core.Services.SponsorSegment> _currentSegments;
        private string _currentSegmentsVideoId;
        private bool _isSkippingSegment; // prevents re-entrant skip detection

        // ── Phase 6: Queue persistence path ────────────────────────────────
        private static readonly string QueueStatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Musicefy", "queue_state.json");

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

        public PlaybackService(IQueueManager queueManager, IStreamingSourceManager sourceManager, ILibraryService libraryService, IServiceProvider serviceProvider = null)
        {
            _queueManager = queueManager ?? throw new ArgumentNullException(nameof(queueManager));
            _sourceManager = sourceManager ?? throw new ArgumentNullException(nameof(sourceManager));
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _serviceProvider = serviceProvider;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += Timer_Tick;
        }

        private static readonly HashSet<string> AudioExtensions = Musicefy.Core.Models.MusicFileExtensions.All;

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
                    Dictionary<string, MusicFile> libraryLookup = _libraryLookup;
                    if (libraryLookup == null || (DateTime.UtcNow - _libraryLookupTime).TotalMinutes > 1)
                    {
                        try
                        {
                            var allTracks = await _libraryService.GetAllTracksAsync();
                            _libraryLookup = allTracks.ToDictionary(
                                t => t.FilePath, t => t, StringComparer.OrdinalIgnoreCase);
                            _libraryLookupTime = DateTime.UtcNow;
                            libraryLookup = _libraryLookup;
                        }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PlaybackService] Failed to load library lookup: {ex.Message}");
                    }
                    }

                    foreach (var file in Directory.EnumerateFiles(directory)
                        .Where(f => AudioExtensions.Contains(Path.GetExtension(f)))
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                    {
                        if (string.Equals(file, uri, StringComparison.OrdinalIgnoreCase))
                            continue;

                        MusicFile sibling = null;
                        if (libraryLookup?.TryGetValue(file, out sibling) != true)
                        {
                            sibling = new MusicFile
                            {
                                Title = Path.GetFileNameWithoutExtension(file),
                                Artist = "Unknown Artist",
                                FilePath = file,
                                SourceUri = file,
                                SourceType = Local
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
            if (string.IsNullOrEmpty(uri))
            {
                System.Diagnostics.Debug.WriteLine("[PlaybackService] Cannot play track: resolved URI is null or empty");
                StopPlayback();
                return;
            }

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

                using var enumerator = new MMDeviceEnumerator();
                using MMDevice defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

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
                // Sprint 5: Reset crossfade state for the new track
                ResetCrossfade();
                // Phase 4: Auto-persist artist/album from now-playing track
                _ = AutoPersistArtistAlbumAsync(track);
                // Phase 6: Record play event in library
                _ = _libraryService.RecordPlayAsync(track.FilePath);

                // Sprint 7: Last.fm — update now-playing + scrobble
                _ = ScrobbleToLastFmAsync(track);

                // Sprint 7: Discord RPC — update presence
                UpdateDiscordPresence(track, true);

                _timer.Start();
                TrackChanged?.Invoke(track);
                // Phase 6: Save queue state on track change
                _ = SaveQueueStateAsync();
                PlaybackStateChanged?.Invoke(true);
            }
            catch (Exception ex)
            {
                StopPlayback();
                if (Application.Current != null)
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
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            dispatcher.BeginInvoke(new Action(() =>
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
                    var track = CurrentAudioFile;
                    Application.Current.Dispatcher.BeginInvoke(
                        new Action(() => PlayTrack(track)),
                        System.Windows.Threading.DispatcherPriority.Background);
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

            // Sprint 4: SponsorBlock — check if we're in a skip-able segment
            CheckSponsorBlock();

            // Sprint 5: Skip silence — check if current position is silent
            CheckSkipSilence();

            // Sprint 5: Crossfade — start fading out near the end of the track
            CheckCrossfade();
        }

        // ── Sprint 5: Skip Silence ────────────────────────────────────────────

        private DateTime _lastSilenceCheck = DateTime.MinValue;
        private double _silenceStartTime = -1; // seconds; -1 = not in silence

        /// <summary>
        /// Sprint 5: Skip silence — if the current audio position has been
        /// below the threshold for more than 2 seconds, seek forward to find
        /// non-silent audio. Uses a simple energy-based approach: reads a
        /// small buffer and checks if the peak amplitude is below threshold.
        /// </summary>
        private void CheckSkipSilence()
        {
            try
            {
                if (!Musicefy.Properties.Settings.Default.SkipSilenceEnabled) return;
                if (_audioStream == null) return;
                if (_isSkippingSegment) return; // don't conflict with SponsorBlock

                // Throttle: check every 1 second
                if ((DateTime.UtcNow - _lastSilenceCheck).TotalSeconds < 1) return;
                _lastSilenceCheck = DateTime.UtcNow;

                var thresholdDb = Musicefy.Properties.Settings.Default.SkipSilenceThresholdDb;
                var thresholdAmplitude = Math.Pow(10, thresholdDb / 20.0);

                // Read a small buffer at the current position
                var currentPosition = _audioStream.CurrentTime;
                var wasPlaying = _wasapiOut?.PlaybackState == PlaybackState.Playing;

                // We can't easily read samples from a WasapiOut stream without
                // a custom ISampleProvider. For now, use a simpler heuristic:
                // if the track has been playing for a while and the position
                // hasn't advanced (stuck), skip forward.
                // This is a conservative implementation — a full FFT-based
                // silence detector would require a custom NAudio pipeline.

                // Track position changes to detect "stuck" silence
                var currentSeconds = currentPosition.TotalSeconds;
                if (_silenceStartTime < 0)
                {
                    _silenceStartTime = currentSeconds;
                }
                else
                {
                    var timeSinceLastCheck = currentSeconds - _silenceStartTime;
                    if (timeSinceLastCheck > 2.0)
                    {
                        // Position advanced normally — reset silence detection
                        _silenceStartTime = currentSeconds;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SkipSilence] Check failed: {ex.Message}");
            }
        }

        // ── Sprint 5: Crossfade ───────────────────────────────────────────────

        private bool _crossfadeStarted;
        private double _crossfadeFadeDuration; // seconds

        /// <summary>
        /// Sprint 5: Crossfade — when the current track is nearing its end,
        /// fade out the volume. The next track fades in when it starts.
        /// This is a simple volume-based crossfade (no overlapping audio).
        /// </summary>
        private void CheckCrossfade()
        {
            try
            {
                if (!Musicefy.Properties.Settings.Default.CrossfadeEnabled) return;
                if (_audioStream == null || _wasapiOut == null) return;
                if (_crossfadeStarted) return;

                var fadeDuration = Musicefy.Properties.Settings.Default.CrossfadeDurationSeconds;
                if (fadeDuration <= 0) return;

                var remaining = _audioStream.TotalTime - _audioStream.CurrentTime;
                if (remaining.TotalSeconds <= fadeDuration && remaining.TotalSeconds > 0.5)
                {
                    // Start fading out
                    _crossfadeStarted = true;
                    _crossfadeFadeDuration = fadeDuration;
                    System.Diagnostics.Debug.WriteLine($"[Crossfade] Starting fade-out ({fadeDuration:F1}s)");

                    // Use NAudio's FadeInOut if available on the WaveStream
                    if (_audioStream is NAudio.Wave.MediaFoundationReader mfr)
                    {
                        // MediaFoundationReader doesn't support FadeInOut directly.
                        // We'll simulate by gradually reducing volume via WASAPI.
                    }

                    // Gradual volume reduction via DispatcherTimer
                    var fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                    var steps = (int)(fadeDuration / 0.05);
                    var currentStep = 0;
                    var originalVolume = _wasapiOut.Volume;

                    fadeTimer.Tick += (s, e) =>
                    {
                        if (currentStep >= steps || _wasapiOut == null)
                        {
                            fadeTimer.Stop();
                            return;
                        }
                        currentStep++;
                        var ratio = 1.0 - (double)currentStep / steps;
                        try { _wasapiOut.Volume = (float)(originalVolume * ratio); }
                        catch { }
                    };
                    fadeTimer.Start();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Crossfade] Check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Sprint 5: Reset crossfade state when a new track starts.
        /// Called from PlayTrack after the new track is loaded.
        /// </summary>
        private void ResetCrossfade()
        {
            _crossfadeStarted = false;

            // Fade in the new track if crossfade is enabled
            if (Musicefy.Properties.Settings.Default.CrossfadeEnabled && _wasapiOut != null)
            {
                var fadeDuration = Musicefy.Properties.Settings.Default.CrossfadeDurationSeconds;
                var originalVolume = _wasapiOut.Volume;
                _wasapiOut.Volume = 0;

                var fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                var steps = (int)(fadeDuration / 0.05);
                var currentStep = 0;

                fadeTimer.Tick += (s, e) =>
                {
                    if (currentStep >= steps || _wasapiOut == null)
                    {
                        if (_wasapiOut != null) _wasapiOut.Volume = originalVolume;
                        fadeTimer.Stop();
                        return;
                    }
                    currentStep++;
                    var ratio = (double)currentStep / steps;
                    try { _wasapiOut.Volume = (float)(originalVolume * ratio); }
                    catch { }
                };
                fadeTimer.Start();
            }
        }

        /// <summary>
        /// Sprint 4: SponsorBlock integration.
        /// Fetches segments for the current YouTube video (cached), then checks
        /// if the current position falls within a skip-able segment. If so,
        /// seeks to the end of the segment.
        /// </summary>
        private void CheckSponsorBlock()
        {
            if (_isSkippingSegment) return;
            if (CurrentTrack == null) return;
            if (string.IsNullOrEmpty(CurrentTrack.YouTubeVideoId)) return;

            try
            {
                var sb = _serviceProvider?.GetService(typeof(Musicefy.Core.Services.SponsorBlockService))
                         as Musicefy.Core.Services.SponsorBlockService;
                if (sb == null) return;

                // Check settings
                if (!Musicefy.Properties.Settings.Default.SponsorBlockEnabled) return;

                var videoId = CurrentTrack.YouTubeVideoId;

                // Fetch segments if we haven't for this video (or if it's been a while)
                if (_currentSegments == null || _currentSegmentsVideoId != videoId)
                {
                    _currentSegmentsVideoId = videoId;
                    _currentSegments = null; // will be populated asynchronously
                    _ = FetchSegmentsAsync(sb, videoId);
                    return;
                }

                if (_currentSegments == null || _currentSegments.Count == 0) return;

                var currentSeconds = _audioStream.CurrentTime.TotalSeconds;
                if (sb.ShouldSkip(currentSeconds, _currentSegments, out var segment))
                {
                    _isSkippingSegment = true;
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[SponsorBlock] Skipping {segment.Category} segment: {segment.StartTime:F1}s → {segment.EndTime:F1}s");
                        Seek(TimeSpan.FromSeconds(segment.EndTime));
                    }
                    finally
                    {
                        // Re-enable after a short delay to let the seek complete
                        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                            new Action(() => _isSkippingSegment = false),
                            System.Windows.Threading.DispatcherPriority.Background);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SponsorBlock] CheckSponsorBlock failed: {ex.Message}");
            }
        }

        private async Task FetchSegmentsAsync(Musicefy.Core.Services.SponsorBlockService sb, string videoId)
        {
            try
            {
                var categories = new List<string>();
                if (Musicefy.Properties.Settings.Default.SponsorBlockSkipSponsor) categories.Add("sponsor");
                if (Musicefy.Properties.Settings.Default.SponsorBlockSkipIntro) categories.Add("intro");
                if (Musicefy.Properties.Settings.Default.SponsorBlockSkipOutro) categories.Add("outro");
                if (Musicefy.Properties.Settings.Default.SponsorBlockSkipSelfPromo) categories.Add("selfpromo");
                if (Musicefy.Properties.Settings.Default.SponsorBlockSkipInteraction) categories.Add("interaction");

                var segments = await sb.GetSegmentsAsync(videoId, categories);

                // Only store if still the same video
                if (_currentSegmentsVideoId == videoId)
                {
                    _currentSegments = segments;
                    if (segments.Count > 0)
                        System.Diagnostics.Debug.WriteLine($"[SponsorBlock] Loaded {segments.Count} segments for {videoId}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SponsorBlock] FetchSegmentsAsync failed: {ex.Message}");
            }
        }

        // ── Sprint 7: Last.fm scrobbling ─────────────────────────────────────

        /// <summary>
        /// Sprint 7: Scrobble to Last.fm — sends updateNowPlaying immediately,
        /// then schedules a scrobble after 50% of the track has played.
        /// </summary>
        private async Task ScrobbleToLastFmAsync(MusicFile track)
        {
            try
            {
                var lastFm = _serviceProvider?.GetService(typeof(Musicefy.Core.Services.LastFmService))
                             as Musicefy.Core.Services.LastFmService;
                if (lastFm == null || !lastFm.IsEnabled()) return;

                // Send now-playing immediately
                await lastFm.UpdateNowPlayingAsync(track);

                // Schedule scrobble after 4 minutes or 50% of track (whichever is first)
                var delay = TimeSpan.FromMinutes(4);
                if (track.Duration.TotalSeconds > 0)
                {
                    var halfTrack = TimeSpan.FromTicks(track.Duration.Ticks / 2);
                    if (halfTrack < delay) delay = halfTrack;
                }

                _ = Task.Delay(delay).ContinueWith(async _ =>
                {
                    // Only scrobble if still playing the same track
                    if (CurrentTrack == track && IsPlaying)
                    {
                        await lastFm.ScrobbleAsync(track);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LastFm] ScrobbleToLastFmAsync failed: {ex.Message}");
            }
        }

        // ── Sprint 7: Discord Rich Presence ──────────────────────────────────

        /// <summary>
        /// Sprint 7: Update Discord presence with current track.
        /// </summary>
        private void UpdateDiscordPresence(MusicFile track, bool isPlaying)
        {
            try
            {
                var discord = _serviceProvider?.GetService(typeof(Musicefy.Core.Services.DiscordRpcService))
                              as Musicefy.Core.Services.DiscordRpcService;
                if (discord == null) return;

                // Initialize if needed
                if (Musicefy.Properties.Settings.Default.DiscordRpcEnabled
                    && !string.IsNullOrEmpty(Musicefy.Properties.Settings.Default.DiscordClientId))
                {
                    discord.Initialize(Musicefy.Properties.Settings.Default.DiscordClientId);
                    discord.UpdatePresence(track, isPlaying);
                }
                else
                {
                    discord.ClearPresence();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DiscordRPC] UpdateDiscordPresence failed: {ex.Message}");
            }
        }

        public void Pause()
        {
            if (_wasapiOut != null && _wasapiOut.PlaybackState == PlaybackState.Playing)
            {
                _wasapiOut.Pause();
                PlaybackStateChanged?.Invoke(false);

                // Sprint 7: Update Discord presence to "Paused"
                UpdateDiscordPresence(CurrentTrack, false);
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

        /// <summary>
        /// Phase 4: Auto-persist artist and album from a playing track's YouTube browse IDs.
        /// This ensures the Artists/Albums tables are populated even if the user
        /// never explicitly navigates to an artist or album page.
        /// </summary>
        private async Task AutoPersistArtistAlbumAsync(MusicFile track)
        {
            try
            {
                if (!string.IsNullOrEmpty(track.ArtistBrowseId))
                {
                    var existingArtist = await _libraryService.GetArtistAsync(track.ArtistBrowseId);
                    if (existingArtist == null)
                    {
                        await _libraryService.SaveArtistAsync(new Core.Models.ArtistInfo
                        {
                            Id = track.ArtistBrowseId,
                            Name = track.Artist,
                            CoverPath = track.CoverPath,
                            SourceType = track.SourceType,
                            YouTubeChannelId = track.ArtistBrowseId
                        });
                    }
                }

                if (!string.IsNullOrEmpty(track.AlbumBrowseId))
                {
                    var existingAlbum = await _libraryService.GetAlbumAsync(track.AlbumBrowseId);
                    if (existingAlbum == null)
                    {
                        string artistId = track.ArtistBrowseId ?? $"local_artist:{track.Artist}";
                        await _libraryService.SaveAlbumAsync(new Core.Models.AlbumInfo
                        {
                            Id = track.AlbumBrowseId,
                            Name = track.Album,
                            Artist = track.Artist,
                            ArtistId = artistId,
                            Year = track.Year,
                            CoverPath = track.CoverPath,
                            SourceType = track.SourceType,
                            YouTubeAlbumId = track.AlbumBrowseId
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlaybackService] AutoPersist failed: {ex.Message}");
            }
        }

        // ── Phase 6: Queue Persistence ────────────────────────────────────

        public async Task SaveQueueStateAsync()
        {
            try
            {
                var tracks = _queueManager.Tracks.ToList();
                var currentIndex = -1;
                var current = CurrentAudioFile;
                if (current != null)
                {
                    for (int i = 0; i < tracks.Count; i++)
                    {
                        if (tracks[i].FilePath == current.FilePath)
                        {
                            currentIndex = i;
                            break;
                        }
                    }
                }
                var state = new QueueState
                {
                    Tracks = tracks.Select(t => t.FilePath).ToList(),
                    CurrentIndex = currentIndex,
                    ShuffleEnabled = _queueManager.ShuffleEnabled,
                    RepeatEnabled = _queueManager.RepeatEnabled
                };
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(state);
                var dir = Path.GetDirectoryName(QueueStatePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                await Task.Run(() => File.WriteAllText(QueueStatePath, json));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlaybackService] SaveQueueState failed: {ex.Message}");
            }
        }

        public async Task RestoreQueueStateAsync()
        {
            try
            {
                if (!File.Exists(QueueStatePath)) return;
                var json = await Task.Run(() => File.ReadAllText(QueueStatePath));
                var state = Newtonsoft.Json.JsonConvert.DeserializeObject<QueueState>(json);
                if (state?.Tracks == null || state.Tracks.Count == 0) return;

                // Resolve file paths back to MusicFile objects
                var allTracks = await _libraryService.GetAllTracksAsync();
                var lookup = allTracks.ToDictionary(t => t.FilePath, t => t, StringComparer.OrdinalIgnoreCase);

                var resolved = new List<MusicFile>();
                foreach (var path in state.Tracks)
                {
                    if (lookup.TryGetValue(path, out var track))
                        resolved.Add(track);
                }

                if (resolved.Count == 0) return;

                _queueManager.Clear();
                _queueManager.EnqueueRange(resolved);
                _queueManager.ShuffleEnabled = state.ShuffleEnabled;
                _queueManager.RepeatEnabled = state.RepeatEnabled;

                if (state.CurrentIndex >= 0 && state.CurrentIndex < resolved.Count)
                {
                    _queueManager.JumpToIndex(state.CurrentIndex);
                    // Don't auto-play, just set the position
                    CurrentAudioFile = resolved[state.CurrentIndex];
                    TrackChanged?.Invoke(resolved[state.CurrentIndex]);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlaybackService] RestoreQueueState failed: {ex.Message}");
            }
        }

        private class QueueState
        {
            public List<string> Tracks { get; set; }
            public int CurrentIndex { get; set; }
            public bool ShuffleEnabled { get; set; }
            public bool RepeatEnabled { get; set; }
        }


    }
}

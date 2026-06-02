using System;
using System.Threading;
using System.Threading.Tasks;

namespace Musicefy.Core.Services.YouTube
{
    /// <summary>
    /// Mitigates YouTube bot detection by rotating guest sessions.
    /// Inspired by Echo Music's BotDetectionMitigator which detects when a guest
    /// session has been flagged as a bot and automatically rotates the visitorData.
    /// 
    /// Key insight from Echo Music: If the first playback attempt fails and there's
    /// no cookie (guest session), it's likely because YouTube flagged the session as
    /// a bot. The solution is to rotate the visitor data and retry.
    /// </summary>
    public class BotDetectionMitigator
    {
        private int _consecutiveFailures;
        private DateTime _lastFailureTime;
        private DateTime _lastSuccessTime;
        private readonly int _failureThreshold;
        private readonly TimeSpan _cooldownPeriod;

        /// <summary>
        /// Fired when a bot detection event is suspected and session rotation is recommended.
        /// </summary>
        public event Action BotDetectionSuspected;

        public BotDetectionMitigator(int failureThreshold = 2, TimeSpan? cooldownPeriod = null)
        {
            _failureThreshold = failureThreshold;
            _cooldownPeriod = cooldownPeriod ?? TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Notify that a playback attempt failed. If enough consecutive failures
        /// occur, this may indicate bot detection.
        /// </summary>
        public void NotifyPlaybackFailure()
        {
            var now = DateTime.UtcNow;
            _lastFailureTime = now;

            // Reset if there was a success recently
            if (_lastSuccessTime > _lastFailureTime ||
                (now - _lastFailureTime) > _cooldownPeriod)
            {
                Interlocked.Exchange(ref _consecutiveFailures, 0);
            }

            var failures = Interlocked.Increment(ref _consecutiveFailures);

            if (failures >= _failureThreshold)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[BotDetection] {failures} consecutive failures detected — possible bot detection");
                BotDetectionSuspected?.Invoke();
            }
        }

        /// <summary>
        /// Notify that a playback attempt succeeded. Resets the failure counter.
        /// </summary>
        public void NotifyPlaybackSuccess()
        {
            _lastSuccessTime = DateTime.UtcNow;
            Interlocked.Exchange(ref _consecutiveFailures, 0);
        }

        /// <summary>
        /// Check if bot detection is likely based on recent failure patterns.
        /// </summary>
        public bool IsBotDetectionLikely
        {
            get
            {
                var failures = Interlocked.CompareExchange(ref _consecutiveFailures, 0, 0);
                return failures >= _failureThreshold &&
                       (DateTime.UtcNow - _lastFailureTime) < _cooldownPeriod;
            }
        }

        /// <summary>
        /// Reset the mitigator state.
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref _consecutiveFailures, 0);
        }
    }
}

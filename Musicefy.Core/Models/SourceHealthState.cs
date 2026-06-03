using System;

namespace Musicefy.Core.Models
{
    public enum SourceHealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy,
        PermanentlyUnhealthy
    }

    public class SourceHealthState
    {
        public string SourceId { get; set; }
        public SourceHealthStatus Status { get; set; } = SourceHealthStatus.Healthy;
        public int ConsecutiveFailures { get; set; }
        public DateTime? LastSuccessfulConnection { get; set; }
        public DateTime? LastHealthCheck { get; set; }
        public TimeSpan CurrentRetryDelay { get; set; } = TimeSpan.FromSeconds(30);
        public string LastErrorMessage { get; set; }

        public TimeSpan GetNextRetryDelay()
        {
            // Exponential backoff: 30s, 60s, 120s, 300s, 600s
            var delays = new[] { 30, 60, 120, 300, 600 };
            var index = Math.Min(ConsecutiveFailures, delays.Length - 1);
            return TimeSpan.FromSeconds(delays[index]);
        }

        public void RecordSuccess()
        {
            Status = SourceHealthStatus.Healthy;
            ConsecutiveFailures = 0;
            LastSuccessfulConnection = DateTime.UtcNow;
            CurrentRetryDelay = TimeSpan.FromSeconds(30);
            LastErrorMessage = null;
        }

        public void RecordFailure(string errorMessage)
        {
            ConsecutiveFailures++;
            LastErrorMessage = errorMessage;
            LastHealthCheck = DateTime.UtcNow;

            if (ConsecutiveFailures >= 5)
                Status = SourceHealthStatus.PermanentlyUnhealthy;
            else if (ConsecutiveFailures >= 3)
                Status = SourceHealthStatus.Unhealthy;
            else
                Status = SourceHealthStatus.Degraded;

            CurrentRetryDelay = GetNextRetryDelay();
        }
    }
}

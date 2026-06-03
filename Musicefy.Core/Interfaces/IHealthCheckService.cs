using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Musicefy.Core.Models;

namespace Musicefy.Core.Interfaces
{
    public interface IHealthCheckService : IDisposable
    {
        /// <summary>Start monitoring all connected sources</summary>
        void StartMonitoring();

        /// <summary>Stop all monitoring</summary>
        void StopMonitoring();

        /// <summary>Get the current health state for a source</summary>
        SourceHealthState GetHealthState(string sourceId);

        /// <summary>Get all source health states</summary>
        IReadOnlyDictionary<string, SourceHealthState> GetAllHealthStates();

        /// <summary>Force an immediate health check for a specific source</summary>
        Task<SourceHealthState> CheckNowAsync(string sourceId);

        /// <summary>Reset a permanently unhealthy source to allow retry</summary>
        void ResetSource(string sourceId);

        /// <summary>Fired when a source transitions from unhealthy to healthy</summary>
        event EventHandler<SourceHealthEventArgs> SourceReconnected;

        /// <summary>Fired when a source transitions from healthy to unhealthy</summary>
        event EventHandler<SourceHealthEventArgs> SourceUnhealthy;

        /// <summary>Fired when any source health state changes</summary>
        event EventHandler<SourceHealthEventArgs> SourceHealthChanged;
    }

    public class SourceHealthEventArgs : EventArgs
    {
        public string SourceId { get; set; }
        public string SourceName { get; set; }
        public SourceHealthStatus PreviousStatus { get; set; }
        public SourceHealthStatus NewStatus { get; set; }
        public string ErrorMessage { get; set; }
    }
}

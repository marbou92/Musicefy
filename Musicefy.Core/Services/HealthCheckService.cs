using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;

namespace Musicefy.Core.Services
{
    /// <summary>
    /// Health check service that monitors all connected streaming sources.
    /// Uses periodic health checks with exponential backoff retry logic.
    /// Auto-reconnects sessions when previously unhealthy sources become healthy again.
    /// </summary>
    public class HealthCheckService : IHealthCheckService
    {
        private readonly IStreamingSourceManager _sourceManager;
        private readonly Dictionary<string, SourceHealthState> _healthStates = new Dictionary<string, SourceHealthState>();
        private Timer _monitoringTimer;
        private readonly object _lock = new object();
        private bool _isMonitoring;
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);

        public event EventHandler<SourceHealthEventArgs> SourceReconnected;
        public event EventHandler<SourceHealthEventArgs> SourceUnhealthy;
        public event EventHandler<SourceHealthEventArgs> SourceHealthChanged;

        public HealthCheckService(IStreamingSourceManager sourceManager)
        {
            _sourceManager = sourceManager ?? throw new ArgumentNullException(nameof(sourceManager));
        }

        public void StartMonitoring()
        {
            lock (_lock)
            {
                if (_isMonitoring) return;
                _isMonitoring = true;

                // Initialize health states for all current sources
                foreach (var source in _sourceManager.Sources)
                {
                    if (!_healthStates.ContainsKey(source.Id))
                    {
                        _healthStates[source.Id] = new SourceHealthState
                        {
                            SourceId = source.Id,
                            Status = source.IsConnected ? SourceHealthStatus.Healthy : SourceHealthStatus.Unhealthy,
                            LastSuccessfulConnection = source.IsConnected ? DateTime.UtcNow : (DateTime?)null
                        };
                    }
                }

                // Start periodic monitoring (every 60 seconds)
                _monitoringTimer = new Timer(async _ => await PerformHealthChecksAsync(),
                    null, CheckInterval, CheckInterval);

                System.Diagnostics.Debug.WriteLine("[HealthCheckService] Monitoring started");
            }
        }

        public void StopMonitoring()
        {
            lock (_lock)
            {
                if (!_isMonitoring) return;
                _isMonitoring = false;

                _monitoringTimer?.Dispose();
                _monitoringTimer = null;

                System.Diagnostics.Debug.WriteLine("[HealthCheckService] Monitoring stopped");
            }
        }

        public SourceHealthState GetHealthState(string sourceId)
        {
            lock (_lock)
            {
                return _healthStates.TryGetValue(sourceId, out var state) ? state : null;
            }
        }

        public IReadOnlyDictionary<string, SourceHealthState> GetAllHealthStates()
        {
            lock (_lock)
            {
                return new Dictionary<string, SourceHealthState>(_healthStates);
            }
        }

        public async Task<SourceHealthState> CheckNowAsync(string sourceId)
        {
            var source = _sourceManager.GetSource(sourceId);
            if (source == null) return null;

            SourceHealthState state;
            lock (_lock)
            {
                if (!_healthStates.TryGetValue(sourceId, out state))
                {
                    state = new SourceHealthState { SourceId = sourceId };
                    _healthStates[sourceId] = state;
                }
            }

            await CheckSourceHealthAsync(source, state);
            return state;
        }

        public void ResetSource(string sourceId)
        {
            lock (_lock)
            {
                if (_healthStates.TryGetValue(sourceId, out var state))
                {
                    var previousStatus = state.Status;
                    state.Status = SourceHealthStatus.Degraded;
                    state.ConsecutiveFailures = 0;
                    state.CurrentRetryDelay = TimeSpan.FromSeconds(30);
                    state.LastErrorMessage = null;

                    OnSourceHealthChanged(new SourceHealthEventArgs
                    {
                        SourceId = sourceId,
                        PreviousStatus = previousStatus,
                        NewStatus = state.Status
                    });
                }
            }
        }

        private async Task PerformHealthChecksAsync()
        {
            try
            {
                var sources = _sourceManager.Sources.ToList();
                var tasks = sources.Select(source => CheckSourceWithLockAsync(source));
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HealthCheckService] Error during health checks: {ex.Message}");
            }
        }

        private async Task CheckSourceWithLockAsync(StreamingSource source)
        {
            SourceHealthState state;
            lock (_lock)
            {
                if (!_healthStates.TryGetValue(source.Id, out state))
                {
                    state = new SourceHealthState { SourceId = source.Id };
                    _healthStates[source.Id] = state;
                }
            }

            await CheckSourceHealthAsync(source, state);
        }

        private async Task CheckSourceHealthAsync(StreamingSource source, SourceHealthState state)
        {
            // Skip permanently unhealthy sources unless enough time has passed for a retry
            if (state.Status == SourceHealthStatus.PermanentlyUnhealthy)
            {
                if (state.LastHealthCheck.HasValue &&
                    DateTime.UtcNow - state.LastHealthCheck.Value < state.CurrentRetryDelay)
                {
                    return; // Within retry delay window, skip check
                }
            }

            // For unhealthy sources, skip check if within retry delay window
            if (state.Status == SourceHealthStatus.Unhealthy || state.Status == SourceHealthStatus.Degraded)
            {
                if (state.LastHealthCheck.HasValue &&
                    DateTime.UtcNow - state.LastHealthCheck.Value < state.CurrentRetryDelay)
                {
                    return; // Within retry delay window, skip check
                }
            }

            var previousStatus = state.Status;

            try
            {
                // Perform a lightweight connection test
                var isConnected = await _sourceManager.TestConnectionAsync(source.Id);

                if (isConnected)
                {
                    state.RecordSuccess();

                    // If this source was previously unhealthy, it's now reconnected
                    if (previousStatus != SourceHealthStatus.Healthy)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[HealthCheckService] Source '{source.Name}' reconnected (was {previousStatus})");

                        OnSourceReconnected(new SourceHealthEventArgs
                        {
                            SourceId = source.Id,
                            SourceName = source.Name,
                            PreviousStatus = previousStatus,
                            NewStatus = SourceHealthStatus.Healthy
                        });
                    }
                }
                else
                {
                    state.RecordFailure("Connection test failed");

                    System.Diagnostics.Debug.WriteLine(
                        $"[HealthCheckService] Source '{source.Name}' health check failed " +
                        $"(consecutive failures: {state.ConsecutiveFailures}, status: {state.Status})");

                    // If this source just became unhealthy, notify
                    if (previousStatus == SourceHealthStatus.Healthy ||
                        previousStatus != state.Status)
                    {
                        OnSourceUnhealthy(new SourceHealthEventArgs
                        {
                            SourceId = source.Id,
                            SourceName = source.Name,
                            PreviousStatus = previousStatus,
                            NewStatus = state.Status,
                            ErrorMessage = state.LastErrorMessage
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                state.RecordFailure(ex.Message);

                if (previousStatus == SourceHealthStatus.Healthy ||
                    previousStatus != state.Status)
                {
                    OnSourceUnhealthy(new SourceHealthEventArgs
                    {
                        SourceId = source.Id,
                        SourceName = source.Name,
                        PreviousStatus = previousStatus,
                        NewStatus = state.Status,
                        ErrorMessage = ex.Message
                    });
                }
            }

            // Always fire general health changed event if status changed
            if (previousStatus != state.Status)
            {
                OnSourceHealthChanged(new SourceHealthEventArgs
                {
                    SourceId = source.Id,
                    SourceName = source.Name,
                    PreviousStatus = previousStatus,
                    NewStatus = state.Status,
                    ErrorMessage = state.LastErrorMessage
                });
            }
        }

        protected virtual void OnSourceReconnected(SourceHealthEventArgs e)
        {
            try { SourceReconnected?.Invoke(this, e); } catch { }
        }

        protected virtual void OnSourceUnhealthy(SourceHealthEventArgs e)
        {
            try { SourceUnhealthy?.Invoke(this, e); } catch { }
        }

        protected virtual void OnSourceHealthChanged(SourceHealthEventArgs e)
        {
            try { SourceHealthChanged?.Invoke(this, e); } catch { }
        }

        public void Dispose()
        {
            StopMonitoring();
        }
    }
}

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// Wrapper ViewModel for a single streaming source with UI-friendly properties.
    /// Provides bindable properties for source health, connection status, and configuration.
    /// </summary>
    public class SourceViewModel : INotifyPropertyChanged
    {
        private bool _isConnecting;
        private bool _isExpanded;
        private string _errorMessage;
        private SourceHealthStatus _healthStatus;
        private DateTime? _lastHealthCheck;
        private bool _isHealthy;

        public StreamingSource Source { get; }
        public IMusicSourceProvider Provider { get; }

        public string Id => Source?.Id;
        public string Name => Source?.Name ?? "Unknown Source";
        public string Type => Source?.Type ?? "Unknown";
        public string IconGlyph => Provider?.IconGlyph ?? "🎵";
        public string DisplayName => Provider?.DisplayName ?? Source?.Type ?? "Unknown";
        public string Description => Provider?.Description ?? "";

        public bool IsConnected
        {
            get => Source?.IsConnected ?? false;
            set
            {
                if (Source != null) Source.IsConnected = value;
                OnPropertyChanged();
            }
        }

        public bool IsConnecting
        {
            get => _isConnecting;
            set => SetProperty(ref _isConnecting, value);
        }

        public bool IsHealthy
        {
            get => _isHealthy;
            set => SetProperty(ref _isHealthy, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public SourceHealthStatus HealthStatus
        {
            get => _healthStatus;
            set
            {
                if (SetProperty(ref _healthStatus, value))
                {
                    IsHealthy = value == SourceHealthStatus.Healthy;
                    OnPropertyChanged(nameof(HealthStatusText));
                    OnPropertyChanged(nameof(HealthStatusColor));
                }
            }
        }

        public DateTime? LastHealthCheck
        {
            get => _lastHealthCheck;
            set
            {
                if (SetProperty(ref _lastHealthCheck, value))
                {
                    OnPropertyChanged(nameof(LastHealthCheckText));
                }
            }
        }

        public string HealthStatusText
        {
            get
            {
                switch (HealthStatus)
                {
                    case SourceHealthStatus.Healthy:
                        return "Connected";
                    case SourceHealthStatus.Degraded:
                        return "Degraded";
                    case SourceHealthStatus.Unhealthy:
                        return "Unhealthy";
                    case SourceHealthStatus.PermanentlyUnhealthy:
                        return "Permanently Unhealthy";
                    default:
                        return "Unknown";
                }
            }
        }

        public string HealthStatusColor
        {
            get
            {
                switch (HealthStatus)
                {
                    case SourceHealthStatus.Healthy:
                        return "#4CAF50"; // Green
                    case SourceHealthStatus.Degraded:
                        return "#FF9800"; // Orange
                    case SourceHealthStatus.Unhealthy:
                        return "#F44336"; // Red
                    case SourceHealthStatus.PermanentlyUnhealthy:
                        return "#9E9E9E"; // Gray
                    default:
                        return "#9E9E9E";
                }
            }
        }

        public string LastHealthCheckText
        {
            get
            {
                if (!LastHealthCheck.HasValue)
                    return "Never checked";

                var elapsed = DateTime.UtcNow - LastHealthCheck.Value;
                if (elapsed.TotalMinutes < 1)
                    return "Just now";
                if (elapsed.TotalMinutes < 60)
                    return $"{(int)elapsed.TotalMinutes}m ago";
                if (elapsed.TotalHours < 24)
                    return $"{(int)elapsed.TotalHours}h ago";
                return LastHealthCheck.Value.ToLocalTime().ToString("g");
            }
        }

        public SourceViewModel(StreamingSource source, IMusicSourceProvider provider)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Provider = provider;
            _isHealthy = source.IsConnected;
            _healthStatus = source.IsConnected ? SourceHealthStatus.Healthy : SourceHealthStatus.Unhealthy;
        }

        public void UpdateHealthState(SourceHealthState healthState)
        {
            if (healthState == null) return;

            HealthStatus = healthState.Status;
            LastHealthCheck = healthState.LastHealthCheck;
            ErrorMessage = healthState.LastErrorMessage;
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// Wrapper ViewModel for a single streaming source with UI-friendly properties.
    /// Provides bindable properties for source health, connection status,
    /// configuration, and home-screen visibility.
    /// </summary>
    public class SourceViewModel : INotifyPropertyChanged
    {
        private bool _isConnecting;
        private bool _isExpanded;
        private string _errorMessage;
        private SourceHealthStatus _healthStatus;
        private DateTime? _lastHealthCheck;
        private bool _isHealthy;
        private bool _isHomeEnabled;

        public StreamingSource Source { get; }
        public IMusicSourceProvider Provider { get; }

        public string Id => Source?.Id;
        public string Name => Source?.Name ?? "Unknown Source";
        public string Type => Source?.Type ?? "Unknown";
        public string IconGlyph => Provider?.IconGlyph ?? "🎵";
        public string DisplayName => Provider?.DisplayName ?? Source?.Type ?? "Unknown";
        public string Description => Provider?.Description ?? "";

        /// <summary>
        /// True if the provider for this source's type is loaded.
        /// False for orphaned sources (e.g. Subsonic sources from a previous
        /// install, after Subsonic was removed from the built-in providers).
        /// </summary>
        public bool IsProviderMissing => Provider == null;

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

        /// <summary>
        /// Whether this source's type appears on the Home screen.
        /// Persisted via SourcesSettingsViewModel.SetHomeEnabled.
        /// </summary>
        public bool IsHomeEnabled
        {
            get => _isHomeEnabled;
            set => SetProperty(ref _isHomeEnabled, value);
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
                        return "#4CAF50";
                    case SourceHealthStatus.Degraded:
                        return "#FF9800";
                    case SourceHealthStatus.Unhealthy:
                        return "#F44336";
                    case SourceHealthStatus.PermanentlyUnhealthy:
                        return "#9E9E9E";
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
            _isHomeEnabled = SourcesSettingsViewModel.IsHomeEnabled(source.Type);
        }

        /// <summary>
        /// Re-read the IsHomeEnabled value from settings (call after toggling).
        /// </summary>
        public void RefreshHomeEnabled()
        {
            IsHomeEnabled = SourcesSettingsViewModel.IsHomeEnabled(Type);
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

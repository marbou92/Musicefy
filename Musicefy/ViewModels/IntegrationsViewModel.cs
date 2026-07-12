using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Services;
using Musicefy.Services;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// Sprint 7: ViewModel for the Integrations settings tab.
    /// Handles Last.fm and Discord RPC configuration.
    /// </summary>
    public class IntegrationsViewModel : ViewModelBase
    {
        private readonly LastFmService _lastFmService;
        private readonly DiscordRpcService _discordService;
        private bool _isBusy;
        private string _statusText = "Ready";

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        // ── Last.fm ──────────────────────────────────────────────────────────
        public bool LastFmEnabled
        {
            get => Musicefy.Properties.Settings.Default.LastFmEnabled;
            set { Musicefy.Properties.Settings.Default.LastFmEnabled = value; OnPropertyChanged(); }
        }

        public string LastFmUsername
        {
            get => Musicefy.Properties.Settings.Default.LastFmUsername ?? "";
            set { Musicefy.Properties.Settings.Default.LastFmUsername = value; OnPropertyChanged(); }
        }

        private string _lastFmPassword = "";
        public string LastFmPassword
        {
            get => _lastFmPassword;
            set => SetProperty(ref _lastFmPassword, value);
        }

        public bool LastFmAuthenticated => !string.IsNullOrEmpty(Musicefy.Properties.Settings.Default.LastFmSessionKey);

        // ── Discord RPC ──────────────────────────────────────────────────────
        public bool DiscordRpcEnabled
        {
            get => Musicefy.Properties.Settings.Default.DiscordRpcEnabled;
            set { Musicefy.Properties.Settings.Default.DiscordRpcEnabled = value; OnPropertyChanged(); }
        }

        public string DiscordClientId
        {
            get => Musicefy.Properties.Settings.Default.DiscordClientId ?? "";
            set { Musicefy.Properties.Settings.Default.DiscordClientId = value; OnPropertyChanged(); }
        }

        // ── Commands ─────────────────────────────────────────────────────────
        public ICommand SaveCommand { get; }
        public ICommand LastFmLoginCommand { get; }
        public ICommand LastFmLogoutCommand { get; }

        public IntegrationsViewModel(
            LastFmService lastFmService,
            DiscordRpcService discordService)
        {
            _lastFmService = lastFmService ?? throw new ArgumentNullException(nameof(lastFmService));
            _discordService = discordService ?? throw new ArgumentNullException(nameof(discordService));

            SaveCommand = new RelayCommand(_ => Save());
            LastFmLoginCommand = new RelayCommand(async _ => await LastFmLoginAsync());
            LastFmLogoutCommand = new RelayCommand(_ => LastFmLogout());
        }

        public IntegrationsViewModel() : this(
            App.Services.GetService<LastFmService>(),
            App.Services.GetService<DiscordRpcService>())
        {
        }

        private void Save()
        {
            Musicefy.Properties.Settings.Default.Save();

            // Initialize Discord RPC if enabled
            if (DiscordRpcEnabled && !string.IsNullOrEmpty(DiscordClientId))
            {
                _discordService.Initialize(DiscordClientId);
            }

            ToastService.ShowToast("Integrations saved.", System.Windows.Media.Brushes.ForestGreen);
        }

        private async Task LastFmLoginAsync()
        {
            if (string.IsNullOrEmpty(LastFmUsername) || string.IsNullOrEmpty(LastFmPassword))
            {
                StatusText = "Enter username and password.";
                return;
            }

            IsBusy = true;
            StatusText = "Authenticating with Last.fm…";

            try
            {
                var sessionKey = await _lastFmService.AuthenticateAsync(LastFmUsername, LastFmPassword);
                Musicefy.Properties.Settings.Default.LastFmSessionKey = sessionKey;
                Musicefy.Properties.Settings.Default.LastFmUsername = LastFmUsername;
                Musicefy.Properties.Settings.Default.LastFmEnabled = true;
                Musicefy.Properties.Settings.Default.Save();

                LastFmPassword = "";
                StatusText = "Connected to Last.fm!";
                OnPropertyChanged(nameof(LastFmAuthenticated));
                ToastService.ShowToast("Last.fm connected successfully.", System.Windows.Media.Brushes.ForestGreen);
            }
            catch (Exception ex)
            {
                StatusText = $"Last.fm login failed: {ex.Message}";
                ToastService.ShowToast($"Last.fm login failed: {ex.Message}", System.Windows.Media.Brushes.OrangeRed);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void LastFmLogout()
        {
            Musicefy.Properties.Settings.Default.LastFmSessionKey = "";
            Musicefy.Properties.Settings.Default.LastFmEnabled = false;
            Musicefy.Properties.Settings.Default.Save();

            StatusText = "Disconnected from Last.fm.";
            OnPropertyChanged(nameof(LastFmAuthenticated));
            OnPropertyChanged(nameof(LastFmEnabled));
            ToastService.ShowToast("Disconnected from Last.fm.", System.Windows.Media.Brushes.Gray);
        }
    }
}

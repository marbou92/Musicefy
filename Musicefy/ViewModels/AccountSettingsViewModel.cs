using System;
using System.Linq;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Interfaces;
using Musicefy.Services;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// Sprint 8.5: ViewModel for the Account settings tab.
    /// Handles YouTube Music login, Discord RPC, and Last.fm scrobbling.
    /// All settings auto-save on change — no Save button.
    /// </summary>
    public class AccountSettingsViewModel : ViewModelBase
    {
        // ── Navigation state ─────────────────────────────────────────────────
        private enum AccountView { Main, Discord, LastFm }
        private AccountView _currentView = AccountView.Main;

        public bool IsMainView => _currentView == AccountView.Main;
        public bool IsDiscordView => _currentView == AccountView.Discord;
        public bool IsLastFmView => _currentView == AccountView.LastFm;

        public ICommand NavigateToDiscordCommand { get; }
        public ICommand NavigateToLastFmCommand { get; }
        public ICommand NavigateBackCommand { get; }

        // ── YouTube Music ────────────────────────────────────────────────────
        public bool YouTubeEnabled
        {
            get => Musicefy.Properties.Settings.Default.YouTubeEnabled;
            set { Musicefy.Properties.Settings.Default.YouTubeEnabled = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public string YouTubeApiKey
        {
            get => Musicefy.Properties.Settings.Default.YouTubeApiKey ?? "";
            set { Musicefy.Properties.Settings.Default.YouTubeApiKey = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); UpdateYouTubeSource(); }
        }

        public string YouTubeCookie
        {
            get => Musicefy.Properties.Settings.Default.YouTubeCookie ?? "";
            set { Musicefy.Properties.Settings.Default.YouTubeCookie = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); UpdateYouTubeSource(); }
        }

        public int YouTubeAudioQualityIndex
        {
            get => string.Equals(Musicefy.Properties.Settings.Default.YouTubeAudioQuality, "aac", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            set
            {
                var newQuality = value == 1 ? "aac" : "opus";
                Musicefy.Properties.Settings.Default.YouTubeAudioQuality = newQuality;
                Musicefy.Properties.Settings.Default.Save();
                OnPropertyChanged();
                UpdateYouTubeSource();
            }
        }

        // ── Discord RPC ──────────────────────────────────────────────────────
        public bool DiscordRpcEnabled
        {
            get => Musicefy.Properties.Settings.Default.DiscordRpcEnabled;
            set { Musicefy.Properties.Settings.Default.DiscordRpcEnabled = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public string DiscordClientId
        {
            get => Musicefy.Properties.Settings.Default.DiscordClientId ?? "";
            set { Musicefy.Properties.Settings.Default.DiscordClientId = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        // ── Last.fm ──────────────────────────────────────────────────────────
        public bool LastFmEnabled
        {
            get => Musicefy.Properties.Settings.Default.LastFmEnabled;
            set { Musicefy.Properties.Settings.Default.LastFmEnabled = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public string LastFmUsername
        {
            get => Musicefy.Properties.Settings.Default.LastFmUsername ?? "";
            set { Musicefy.Properties.Settings.Default.LastFmUsername = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        private string _lastFmPassword = "";
        public string LastFmPassword
        {
            get => _lastFmPassword;
            set => SetProperty(ref _lastFmPassword, value);
        }

        public bool LastFmAuthenticated => !string.IsNullOrEmpty(Musicefy.Properties.Settings.Default.LastFmSessionKey);

        // ── Login commands ───────────────────────────────────────────────────
        public ICommand LastFmLoginCommand { get; }
        public ICommand LastFmLogoutCommand { get; }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _statusText = "";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public AccountSettingsViewModel()
        {
            NavigateToDiscordCommand = new RelayCommand(_ => NavigateTo(AccountView.Discord));
            NavigateToLastFmCommand = new RelayCommand(_ => NavigateTo(AccountView.LastFm));
            NavigateBackCommand = new RelayCommand(_ => NavigateTo(AccountView.Main));
            LastFmLoginCommand = new RelayCommand(async _ => await LastFmLoginAsync());
            LastFmLogoutCommand = new RelayCommand(_ => LastFmLogout());
        }

        private void NavigateTo(AccountView view)
        {
            _currentView = view;
            OnPropertyChanged(nameof(IsMainView));
            OnPropertyChanged(nameof(IsDiscordView));
            OnPropertyChanged(nameof(IsLastFmView));
        }

        private void UpdateYouTubeSource()
        {
            try
            {
                var sourceManager = App.Services?.GetService(typeof(IStreamingSourceManager))
                                     as IStreamingSourceManager;
                if (sourceManager != null)
                {
                    var ytSource = sourceManager.Sources.FirstOrDefault(
                        s => string.Equals(s.Type, Musicefy.Core.SourceTypes.YouTube, StringComparison.OrdinalIgnoreCase));
                    if (ytSource != null)
                    {
                        ytSource.Configuration["apiKey"] = YouTubeApiKey ?? "";
                        ytSource.Configuration["cookie"] = YouTubeCookie ?? "";
                        ytSource.Configuration["audioQuality"] = Musicefy.Properties.Settings.Default.YouTubeAudioQuality ?? "opus";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountSettings] UpdateYouTubeSource failed: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LastFmLoginAsync()
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
                var lastFm = App.Services?.GetService(typeof(Musicefy.Core.Services.LastFmService))
                             as Musicefy.Core.Services.LastFmService;
                if (lastFm == null) return;

                var sessionKey = await lastFm.AuthenticateAsync(LastFmUsername, LastFmPassword);
                Musicefy.Properties.Settings.Default.LastFmSessionKey = sessionKey;
                Musicefy.Properties.Settings.Default.LastFmUsername = LastFmUsername;
                Musicefy.Properties.Settings.Default.LastFmEnabled = true;
                Musicefy.Properties.Settings.Default.Save();

                LastFmPassword = "";
                StatusText = "Connected to Last.fm!";
                OnPropertyChanged(nameof(LastFmAuthenticated));
                OnPropertyChanged(nameof(LastFmEnabled));
                ToastService.ShowToast("Last.fm connected successfully.", System.Windows.Media.Brushes.ForestGreen);
            }
            catch (Exception ex)
            {
                StatusText = $"Login failed: {ex.Message}";
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

            StatusText = "";
            OnPropertyChanged(nameof(LastFmAuthenticated));
            OnPropertyChanged(nameof(LastFmEnabled));
            ToastService.ShowToast("Disconnected from Last.fm.", System.Windows.Media.Brushes.Gray);
        }
    }
}

using System;
using System.Windows.Input;
using Musicefy.Services;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// Sprint 8: ViewModel for the Player & Audio settings tab.
    /// Handles Skip Silence, Crossfade, and SponsorBlock settings.
    /// </summary>
    public class PlayerAudioSettingsViewModel : ViewModelBase
    {
        // ── Skip Silence ─────────────────────────────────────────────────────
        public bool SkipSilenceEnabled
        {
            get => Musicefy.Properties.Settings.Default.SkipSilenceEnabled;
            set { Musicefy.Properties.Settings.Default.SkipSilenceEnabled = value; OnPropertyChanged(); }
        }

        public int SkipSilenceThresholdDb
        {
            get => Musicefy.Properties.Settings.Default.SkipSilenceThresholdDb;
            set { Musicefy.Properties.Settings.Default.SkipSilenceThresholdDb = value; OnPropertyChanged(); }
        }

        // ── Crossfade ────────────────────────────────────────────────────────
        public bool CrossfadeEnabled
        {
            get => Musicefy.Properties.Settings.Default.CrossfadeEnabled;
            set { Musicefy.Properties.Settings.Default.CrossfadeEnabled = value; OnPropertyChanged(); }
        }

        public double CrossfadeDurationSeconds
        {
            get => Musicefy.Properties.Settings.Default.CrossfadeDurationSeconds;
            set { Musicefy.Properties.Settings.Default.CrossfadeDurationSeconds = value; OnPropertyChanged(); }
        }

        // ── SponsorBlock ─────────────────────────────────────────────────────
        public bool SponsorBlockEnabled
        {
            get => Musicefy.Properties.Settings.Default.SponsorBlockEnabled;
            set { Musicefy.Properties.Settings.Default.SponsorBlockEnabled = value; OnPropertyChanged(); }
        }

        public bool SponsorBlockSkipSponsor
        {
            get => Musicefy.Properties.Settings.Default.SponsorBlockSkipSponsor;
            set { Musicefy.Properties.Settings.Default.SponsorBlockSkipSponsor = value; OnPropertyChanged(); }
        }

        public bool SponsorBlockSkipIntro
        {
            get => Musicefy.Properties.Settings.Default.SponsorBlockSkipIntro;
            set { Musicefy.Properties.Settings.Default.SponsorBlockSkipIntro = value; OnPropertyChanged(); }
        }

        public bool SponsorBlockSkipOutro
        {
            get => Musicefy.Properties.Settings.Default.SponsorBlockSkipOutro;
            set { Musicefy.Properties.Settings.Default.SponsorBlockSkipOutro = value; OnPropertyChanged(); }
        }

        public bool SponsorBlockSkipSelfPromo
        {
            get => Musicefy.Properties.Settings.Default.SponsorBlockSkipSelfPromo;
            set { Musicefy.Properties.Settings.Default.SponsorBlockSkipSelfPromo = value; OnPropertyChanged(); }
        }

        public bool SponsorBlockSkipInteraction
        {
            get => Musicefy.Properties.Settings.Default.SponsorBlockSkipInteraction;
            set { Musicefy.Properties.Settings.Default.SponsorBlockSkipInteraction = value; OnPropertyChanged(); }
        }

        // ── Expandable section state ─────────────────────────────────────────
        private bool _sponsorBlockExpanded;
        public bool SponsorBlockExpanded
        {
            get => _sponsorBlockExpanded;
            set => SetProperty(ref _sponsorBlockExpanded, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand ToggleSponsorBlockExpandedCommand { get; }

        public PlayerAudioSettingsViewModel()
        {
            SaveCommand = new RelayCommand(_ => Save());
            ToggleSponsorBlockExpandedCommand = new RelayCommand(_ => SponsorBlockExpanded = !SponsorBlockExpanded);
        }

        private void Save()
        {
            Musicefy.Properties.Settings.Default.Save();

            // Clear SponsorBlock cache so new category settings take effect
            try
            {
                var sb = App.Services?.GetService(typeof(Musicefy.Core.Services.SponsorBlockService))
                         as Musicefy.Core.Services.SponsorBlockService;
                sb?.ClearCache();
            }
            catch { }

            ToastService.ShowToast("Player & audio settings saved.", System.Windows.Media.Brushes.ForestGreen);
        }
    }
}

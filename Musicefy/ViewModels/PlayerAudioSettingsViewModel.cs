using System;
using System.Windows.Input;
using Musicefy.Services;

namespace Musicefy.ViewModels
{
    /// <summary>
    /// Sprint 8.5: ViewModel for the Player & Audio settings tab.
    /// All settings auto-save on change — no Save button.
    /// </summary>
    public class PlayerAudioSettingsViewModel : ViewModelBase
    {
        // ── Skip Silence ─────────────────────────────────────────────────────
        public bool SkipSilenceEnabled
        {
            get => Musicefy.Properties.Settings.Default.SkipSilenceEnabled;
            set { Musicefy.Properties.Settings.Default.SkipSilenceEnabled = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public int SkipSilenceThresholdDb
        {
            get => Musicefy.Properties.Settings.Default.SkipSilenceThresholdDb;
            set { Musicefy.Properties.Settings.Default.SkipSilenceThresholdDb = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        // ── Crossfade ────────────────────────────────────────────────────────
        public bool CrossfadeEnabled
        {
            get => Musicefy.Properties.Settings.Default.CrossfadeEnabled;
            set { Musicefy.Properties.Settings.Default.CrossfadeEnabled = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public double CrossfadeDurationSeconds
        {
            get => Musicefy.Properties.Settings.Default.CrossfadeDurationSeconds;
            set { Musicefy.Properties.Settings.Default.CrossfadeDurationSeconds = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        // ── SponsorBlock ─────────────────────────────────────────────────────
        public bool SponsorBlockEnabled
        {
            get => Musicefy.Properties.Settings.Default.SponsorBlockEnabled;
            set
            {
                Musicefy.Properties.Settings.Default.SponsorBlockEnabled = value;
                Musicefy.Properties.Settings.Default.Save();
                OnPropertyChanged();

                // Fix: Expand when enabling, collapse when disabling
                SponsorBlockExpanded = value;
            }
        }

        public bool SponsorBlockSkipSponsor
        {
            get => Musicefy.Properties.Settings.Default.SponsorBlockSkipSponsor;
            set { Musicefy.Properties.Settings.Default.SponsorBlockSkipSponsor = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public bool SponsorBlockSkipIntro
        {
            get => Musicefy.Properties.Settings.Default.SponsorBlockSkipIntro;
            set { Musicefy.Properties.Settings.Default.SponsorBlockSkipIntro = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public bool SponsorBlockSkipOutro
        {
            get => Musicefy.Properties.Settings.Default.SponsorBlockSkipOutro;
            set { Musicefy.Properties.Settings.Default.SponsorBlockSkipOutro = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public bool SponsorBlockSkipSelfPromo
        {
            get => Musicefy.Properties.Settings.Default.SponsorBlockSkipSelfPromo;
            set { Musicefy.Properties.Settings.Default.SponsorBlockSkipSelfPromo = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        public bool SponsorBlockSkipInteraction
        {
            get => Musicefy.Properties.Settings.Default.SponsorBlockSkipInteraction;
            set { Musicefy.Properties.Settings.Default.SponsorBlockSkipInteraction = value; Musicefy.Properties.Settings.Default.Save(); OnPropertyChanged(); }
        }

        // ── Expandable section state ─────────────────────────────────────────
        private bool _sponsorBlockExpanded;
        public bool SponsorBlockExpanded
        {
            get => _sponsorBlockExpanded;
            set => SetProperty(ref _sponsorBlockExpanded, value);
        }

        public ICommand ToggleSponsorBlockExpandedCommand { get; }

        public PlayerAudioSettingsViewModel()
        {
            ToggleSponsorBlockExpandedCommand = new RelayCommand(_ => SponsorBlockExpanded = !SponsorBlockExpanded);
        }
    }
}

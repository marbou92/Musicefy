using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Interfaces;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class SettingsPage : UserControl, INotifyPropertyChanged
    {
        private AppearanceSettingsViewModel _appearanceVM;
        private bool _initialLoadPending = true;
        private string _searchQuery = "";
        private ObservableCollection<SettingsSearchItem> _searchResults = new ObservableCollection<SettingsSearchItem>();

        /// <summary>
        /// Sprint 4: Search query for the settings search bar.
        /// When non-empty, the sidebar tabs are hidden and a list of matching
        /// settings is shown instead. Clicking a result navigates to the
        /// relevant tab.
        /// </summary>
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                    OnSearchChanged();
            }
        }

        public ObservableCollection<SettingsSearchItem> SearchResults
        {
            get => _searchResults;
            set => SetProperty(ref _searchResults, value);
        }

        public SettingsPage()
        {
            InitializeComponent();
            DataContext = this;
            this.Loaded += SettingsPage_Loaded;

            try
            {
                ShowAppearance();
                _initialLoadPending = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Constructor init failed: {ex.Message}");
                ShowFallbackContent($"Error loading Appearance: {ex.Message}");
            }
        }

        private void SettingsPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_initialLoadPending)
            {
                _initialLoadPending = false;
                try { ShowAppearance(); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Loaded init failed: {ex.Message}");
                    ShowFallbackContent($"Error loading Appearance: {ex.Message}");
                }
            }
        }

        // ── Search ────────────────────────────────────────────────────────────

        private void OnSearchChanged()
        {
            var q = (SearchQuery ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrEmpty(q))
            {
                SearchResults.Clear();
                SearchResultsPanel.Visibility = Visibility.Collapsed;
                TabsPanel.Visibility = Visibility.Visible;
                return;
            }

            SearchResults.Clear();
            foreach (var item in BuildSearchIndex().Where(i =>
                i.Label.ToLowerInvariant().Contains(q) ||
                i.Category.ToLowerInvariant().Contains(q)))
            {
                SearchResults.Add(item);
            }

            SearchResultsPanel.Visibility = SearchResults.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            TabsPanel.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Build the flat search index of all settings labels.
        /// Mirrors Echo Music's SearchableSettings.kt approach.
        /// </summary>
        private IEnumerable<SettingsSearchItem> BuildSearchIndex()
        {
            // Appearance
            yield return new SettingsSearchItem("Theme mode (Light/Dark/AMOLED)", "Appearance", () => SwitchToTab("Appearance"));
            yield return new SettingsSearchItem("Color palette", "Appearance", () => SwitchToTab("Appearance"));
            yield return new SettingsSearchItem("Dynamic colors (album-art based)", "Appearance", () => SwitchToTab("Appearance"));
            yield return new SettingsSearchItem("Player background style", "Appearance", () => SwitchToTab("Appearance"));

            // Storage & Sources
            yield return new SettingsSearchItem("Local music folders", "Storage & Sources", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("Add local folder", "Storage & Sources", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("YouTube Music", "Storage & Sources", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("YouTube API key", "Storage & Sources", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("YouTube cookie (login)", "Storage & Sources", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("YouTube audio quality", "Storage & Sources", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("Download path", "Storage & Sources", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("Auto-clear cache on exit", "Storage & Sources", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("Limit download size", "Storage & Sources", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("Clear cache", "Storage & Sources", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("SponsorBlock", "Storage & Sources", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("Skip sponsor segments", "Storage & Sources", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("Lyrics", "Storage & Sources", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("LrcLib lyrics provider", "Storage & Sources", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("Show local music on Home", "Storage & Sources", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("Show YouTube on Home", "Storage & Sources", () => SwitchToTab("Downloads"));
            // Sprint 5
            yield return new SettingsSearchItem("Skip silence", "Storage & Sources", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("Silence threshold", "Storage & Sources", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("Crossfade", "Storage & Sources", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("Crossfade duration", "Storage & Sources", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("Playback", "Storage & Sources", () => SwitchToTab("Downloads"));
            // Sprint 5/6
            yield return new SettingsSearchItem("Listen history", "History", () => SwitchToTab("History"));
            yield return new SettingsSearchItem("Recently played", "History", () => SwitchToTab("History"));
            yield return new SettingsSearchItem("Stats", "Stats", () => SwitchToTab("Stats"));
            yield return new SettingsSearchItem("Most played", "Stats", () => SwitchToTab("Stats"));
            yield return new SettingsSearchItem("Top tracks", "Stats", () => SwitchToTab("Stats"));
            yield return new SettingsSearchItem("Top artists", "Stats", () => SwitchToTab("Stats"));
            yield return new SettingsSearchItem("Top albums", "Stats", () => SwitchToTab("Stats"));
            yield return new SettingsSearchItem("Backup", "Backup", () => SwitchToTab("Backup"));
            yield return new SettingsSearchItem("Restore", "Backup", () => SwitchToTab("Backup"));
            yield return new SettingsSearchItem("Export", "Backup", () => SwitchToTab("Backup"));
            // Sprint 7
            yield return new SettingsSearchItem("Last.fm", "Integrations", () => SwitchToTab("Integrations"));
            yield return new SettingsSearchItem("Scrobble", "Integrations", () => SwitchToTab("Integrations"));
            yield return new SettingsSearchItem("Discord", "Integrations", () => SwitchToTab("Integrations"));
            yield return new SettingsSearchItem("Discord RPC", "Integrations", () => SwitchToTab("Integrations"));
        }

        private void SwitchToTab(string tabName)
        {
            SearchQuery = "";
            switch (tabName)
            {
                case "Appearance":
                    AppearanceButton.IsChecked = true;
                    break;
                case "Downloads":
                    DownloadsButton.IsChecked = true;
                    break;
                case "History":
                    HistoryButton.IsChecked = true;
                    break;
                case "Stats":
                    StatsButton.IsChecked = true;
                    break;
                case "Backup":
                    BackupButton.IsChecked = true;
                    break;
                case "Integrations":
                    IntegrationsButton.IsChecked = true;
                    break;
            }
        }

        private void SearchResult_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is SettingsSearchItem item)
            {
                item.Navigate?.Invoke();
            }
        }

        // ── Tab handlers ──────────────────────────────────────────────────────

        private void AppearanceButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_initialLoadPending) return;
            if (AppearanceButton.IsChecked == true)
            {
                try { ShowAppearance(); }
                catch (Exception ex) { ShowFallbackContent($"Error: {ex.Message}"); }
            }
        }

        private void DownloadsButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DownloadsButton.IsChecked == true)
            {
                try { ShowDownloads(); }
                catch (Exception ex) { ShowFallbackContent($"Error: {ex.Message}"); }
            }
        }

        // ── Sprint 5: History tab ────────────────────────────────────────────
        private void HistoryButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (HistoryButton.IsChecked == true)
            {
                try { ShowHistory(); }
                catch (Exception ex) { ShowFallbackContent($"Error: {ex.Message}"); }
            }
        }

        // ── Sprint 5: Stats tab ──────────────────────────────────────────────
        private void StatsButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (StatsButton.IsChecked == true)
            {
                try { ShowStats(); }
                catch (Exception ex) { ShowFallbackContent($"Error: {ex.Message}"); }
            }
        }

        // ── Sprint 6: Backup tab ─────────────────────────────────────────────
        private void BackupButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (BackupButton.IsChecked == true)
            {
                try { ShowBackup(); }
                catch (Exception ex) { ShowFallbackContent($"Error: {ex.Message}"); }
            }
        }

        private void ShowHistory()
        {
            var control = new HistoryControl();
            AnimateContentChange(control, "Listen History");
        }

        private void ShowStats()
        {
            var control = new StatsControl();
            AnimateContentChange(control, "Stats");
        }

        private void ShowBackup()
        {
            var control = new BackupRestoreControl();
            AnimateContentChange(control, "Backup & Restore");
        }

        // ── Sprint 7: Integrations tab ──────────────────────────────────────
        private void IntegrationsButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (IntegrationsButton.IsChecked == true)
            {
                try { ShowIntegrations(); }
                catch (Exception ex) { ShowFallbackContent($"Error: {ex.Message}"); }
            }
        }

        private void ShowIntegrations()
        {
            var control = new IntegrationsSettingsControl();
            AnimateContentChange(control, "Integrations");
        }

        private void ShowAppearance()
        {
            _appearanceVM = App.Services.GetService<AppearanceSettingsViewModel>();
            var control = new AppearanceSettingsControl();
            if (_appearanceVM != null)
                control.DataContext = _appearanceVM;
            AnimateContentChange(control, "Appearance Settings");
        }

        private void ShowDownloads()
        {
            var vm = App.Services.GetService<DownloadsSettingsViewModel>();
            var control = new DownloadsSettingsControl();
            if (vm != null)
                control.DataContext = vm;
            AnimateContentChange(control, "Storage & Sources");
        }

        private void ShowFallbackContent(string message)
        {
            if (SettingsContent == null || SectionTitle == null) return;
            SectionTitle.Text = "Settings";
            SettingsContent.Content = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Colors.Red),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 20, 0, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
        }

        private void AnimateContentChange(UserControl newContent, string title)
        {
            if (SettingsContent?.Content is ISettingsControl outgoing)
            {
                try { outgoing.Save(); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Save error: {ex.Message}");
                }
            }

            if (SettingsContent == null || SectionTitle == null)
                return;

            SettingsContent.Content = newContent;
            SectionTitle.Text = title;
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

    /// <summary>
    /// A single searchable settings item. Label is what the user sees,
    /// Category is the parent tab, Navigate is the action to run on click.
    /// </summary>
    public class SettingsSearchItem
    {
        public string Label { get; set; }
        public string Category { get; set; }
        public Action Navigate { get; set; }

        public SettingsSearchItem() { }

        public SettingsSearchItem(string label, string category, Action navigate)
        {
            Label = label;
            Category = category;
            Navigate = navigate;
        }
    }
}

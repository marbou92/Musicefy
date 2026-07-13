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
                ShowAccount();
                _initialLoadPending = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsPage] Constructor init failed: {ex.Message}");
                ShowFallbackContent($"Error loading Account: {ex.Message}");
            }
        }

        private void SettingsPage_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_initialLoadPending)
            {
                _initialLoadPending = false;
                try { ShowAccount(); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsPage] Loaded init failed: {ex.Message}");
                    ShowFallbackContent($"Error loading Account: {ex.Message}");
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

        private IEnumerable<SettingsSearchItem> BuildSearchIndex()
        {
            // Account
            yield return new SettingsSearchItem("YouTube login", "Account", () => SwitchToTab("Account"));
            yield return new SettingsSearchItem("YouTube cookie", "Account", () => SwitchToTab("Account"));
            yield return new SettingsSearchItem("YouTube API key", "Account", () => SwitchToTab("Account"));
            yield return new SettingsSearchItem("Audio quality", "Account", () => SwitchToTab("Account"));
            // Appearance
            yield return new SettingsSearchItem("Theme mode", "Appearance", () => SwitchToTab("Appearance"));
            yield return new SettingsSearchItem("Color palette", "Appearance", () => SwitchToTab("Appearance"));
            yield return new SettingsSearchItem("Dynamic colors", "Appearance", () => SwitchToTab("Appearance"));
            yield return new SettingsSearchItem("Player background", "Appearance", () => SwitchToTab("Appearance"));
            // Player & Audio
            yield return new SettingsSearchItem("Skip silence", "Player & Audio", () => SwitchToTab("PlayerAudio"));
            yield return new SettingsSearchItem("Crossfade", "Player & Audio", () => SwitchToTab("PlayerAudio"));
            yield return new SettingsSearchItem("SponsorBlock", "Player & Audio", () => SwitchToTab("PlayerAudio"));
            yield return new SettingsSearchItem("Skip sponsor", "Player & Audio", () => SwitchToTab("PlayerAudio"));
            // Content
            yield return new SettingsSearchItem("Lyrics", "Content", () => SwitchToTab("Content"));
            yield return new SettingsSearchItem("Show on home", "Content", () => SwitchToTab("Content"));
            // Storage
            yield return new SettingsSearchItem("Local folders", "Storage", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("Download path", "Storage", () => SwitchToTab("Downloads"));
            yield return new SettingsSearchItem("Clear cache", "Storage", () => SwitchToTab("Downloads"));
            // Backup
            yield return new SettingsSearchItem("Backup", "Backup", () => SwitchToTab("Backup"));
            yield return new SettingsSearchItem("Restore", "Backup", () => SwitchToTab("Backup"));
            // Integrations
            yield return new SettingsSearchItem("Last.fm", "Integrations", () => SwitchToTab("Integrations"));
            yield return new SettingsSearchItem("Discord", "Integrations", () => SwitchToTab("Integrations"));
            // About
            yield return new SettingsSearchItem("About", "About", () => SwitchToTab("About"));
            yield return new SettingsSearchItem("GitHub", "About", () => SwitchToTab("About"));
            yield return new SettingsSearchItem("Version", "About", () => SwitchToTab("About"));
        }

        private void SwitchToTab(string tabName)
        {
            SearchQuery = "";
            switch (tabName)
            {
                case "Account":
                    AccountButton.IsChecked = true;
                    break;
                case "Appearance":
                    AppearanceButton.IsChecked = true;
                    break;
                case "PlayerAudio":
                    PlayerAudioButton.IsChecked = true;
                    break;
                case "Content":
                    ContentButton.IsChecked = true;
                    break;
                case "Downloads":
                    DownloadsButton.IsChecked = true;
                    break;
                case "Backup":
                    BackupButton.IsChecked = true;
                    break;
                case "Integrations":
                    IntegrationsButton.IsChecked = true;
                    break;
                case "About":
                    AboutButton.IsChecked = true;
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

        private void AccountButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_initialLoadPending) return;
            if (AccountButton.IsChecked == true)
            {
                try { ShowAccount(); }
                catch (Exception ex) { ShowFallbackContent($"Error: {ex.Message}"); }
            }
        }

        private void AppearanceButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_initialLoadPending) return;
            if (AppearanceButton.IsChecked == true)
            {
                try { ShowAppearance(); }
                catch (Exception ex) { ShowFallbackContent($"Error: {ex.Message}"); }
            }
        }

        private void PlayerAudioButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (PlayerAudioButton.IsChecked == true)
            {
                try { ShowPlayerAudio(); }
                catch (Exception ex) { ShowFallbackContent($"Error: {ex.Message}"); }
            }
        }

        private void ContentButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (ContentButton.IsChecked == true)
            {
                try { ShowContent(); }
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

        private void BackupButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (BackupButton.IsChecked == true)
            {
                try { ShowBackup(); }
                catch (Exception ex) { ShowFallbackContent($"Error: {ex.Message}"); }
            }
        }

        private void IntegrationsButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (IntegrationsButton.IsChecked == true)
            {
                try { ShowIntegrations(); }
                catch (Exception ex) { ShowFallbackContent($"Error: {ex.Message}"); }
            }
        }

        private void AboutButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (AboutButton.IsChecked == true)
            {
                try { ShowAbout(); }
                catch (Exception ex) { ShowFallbackContent($"Error: {ex.Message}"); }
            }
        }

        // ── Show methods ──────────────────────────────────────────────────────

        private void ShowAccount()
        {
            var control = new AccountSettingsControl();
            AnimateContentChange(control, "Account");
        }

        private void ShowAppearance()
        {
            _appearanceVM = App.Services.GetService<AppearanceSettingsViewModel>();
            var control = new AppearanceSettingsControl();
            if (_appearanceVM != null)
                control.DataContext = _appearanceVM;
            AnimateContentChange(control, "Appearance");
        }

        private void ShowPlayerAudio()
        {
            var control = new PlayerAudioSettingsControl();
            AnimateContentChange(control, "Player & Audio");
        }

        private void ShowContent()
        {
            var control = new ContentSettingsControl();
            AnimateContentChange(control, "Content");
        }

        private void ShowDownloads()
        {
            var vm = App.Services.GetService<DownloadsSettingsViewModel>();
            var control = new DownloadsSettingsControl();
            if (vm != null)
                control.DataContext = vm;
            AnimateContentChange(control, "Storage");
        }

        private void ShowBackup()
        {
            var control = new BackupRestoreControl();
            AnimateContentChange(control, "Backup & Restore");
        }

        private void ShowIntegrations()
        {
            var control = new IntegrationsSettingsControl();
            AnimateContentChange(control, "Integrations");
        }

        private void ShowAbout()
        {
            var control = new AboutControl();
            AnimateContentChange(control, "About");
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

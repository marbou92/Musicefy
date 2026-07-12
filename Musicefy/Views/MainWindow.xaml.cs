using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Musicefy.Core.Interfaces;
using Musicefy.Core.Models;
using Musicefy.Views;
using Musicefy.Services;
using Musicefy.ViewModels;

namespace Musicefy
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _viewModel;
        private readonly ILibraryService _libService;
        private bool _isInitializing = true;

        // Cache pages so they're only created once. Creating a new
        // HomeControl/SearchControl/etc. on every navigation is the
        // main cause of lag — it re-parses XAML, re-loads data, and
        // re-decodes all images from scratch.
        private HomeControl _homePage;
        private SearchControl _searchPage;
        private LibraryControl _libraryPage;
        private SettingsPage _settingsPage;

        private NowPlayingControl _nowPlayingView;
        private bool _isDraggingMiniPlayer = false;
        private Point _dragStartPoint;
        private double _dragStartTranslateX;
        private double _dragStartTranslateY;
        private bool _hasDraggedSignificantly = false;

        private NavigationService _navService;

        public MainWindow()
        {
            var services = App.Services;
            var playback = (PlaybackService)services.GetService(typeof(PlaybackService));
            _libService = (ILibraryService)services.GetService(typeof(ILibraryService));
            _navService = (NavigationService)services.GetService(typeof(NavigationService));

            _viewModel = new MainWindowViewModel(playback, _navService);
            _viewModel.PropertyChanged += OnMainWindowViewModelPropertyChanged;

            // Phase 1: Subscribe to typed artist/album navigation events
            // so that full ArtistInfo/AlbumInfo objects (with YouTube browse IDs)
            // are passed through to the target ViewModels.
            _navService.ArtistNavigationRequested += OnArtistNavigationRequested;
            _navService.AlbumNavigationRequested += OnAlbumNavigationRequested;

            // Phase 5: Subscribe to typed playlist navigation event
            _navService.PlaylistNavigationRequested += OnPlaylistNavigationRequested;

            this.DataContext = _viewModel;
            InitializeComponent();
            InitializeWindowChromeCommands();

            _viewModel.NavigateToPage(0);
            MainContent.Content = _viewModel.CurrentPage;

            MainContent.Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            MainContent.BeginAnimation(OpacityProperty, fadeIn);

            SidebarList.SelectedIndex = 0;
            _isInitializing = false;
        }

        private void OnMainWindowViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsMiniPlayerVisible))
                MiniPlayerBar.Visibility = _viewModel.IsMiniPlayerVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void InitializeWindowChromeCommands()
        {
            this.CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, (s, e) => SystemCommands.MinimizeWindow(this)));
            this.CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, (s, e) => SystemCommands.MaximizeWindow(this)));
            this.CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, (s, e) => SystemCommands.RestoreWindow(this)));
            this.CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (s, e) => this.Close()));
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Force-kill the process immediately. NAudio and other
            // background threads keep the process alive after the
            // window closes. Environment.Exit(0) guarantees the
            // process disappears from Task Manager.
            Environment.Exit(0);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (GetTemplateChild("BtnShellMinimize") is Button btnMinimize)
            { btnMinimize.Click -= MinimizeHandler; btnMinimize.Click += MinimizeHandler; }
            if (GetTemplateChild("BtnShellMaximize") is Button btnMaximize)
            { btnMaximize.Click -= MaximizeHandler; btnMaximize.Click += MaximizeHandler; }
            if (GetTemplateChild("BtnShellClose") is Button btnClose)
            { btnClose.Click -= CloseHandler; btnClose.Click += CloseHandler; }
        }

        private void MinimizeHandler(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
        private void CloseHandler(object sender, RoutedEventArgs e) => this.Close();
        private void MaximizeHandler(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
            else SystemCommands.MaximizeWindow(this);
        }

        // ── Navigation ListBox (Home / Search / Library) ─────────────────────
        private void Sidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SidebarList.SelectedItem == null) return;
            if (_isInitializing) return;

            // Deselect settings when main nav is chosen
            SettingsSidebarList.SelectedIndex = -1;

            AnimateToPage(SidebarList.SelectedIndex);
        }

        // ── Settings ListBox (pinned at bottom) ───────────────────────────────
        private void SettingsSidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SettingsSidebarList.SelectedItem == null) return;
            if (_isInitializing) return;

            // Deselect main nav when settings is chosen
            SidebarList.SelectedIndex = -1;

            AnimateToPage(3); // Settings is page index 3
        }

        private void AnimateToPage(int index)
        {
            if (MainContent == null) return;

            // Resolve the page NOW (before animation starts) so we know if it's null
            _viewModel.NavigateToPage(index);
            var targetPage = _viewModel.CurrentPage;

            // Fallback: if navigation returned null, use cached page
            if (targetPage == null)
            {
                try
                {
                    targetPage = index switch
                    {
                        0 => _homePage ??= new HomeControl(),
                        1 => _searchPage ??= new SearchControl(),
                        2 => _libraryPage ??= new LibraryControl(),
                        3 => _settingsPage ??= new SettingsPage(),
                        _ => null
                    };
                    _viewModel.CurrentPage = targetPage;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Direct page creation failed for index {index}: {ex}");
                }
            }

            // If still null, show error placeholder instead of blank
            if (targetPage == null)
            {
                MainContent.Content = new TextBlock
                {
                    Text = $"Failed to load page {index}. Check the Output window for errors.",
                    Foreground = new SolidColorBrush(Colors.Red),
                    FontSize = 16,
                    Margin = new Thickness(40),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                return;
            }

            var durationOut = TimeSpan.FromMilliseconds(110);
            var durationIn  = TimeSpan.FromMilliseconds(220);
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            var easeIn  = new CubicEase { EasingMode = EasingMode.EaseIn };

            var fadeOut = new DoubleAnimation(1, 0, durationOut) { EasingFunction = easeIn };
            var slideOut = new DoubleAnimation(0, -16, durationOut) { EasingFunction = easeIn };

            fadeOut.Completed += (s, ev) =>
            {
                MainContent.Content = targetPage;
                PageSlideTransform.Y = 16;
                this.UpdateLayout();

                var fadeIn  = new DoubleAnimation(0, 1, durationIn) { EasingFunction = easeOut };
                var slideIn = new DoubleAnimation(16, 0, durationIn) { EasingFunction = easeOut };
                MainContent.BeginAnimation(OpacityProperty, fadeIn);
                PageSlideTransform.BeginAnimation(TranslateTransform.YProperty, slideIn);
            };

            MainContent.BeginAnimation(OpacityProperty, fadeOut);
            PageSlideTransform.BeginAnimation(TranslateTransform.YProperty, slideOut);
        }

        public void NavigateToSettings()
        {
            SidebarList.SelectedIndex = -1;
            SettingsSidebarList.SelectedIndex = 0;
        }

        // ── Mini-player drag ──────────────────────────────────────────────────
        private void MiniPlayerBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            MiniPlayerTransform.BeginAnimation(TranslateTransform.XProperty, null);
            MiniPlayerTransform.BeginAnimation(TranslateTransform.YProperty, null);
            MiniPlayerBar.BeginAnimation(OpacityProperty, null);

            _isDraggingMiniPlayer = true;
            _dragStartPoint = e.GetPosition(this);
            _dragStartTranslateX = MiniPlayerTransform.X;
            _dragStartTranslateY = MiniPlayerTransform.Y;
            _hasDraggedSignificantly = false;
            MiniPlayerBar.CaptureMouse();
        }

        private void MiniPlayerBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingMiniPlayer) return;

            Point currentPoint = e.GetPosition(this);
            double deltaX = currentPoint.X - _dragStartPoint.X;
            double deltaY = currentPoint.Y - _dragStartPoint.Y;

            if (!_hasDraggedSignificantly && (Math.Abs(deltaX) > 5 || Math.Abs(deltaY) > 5))
                _hasDraggedSignificantly = true;

            double maxOffset = double.IsNaN(MiniPlayerBar.Width) ? 0 : Math.Max(0, (this.ActualWidth - MiniPlayerBar.Width) / 2.0 - 16);
            MiniPlayerTransform.X = Math.Max(-maxOffset, Math.Min(maxOffset, _dragStartTranslateX + deltaX));

            if (deltaY > 0)
            {
                MiniPlayerTransform.Y = _dragStartTranslateY + deltaY;
                MiniPlayerBar.Opacity = Math.Max(0.2, 1.0 - (deltaY / 120.0));
            }
            else
            {
                MiniPlayerTransform.Y = 0;
                MiniPlayerBar.Opacity = 1.0;
            }
        }

        private void MiniPlayerBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingMiniPlayer) return;
            _isDraggingMiniPlayer = false;
            MiniPlayerBar.ReleaseMouseCapture();

            if (MiniPlayerTransform.Y > 45) DismissMiniPlayer();
            else if (!_hasDraggedSignificantly) OpenFullNowPlaying();
            else if (MiniPlayerTransform.Y > 0) SnapMiniPlayerBack();
        }

        private void DismissMiniPlayer()
        {
            _viewModel.Playback.Stop();
            _viewModel.NowPlaying = null;
            MiniPlayerBar.Visibility = Visibility.Collapsed;
            MiniPlayerTransform.X = 0;
            MiniPlayerTransform.Y = 0;
            MiniPlayerBar.Opacity = 1.0;
        }

        private void SnapMiniPlayerBack()
        {
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            MiniPlayerTransform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(MiniPlayerTransform.Y, 0, TimeSpan.FromMilliseconds(250)) { EasingFunction = easeOut });
            MiniPlayerBar.BeginAnimation(OpacityProperty,
                new DoubleAnimation(MiniPlayerBar.Opacity, 1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = easeOut });
        }

        private void OpenFullNowPlaying()
        {
            if (_nowPlayingView == null)
            {
                var npVm = new NowPlayingViewModel(_viewModel.Playback, _libService);
                _nowPlayingView = new NowPlayingControl(_viewModel.Playback, npVm);
                _nowPlayingView.RequestCollapse += CollapseFullNowPlaying;
                NowPlayingPresenter.Content = _nowPlayingView;
            }

            _viewModel.IsFullPanelOpen = true;
            FullNowPlayingContainer.Visibility = Visibility.Visible;
            FullNowPlayingContainer.Opacity = 0;
            FullPanelTransform.Y = this.ActualHeight;

            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            FullPanelTransform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(this.ActualHeight, 0, TimeSpan.FromMilliseconds(400)) { EasingFunction = easeOut });
            FullNowPlayingContainer.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)) { EasingFunction = easeOut });
            MiniPlayerBar.BeginAnimation(OpacityProperty,
                new DoubleAnimation(MiniPlayerBar.Opacity, 0, TimeSpan.FromMilliseconds(200)) { EasingFunction = easeOut });
            MiniPlayerTransform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(MiniPlayerTransform.Y, MiniPlayerTransform.Y + 40, TimeSpan.FromMilliseconds(350)) { EasingFunction = easeOut });
        }

        private void CollapseFullNowPlaying()
        {
            MiniPlayerBar.Visibility = Visibility.Visible;
            var easeIn  = new CubicEase { EasingMode = EasingMode.EaseIn };
            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };

            var slideDown = new DoubleAnimation(0, this.ActualHeight, TimeSpan.FromMilliseconds(350)) { EasingFunction = easeIn };
            slideDown.Completed += (s, e) =>
            {
                _viewModel.IsFullPanelOpen = false;
                FullNowPlayingContainer.Visibility = Visibility.Collapsed;
            };

            FullPanelTransform.BeginAnimation(TranslateTransform.YProperty, slideDown);
            FullNowPlayingContainer.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250)) { EasingFunction = easeIn });
            MiniPlayerBar.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)) { EasingFunction = easeOut });
            MiniPlayerTransform.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(40, 0, TimeSpan.FromMilliseconds(350)) { EasingFunction = easeOut });
        }

        /// <summary>
        /// Navigate to artist by name only (legacy path).
        /// Used by code paths that don't have an ArtistInfo object.
        /// </summary>
        public void NavigateToArtist(string artistName)
        {
            var services = App.Services;
            var viewModel = (ArtistViewModel)services.GetService(typeof(ArtistViewModel));
            var artistView = new ArtistView(viewModel);
            ArtistOverlay.Content = artistView;
            ArtistOverlay.Visibility = Visibility.Visible;
            artistView.LoadArtist(artistName);
        }

        /// <summary>
        /// Navigate to artist using a full <see cref="ArtistInfo"/> object.
        /// Preserves YouTube channel ID for rich YouTube Music artist browsing.
        /// Inspired by Echo Music's first-class entity navigation.
        /// </summary>
        public void NavigateToArtist(ArtistInfo artistInfo)
        {
            if (artistInfo == null) return;
            var services = App.Services;
            var viewModel = (ArtistViewModel)services.GetService(typeof(ArtistViewModel));
            var artistView = new ArtistView(viewModel);
            ArtistOverlay.Content = artistView;
            ArtistOverlay.Visibility = Visibility.Visible;
            artistView.LoadArtist(artistInfo);
        }

        /// <summary>
        /// Navigate to album by name only (legacy path).
        /// Used by code paths that don't have an AlbumInfo object.
        /// </summary>
        public void NavigateToAlbum(string albumName, string artistName)
        {
            var services = App.Services;
            var viewModel = (AlbumViewModel)services.GetService(typeof(AlbumViewModel));
            var albumView = new AlbumView(viewModel);
            AlbumOverlay.Content = albumView;
            AlbumOverlay.Visibility = Visibility.Visible;
            albumView.LoadAlbum(albumName, artistName);
        }

        /// <summary>
        /// Navigate to album using a full <see cref="AlbumInfo"/> object.
        /// Preserves YouTube album browse ID for rich YouTube Music album browsing.
        /// Inspired by Echo Music's two-step album fetch (list → detail).
        /// </summary>
        public void NavigateToAlbum(AlbumInfo albumInfo)
        {
            if (albumInfo == null) return;
            var services = App.Services;
            var viewModel = (AlbumViewModel)services.GetService(typeof(AlbumViewModel));
            var albumView = new AlbumView(viewModel);
            AlbumOverlay.Content = albumView;
            AlbumOverlay.Visibility = Visibility.Visible;
            albumView.LoadAlbum(albumInfo);
        }

        public void NavigateBack()
        {
            ArtistOverlay.Visibility = Visibility.Collapsed;
            AlbumOverlay.Visibility = Visibility.Collapsed;
            PlaylistOverlay.Visibility = Visibility.Collapsed;
        }

        // ── Phase 1: Typed Navigation Event Handlers ───────────────────────

        /// <summary>
        /// Handles <see cref="NavigationService.ArtistNavigationRequested"/>.
        /// Dispatches the full <see cref="ArtistInfo"/> object to the artist overlay.
        /// </summary>
        private void OnArtistNavigationRequested(ArtistInfo artistInfo)
        {
            NavigateToArtist(artistInfo);
        }

        /// <summary>
        /// Handles <see cref="NavigationService.AlbumNavigationRequested"/>.
        /// Dispatches the full <see cref="AlbumInfo"/> object to the album overlay.
        /// </summary>
        private void OnAlbumNavigationRequested(AlbumInfo albumInfo)
        {
            NavigateToAlbum(albumInfo);
        }

        // ── Phase 5: Playlist navigation ────────────────────────────────

        /// <summary>
        /// Navigate to a playlist detail page using a full <see cref="PlaylistInfo"/> object.
        /// Phase 5: Playlists & Collection Management.
        /// </summary>
        public void NavigateToPlaylist(PlaylistInfo playlistInfo)
        {
            if (playlistInfo == null) return;
            var services = App.Services;
            var viewModel = (PlaylistViewModel)services.GetService(typeof(PlaylistViewModel));
            var playlistView = new PlaylistView(viewModel);
            PlaylistOverlay.Content = playlistView;
            PlaylistOverlay.Visibility = Visibility.Visible;
            playlistView.LoadPlaylist(playlistInfo);
        }

        /// <summary>
        /// Navigate to a playlist by its database ID.
        /// </summary>
        public void NavigateToPlaylist(string playlistId)
        {
            if (string.IsNullOrEmpty(playlistId)) return;
            var services = App.Services;
            var viewModel = (PlaylistViewModel)services.GetService(typeof(PlaylistViewModel));
            var playlistView = new PlaylistView(viewModel);
            PlaylistOverlay.Content = playlistView;
            PlaylistOverlay.Visibility = Visibility.Visible;
            playlistView.LoadPlaylist(playlistId);
        }

        /// <summary>
        /// Handles <see cref="NavigationService.PlaylistNavigationRequested"/>.
        /// Dispatches the full <see cref="PlaylistInfo"/> object to the playlist overlay.
        /// </summary>
        private void OnPlaylistNavigationRequested(PlaylistInfo playlistInfo)
        {
            NavigateToPlaylist(playlistInfo);
        }
    }
}

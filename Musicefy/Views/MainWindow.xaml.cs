using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Musicefy.Core.Interfaces;
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

        // Animation-only state (purely visual, stays in code-behind)
        private NowPlayingControl _nowPlayingView;
        private bool _isDraggingMiniPlayer = false;
        private Point _dragStartPoint;
        private double _dragStartTranslateX;
        private double _dragStartTranslateY;
        private bool _hasDraggedSignificantly = false;

        public MainWindow()
        {
            var services = App.Services;
            var playback = (PlaybackService)services.GetService(typeof(PlaybackService));
            _libService = (ILibraryService)services.GetService(typeof(ILibraryService));
            var navService = (NavigationService)services.GetService(typeof(NavigationService));

            _viewModel = new MainWindowViewModel(playback, navService);
            _viewModel.SettingsRequested += OnSettingsRequested;
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainWindowViewModel.IsMiniPlayerVisible))
                    MiniPlayerBar.Visibility = _viewModel.IsMiniPlayerVisible ? Visibility.Visible : Visibility.Collapsed;
            };

            this.DataContext = _viewModel;
            InitializeComponent();

            InitializeWindowChromeCommands();

            // CRITICAL: Initialize with Home page immediately
            // This ensures the content area is populated before the window is shown
            _viewModel.NavigateToPage(0);
            MainContent.Content = _viewModel.CurrentPage;

            // Start fade-in animation for the content
            MainContent.Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            MainContent.BeginAnimation(OpacityProperty, fadeIn);

            _isInitializing = false;
            SidebarList.SelectedIndex = 0;
        }

        private void OnSettingsRequested(object sender, EventArgs e)
        {
            new SettingsWindow { Owner = this }.ShowDialog();
            _viewModel.RequestSidebarReset();
        }

        // ── Window chrome ────────────────────────────────────────────────
        private void InitializeWindowChromeCommands()
        {
            this.CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, (s, e) => SystemCommands.MinimizeWindow(this)));
            this.CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, (s, e) => SystemCommands.MaximizeWindow(this)));
            this.CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, (s, e) => SystemCommands.RestoreWindow(this)));
            this.CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (s, e) => this.Close()));
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

        // ── Sidebar navigation ─────────────────────────────────────────
        private void Sidebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SidebarList.SelectedItem == null) return;
            if (_isInitializing) return;

            // Handle Settings separately
            if (SidebarList.SelectedItem == SettingsItem)
            {
                OnSettingsRequested(this, EventArgs.Empty);
                SidebarList.SelectedIndex = _viewModel.PreviousSidebarIndex;
                return;
            }

            if (MainContent == null) return;

            // Animate fade transition
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s, ev) =>
            {
                // Navigate to the selected page
                int index = SidebarList.SelectedIndex;
                _viewModel.NavigateToPage(index);
                MainContent.Content = _viewModel.CurrentPage;
                this.UpdateLayout();
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                MainContent.BeginAnimation(OpacityProperty, fadeIn);
            };
            MainContent.BeginAnimation(OpacityProperty, fadeOut);
        }

        // ── Mini-player drag (purely visual animation) ───────────────────
        private void MiniPlayerBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Stop any running animations so local value assignments in MouseMove take effect
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

            double maxOffset = Math.Max(0, (this.ActualWidth - MiniPlayerBar.Width) / 2.0 - 16);
            double targetX = _dragStartTranslateX + deltaX;
            MiniPlayerTransform.X = Math.Max(-maxOffset, Math.Min(maxOffset, targetX));

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

            if (MiniPlayerTransform.Y > 45)
                DismissMiniPlayer();
            else if (!_hasDraggedSignificantly)
                OpenFullNowPlaying();
            else if (MiniPlayerTransform.Y > 0)
                SnapMiniPlayerBack();
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
            var easeIn = new CubicEase { EasingMode = EasingMode.EaseIn };
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

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Musicefy.Core.Models;
using Musicefy.Services;
using Musicefy.ViewModels;

namespace Musicefy.Views
{
    public partial class LibraryControl : UserControl
    {
        private LibraryViewModel ViewModel => DataContext as LibraryViewModel;
        private Action _folderInitHandler;
        private Action<string> _createPlaylistHandler;

        public LibraryControl()
        {
            InitializeComponent();
            Loaded += LibraryControl_Loaded;
            Unloaded += LibraryControl_Unloaded;
        }

        private void LibraryControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                _folderInitHandler = () =>
                {
                    TrackListDisplayPanel.InitializeDataStream(
                        new System.Collections.Generic.List<MusicFile>(),
                        (PlaybackService)App.Services.GetService(typeof(PlaybackService)));
                };
                _createPlaylistHandler = name =>
                {
                    var dialog = new CreatePlaylistWindow { Owner = Window.GetWindow(this) };
                    if (dialog.ShowDialog() == true)
                        _ = ViewModel.OnPlaylistNameReceived(dialog.ResultPlaylistName);
                };
                ViewModel.RequestFolderInit += _folderInitHandler;
                ViewModel.CreatePlaylistRequested += _createPlaylistHandler;
            }
        }

        private void LibraryControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                if (_folderInitHandler != null)
                    ViewModel.RequestFolderInit -= _folderInitHandler;
                if (_createPlaylistHandler != null)
                    ViewModel.CreatePlaylistRequested -= _createPlaylistHandler;
            }
            _folderInitHandler = null;
            _createPlaylistHandler = null;
        }

        // Panel fade transitions (visual-only, stays in code-behind)
        public static readonly DependencyProperty FadeTargetProperty =
            DependencyProperty.Register("FadeTarget", typeof(Visibility), typeof(LibraryControl),
                new PropertyMetadata(Visibility.Collapsed, OnFadeTargetChanged));

        private static void OnFadeTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LibraryControl control && e.NewValue is Visibility vis && vis == Visibility.Visible)
            {
                control.AnimateFadeIn();
            }
        }

        private void AnimateFadeIn()
        {
            var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, fadeIn);
        }
    }
}

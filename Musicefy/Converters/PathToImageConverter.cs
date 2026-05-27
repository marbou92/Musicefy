using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Musicefy.Core.Interfaces;

namespace Musicefy.Converters
{
    public class PathToImageConverter : IValueConverter
    {
        private static readonly ConcurrentDictionary<string, BitmapImage> _cache
            = new ConcurrentDictionary<string, BitmapImage>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, byte> _inFlight
            = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        private static readonly SemaphoreSlim _throttle = new SemaphoreSlim(4, 4);

        private static readonly ImageSource _fallbackImage;
        private static IStreamingSourceManager _sourceManager;

        private const int MaxCacheEntries = 800;
        private const int ListDecodeWidth  = 48;
        private const int GridDecodeWidth  = 200;

        static PathToImageConverter()
        {
            _fallbackImage = CreateFallbackIcon();
            try { _sourceManager = App.Services.GetService<IStreamingSourceManager>(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[PathToImageConverter] Failed to resolve IStreamingSourceManager: {ex.Message}"); }
        }

        private static ImageSource CreateFallbackIcon()
        {
            var geometry = Geometry.Parse(
                "M9 6.5C9 7.33 8.33 8 7.5 8S6 7.33 6 6.5 6.67 5 7.5 5 9 5.67 9 6.5zM16 7h-2v.82c-.42-.52-1.07-.82-1.82-.82C11.01 7 10 8.01 10 9.25c0 1.24 1.01 2.25 2.25 2.25.68 0 1.28-.3 1.7-.78.02.01.04.02.05.03V14H9V6.5C9 5.12 7.88 4 6.5 4S4 5.12 4 6.5 5.12 9 6.5 9c.38 0 .74-.07 1.07-.2.02.06.04.13.06.2h.01C7.6 10.17 7 11.27 7 12.5c0 2.48 2.02 4.5 4.5 4.5s4.5-2.02 4.5-4.5c0-1.23-.6-2.33-1.64-3 .01-.01.03-.02.04-.03.33.13.69.2 1.07.2 1.38 0 2.5-1.12 2.5-2.5S17.38 4 16 4s-2.5 1.12-2.5 2.5c0 .09.01.18.02.27.13.15.28.28.45.4L16 7.78V9h-1v3.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h-1z");
            var penBrush = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128));
            penBrush.Freeze();
            var pen = new Pen(penBrush, 1.2);
            pen.Freeze();
            return new DrawingImage(new GeometryDrawing
            {
                Geometry = geometry,
                Pen = pen,
            });
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string path = value as string;
            if (string.IsNullOrEmpty(path))
                return _fallbackImage;

            if (_cache.TryGetValue(path, out var cached))
                return cached;

            if (_inFlight.TryAdd(path, 0))
            {
                bool isGrid = string.Equals(parameter?.ToString(), "grid",
                    StringComparison.OrdinalIgnoreCase);
                int decodeWidth = isGrid ? GridDecodeWidth : ListDecodeWidth;
                _ = LoadAsync(path, decodeWidth);
            }

            return _fallbackImage;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static async System.Threading.Tasks.Task LoadAsync(string coverPath, int decodeWidth)
        {
            try
            {
                await _throttle.WaitAsync();
            }
            catch (ObjectDisposedException)
            {
                _inFlight.TryRemove(coverPath, out _);
                return;
            }

            BitmapImage bitmap = null;
            try
            {
                // Streaming source cover ID
                if (coverPath.Contains(":cover:"))
                {
                    bitmap = await LoadStreamingCoverAsync(coverPath);
                }
                else
                {
                    bitmap = await System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            // HTTP URL
                            if (coverPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                                return LoadHttpImage(coverPath, decodeWidth);

                            // Local file path
                            return LoadDiskImage(coverPath, decodeWidth);
                        }
                        catch { return null; }
                    });
                }
            }
            finally
            {
                try { _throttle.Release(); }
                catch (ObjectDisposedException) { }
            }

            if (bitmap != null)
            {
                _cache[coverPath] = bitmap;
                EvictIfNeeded();
            }

            _inFlight.TryRemove(coverPath, out _);

            Application.Current?.Dispatcher.BeginInvoke(
                (Action)(() => RefreshImages(coverPath)),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private static void EvictIfNeeded()
        {
            if (_cache.Count <= MaxCacheEntries) return;
            int toRemove = MaxCacheEntries / 5;
            foreach (var key in _cache.Keys.Take(toRemove))
                _cache.TryRemove(key, out _);
        }

        private static void RefreshImages(string coverPath)
        {
            if (Application.Current == null) return;
            foreach (Window window in Application.Current.Windows)
            {
                if (!window.IsVisible) continue;
                var content = window.Content as FrameworkElement;
                if (content != null)
                    WalkAndUpdate(content, coverPath, 0);
            }
        }

        private static void WalkAndUpdate(DependencyObject parent, string coverPath, int depth)
        {
            if (depth > 20) return;
            int children = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < children; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Image img && ReferenceEquals(img.Source, _fallbackImage))
                {
                    var dc = (child as FrameworkElement)?.DataContext as Musicefy.Core.Models.MusicFile;
                    if (dc?.CoverPath != null &&
                        string.Equals(dc.CoverPath, coverPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (_cache.TryGetValue(coverPath, out var bmp))
                            img.Source = bmp;
                    }
                }
                if (child is System.Windows.Controls.Panel || child is System.Windows.Controls.ContentPresenter ||
                    child is System.Windows.Controls.ItemsPresenter || child is System.Windows.Controls.Border)
                    WalkAndUpdate(child, coverPath, depth + 1);
            }
        }

        private static BitmapImage LoadDiskImage(string filePath, int decodeWidth)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(Path.GetFullPath(filePath), UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = decodeWidth;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        private static BitmapImage LoadHttpImage(string url, int decodeWidth)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(url, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = decodeWidth;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        private static async System.Threading.Tasks.Task<BitmapImage> LoadStreamingCoverAsync(string coverId)
        {
            if (_sourceManager == null) return null;

            var bytes = await _sourceManager.ResolveCoverArtAsync(coverId);

            if (bytes == null || bytes.Length == 0) return null;

            var bmp = new BitmapImage();
            using (var ms = new MemoryStream(bytes))
            {
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
            }
            bmp.Freeze();
            return bmp;
        }

    }
}

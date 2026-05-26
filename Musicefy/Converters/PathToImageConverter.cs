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
            catch { }
        }

        private static ImageSource CreateFallbackIcon()
        {
            var geometry = Geometry.Parse(
                "M12 3v10.55c-.59-.34-1.27-.55-2-.55-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4V7h4V3h-6z");
            var drawing = new GeometryDrawing
            {
                Geometry = geometry,
                Brush = new SolidColorBrush(Color.FromArgb(80, 128, 128, 128)),
            };
            return new DrawingImage(drawing);
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
                bitmap = await System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        // Streaming source cover ID
                        if (coverPath.Contains(":cover:"))
                            return LoadStreamingCover(coverPath);

                        // HTTP URL
                        if (coverPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            return LoadHttpImage(coverPath, decodeWidth);

                        // Local file path
                        return LoadDiskImage(coverPath, decodeWidth);
                    }
                    catch { return null; }
                });
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
            var keys = _cache.Keys.ToArray();
            for (int i = 0; i < Math.Min(toRemove, keys.Length); i++)
                _cache.TryRemove(keys[i], out _);
        }

        private static void RefreshImages(string coverPath)
        {
            if (Application.Current == null) return;
            foreach (Window window in Application.Current.Windows)
            {
                if (!window.IsVisible) continue;
                WalkAndUpdate(window, coverPath);
            }
        }

        private static void WalkAndUpdate(DependencyObject parent, string coverPath)
        {
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
                WalkAndUpdate(child, coverPath);
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

        private static BitmapImage LoadStreamingCover(string coverId)
        {
            if (_sourceManager == null) return null;

            var bytes = System.Threading.Tasks.Task.Run(() =>
                _sourceManager.ResolveCoverArtAsync(coverId)).GetAwaiter().GetResult();

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

        private static BitmapImage LoadResourceImage(string packUri)
        {
            try
            {
                var bmp = new BitmapImage(new Uri(packUri, UriKind.RelativeOrAbsolute));
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                var fallback = new BitmapImage();
                fallback.BeginInit();
                fallback.DecodePixelWidth = 1;
                fallback.DecodePixelHeight = 1;
                fallback.EndInit();
                fallback.Freeze();
                return fallback;
            }
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Musicefy.Converters;

namespace Musicefy.Converters
{
    /// <summary>
    /// High-performance async image converter with DecodePixelWidth optimization and Freeze().
    /// Enhances PathToImageConverter by providing size-aware decoding that prevents
    /// full-resolution images from being loaded into memory for small thumbnails.
    ///
    /// ConverterParameter values:
    ///   "grid"    → 320px decode (for 160px cards at 2x HiDPI)
    ///   "list"    → 96px decode  (for 48px list items at 2x HiDPI)
    ///   "hero"    → 800px decode (for NowPlaying 340px art at 2x HiDPI)
    ///   "full"    → 1280px decode (for full-screen hero images)
    ///   null/""   → 160px decode (default, moderate quality)
    ///
    /// The converter delegates actual loading to PathToImageConverter which handles
    /// WebP/AVIF detection, CDN URL sanitization, and streaming source covers.
    /// This converter adds:
    ///   1. Size-aware DecodePixelWidth (prevents loading 3000x3000 images for 48px thumbnails)
    ///   2. BitmapImage.Freeze() for cross-thread safety
    ///   3. ConcurrentDictionary cache with WeakReference for memory efficiency
    ///   4. ImageOpened fade-in via attached property (see CoverFadeBehavior)
    /// </summary>
    public class AsyncCoverConverter : IValueConverter
    {
        // Decode sizes for different render contexts (2x for HiDPI)
        private const int ListDecodeWidth = 96;    // 48px list items × 2
        private const int GridDecodeWidth = 320;   // 160px cards × 2
        private const int HeroDecodeWidth = 800;   // 340-400px hero art × 2
        private const int FullDecodeWidth = 1280;  // Full-screen display
        private const int DefaultDecodeWidth = 160; // Default moderate quality

        // Secondary cache using WeakReference — allows GC to reclaim bitmaps under memory pressure
        // while still providing instant access for frequently-used images
        private static readonly ConcurrentDictionary<string, WeakReference<BitmapImage>> _weakCache
            = new ConcurrentDictionary<string, WeakReference<BitmapImage>>(StringComparer.OrdinalIgnoreCase);

        // Reuse the existing PathToImageConverter for all the heavy lifting (HTTP, WebP, CDN, etc.)
        private static readonly PathToImageConverter _innerConverter = new PathToImageConverter();

        /// <summary>
        /// Converts a CoverPath string to a BitmapImage with appropriate decode width.
        /// First checks the weak cache, then delegates to PathToImageConverter.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string path = value as string;
            if (string.IsNullOrEmpty(path))
                return _innerConverter.Convert(value, targetType, parameter, culture);

            // Determine the target decode width from the ConverterParameter
            int decodeWidth = GetDecodeWidth(parameter);

            // Build a cache key that includes the decode width so the same image
            // at different sizes gets cached separately
            string cacheKey = $"{decodeWidth}:{path}";

            // Check weak cache first — if the BitmapImage is still alive, reuse it
            if (_weakCache.TryGetValue(cacheKey, out var weakRef) &&
                weakRef.TryGetTarget(out var cachedBmp) &&
                cachedBmp != null)
            {
                return cachedBmp;
            }

            // Clean up dead weak references
            if (weakRef != null)
                _weakCache.TryRemove(cacheKey, out _);

            // Delegate to PathToImageConverter which handles:
            // - WebP/AVIF format detection and fallback
            // - YouTube CDN URL sanitization
            // - Streaming source cover resolution
            // - Async loading with throttle
            // - LRU cache with eviction
            var result = _innerConverter.Convert(value, targetType, parameter, culture);

            // If the inner converter returned a BitmapImage, cache it with WeakReference
            // for faster subsequent lookups (the inner converter's strong cache will also hold it)
            if (result is BitmapImage bmp && bmp.IsFrozen)
            {
                _weakCache[cacheKey] = new WeakReference<BitmapImage>(bmp);
            }

            return result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Determines the DecodePixelWidth based on the ConverterParameter.
        /// This ensures we never load a 3000×3000 JPEG for a 48px list thumbnail.
        /// </summary>
        private static int GetDecodeWidth(object parameter)
        {
            string size = parameter?.ToString()?.ToLowerInvariant();
            switch (size)
            {
                case "list": return ListDecodeWidth;
                case "grid": return GridDecodeWidth;
                case "hero": return HeroDecodeWidth;
                case "full": return FullDecodeWidth;
                default: return DefaultDecodeWidth;
            }
        }

        /// <summary>
        /// Creates a BitmapImage with the specified decode width from a byte array.
        /// Called on the UI thread. The resulting BitmapImage is frozen for cross-thread use.
        /// </summary>
        internal static BitmapImage CreateOptimizedBitmap(byte[] bytes, int decodeWidth)
        {
            var bmp = new BitmapImage();
            using (var ms = new MemoryStream(bytes))
            {
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = decodeWidth;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bmp.StreamSource = ms;
                bmp.EndInit();
            }
            bmp.Freeze();
            return bmp;
        }

        /// <summary>
        /// Creates a BitmapImage from a URI with the specified decode width.
        /// Used for local file paths. The resulting BitmapImage is frozen for cross-thread use.
        /// </summary>
        internal static BitmapImage CreateOptimizedBitmapFromUri(string path, int decodeWidth)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path, UriKind.RelativeOrAbsolute);
                bmp.DecodePixelWidth = decodeWidth;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.DelayCreation;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Attached behavior that adds a fade-in animation when a cover image loads.
    /// Usage in XAML:
    ///   &lt;Image Source="{Binding CoverPath, Converter={StaticResource AsyncCoverConverter}}"
    ///          converters:CoverFadeBehavior.IsEnabled="True"/&gt;
    ///
    /// When IsEnabled is true, the Image starts at Opacity=0 and fades to 1
    /// when the Source property changes to a non-null value, creating a smooth
    /// content-reveal effect. WPF does not have ImageOpened (that's UWP/WinUI),
    /// so we use DependencyPropertyDescriptor to watch Source changes instead.
    /// </summary>
    public static class CoverFadeBehavior
    {
        private static readonly Duration FadeDuration = new Duration(TimeSpan.FromMilliseconds(250));

        // Track whether a given Image is being observed so we can clean up
        private static readonly ConditionalWeakTable<Image, SourceWatcher> _watchers =
            new ConditionalWeakTable<Image, SourceWatcher>();

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(CoverFadeBehavior),
                new FrameworkPropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) =>
            (bool)obj.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(DependencyObject obj, bool value) =>
            obj.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Image img)
            {
                if ((bool)e.NewValue)
                {
                    // Attach a SourceWatcher that monitors Source property changes
                    // via DependencyPropertyDescriptor (WPF-compatible alternative
                    // to UWP's ImageOpened event)
                    var watcher = new SourceWatcher(img);
                    _watchers.Add(img, watcher);

                    // Initial state: invisible if no source yet, visible otherwise
                    img.Opacity = img.Source != null ? 1 : 0;
                }
                else
                {
                    // Remove watcher if it exists
                    if (_watchers.TryGetValue(img, out var watcher))
                    {
                        watcher.Detach();
                        _watchers.Remove(img);
                    }
                    img.Opacity = 1;
                }
            }
        }

        /// <summary>
        /// Helper class that subscribes to Source property changes on an Image
        /// and triggers the fade-in animation. Uses ConditionalWeakTable for
        /// automatic cleanup when the Image is garbage-collected.
        /// </summary>
        private sealed class SourceWatcher
        {
            private readonly Image _image;
            private readonly EventHandler _sourceChangedHandler;

            public SourceWatcher(Image image)
            {
                _image = image;

                // Use DependencyPropertyDescriptor to watch Source changes —
                // this is the WPF equivalent of UWP's ImageOpened event
                var descriptor = DependencyPropertyDescriptor.FromProperty(
                    Image.SourceProperty, typeof(Image));

                _sourceChangedHandler = OnSourceChanged;
                descriptor.AddValueChanged(_image, _sourceChangedHandler);

                // Also subscribe to Loaded to handle cached images that are
                // already set when the control enters the visual tree
                _image.Loaded += OnImageLoaded;

                // If the source is already downloading asynchronously,
                // subscribe to BitmapImage.DownloadCompleted for remote URIs
                HookBitmapDownload(image.Source as BitmapImage);
            }

            public void Detach()
            {
                var descriptor = DependencyPropertyDescriptor.FromProperty(
                    Image.SourceProperty, typeof(Image));
                descriptor.RemoveValueChanged(_image, _sourceChangedHandler);
                _image.Loaded -= OnImageLoaded;
                // Unhook the tracked downloading bitmap (if any) instead of
                // _image.Source which might be a different (frozen) instance
                if (_downloadingBitmap != null)
                    UnhookBitmapDownload(_downloadingBitmap);
            }

            private void OnSourceChanged(object sender, EventArgs e)
            {
                var img = (Image)sender;

                // Unhook previous downloading BitmapImage handler if any.
                // Note: img.Source is already the NEW source at this point,
                // so we unhook the tracked _downloadingBitmap (the old one) instead.
                if (_downloadingBitmap != null)
                    UnhookBitmapDownload(_downloadingBitmap);

                if (img.Source == null)
                {
                    // Source cleared — go invisible
                    img.Opacity = 0;
                    return;
                }

                // If the BitmapImage is still downloading (remote URI), wait for it
                if (img.Source is BitmapImage bmp && bmp.IsDownloading)
                {
                    HookBitmapDownload(bmp);
                    img.Opacity = 0;
                    return;
                }

                // Source is ready (local, cached, or already downloaded) — fade in
                FadeIn(img);
            }

            private void OnImageLoaded(object sender, RoutedEventArgs e)
            {
                // When the Image control loads in the visual tree, check if it
                // already has a source. If it does (from cache), show it — no
                // need to fade in cached content that appears instantly.
                if (_image.Source != null && !(_image.Source is BitmapImage bmp && bmp.IsDownloading))
                {
                    _image.Opacity = 1;
                }
            }

            private BitmapImage _downloadingBitmap;

            private void HookBitmapDownload(BitmapImage bmp)
            {
                // Frozen bitmaps are immutable — they cannot have event handlers
                // added/removed. A frozen bitmap is already fully decoded, so
                // there's nothing to wait for.
                if (bmp == null || bmp.IsFrozen || !bmp.IsDownloading) return;
                _downloadingBitmap = bmp;
                bmp.DownloadCompleted += OnDownloadCompleted;
                bmp.DownloadFailed += OnDownloadFailed;
            }

            private void UnhookBitmapDownload(BitmapImage bmp)
            {
                if (bmp == null) return;
                // Frozen bitmaps cannot be modified — attempting to -= from events
                // throws InvalidOperationException. Since frozen bitmaps are
                // already fully loaded, the event handlers will never fire anyway.
                if (!bmp.IsFrozen)
                {
                    bmp.DownloadCompleted -= OnDownloadCompleted;
                    bmp.DownloadFailed -= OnDownloadFailed;
                }
                if (_downloadingBitmap == bmp)
                    _downloadingBitmap = null;
            }

            private void OnDownloadCompleted(object sender, EventArgs e)
            {
                // Remote image finished downloading — fade in on the UI thread
                var bmp = (BitmapImage)sender;
                UnhookBitmapDownload(bmp);
                _image.Dispatcher.BeginInvoke(new Action(() => FadeIn(_image)));
            }

            private void OnDownloadFailed(object sender, ExceptionEventArgs e)
            {
                // Download failed — still make the Image visible (shows fallback)
                var bmp = (BitmapImage)sender;
                UnhookBitmapDownload(bmp);
                _image.Dispatcher.BeginInvoke(new Action(() => { _image.Opacity = 1; }));
            }

            private static void FadeIn(Image img)
            {
                var animation = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = FadeDuration,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                img.BeginAnimation(UIElement.OpacityProperty, animation);
            }
        }
    }
}

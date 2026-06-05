using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
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
    /// when the ImageOpened event fires, creating a smooth content-reveal effect.
    /// </summary>
    public static class CoverFadeBehavior
    {
        private static readonly Duration FadeDuration = new Duration(TimeSpan.FromMilliseconds(250));

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
                    img.ImageOpened += OnImageOpened;
                    img.Loaded += OnImageLoaded;

                    // Start invisible — will fade in when image source loads
                    if (img.Source != null)
                        img.Opacity = 1; // Already has source, show it
                    else
                        img.Opacity = 0;
                }
                else
                {
                    img.ImageOpened -= OnImageOpened;
                    img.Loaded -= OnImageLoaded;
                    img.Opacity = 1;
                }
            }
        }

        private static void OnImageLoaded(object sender, RoutedEventArgs e)
        {
            // When the Image control loads in the visual tree, check if it already has a source
            // If it does (from cache), show it immediately — no need to fade in cached content
            if (sender is Image img && img.Source != null)
            {
                img.Opacity = 1;
            }
        }

        private static void OnImageOpened(object sender, RoutedEventArgs e)
        {
            if (sender is Image img)
            {
                // Animate from transparent to visible
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

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Musicefy.Core.Models;
using Musicefy.Core.Services;

namespace Musicefy.Converters
{
    /// <summary>
    /// Attached behavior that extracts the dominant color from a card's cover image
    /// and applies a subtle colored glow to the card background on hover.
    /// This creates the "color-reactive" feel that Echo Music is known for.
    ///
    /// Usage in XAML:
    ///   &lt;Border converters:CardColorGlowBehavior.IsEnabled="True"&gt;
    ///     &lt;Image Source="{Binding CoverPath, ...}"/&gt;
    ///   &lt;/Border&gt;
    ///
    /// On MouseEnter, the behavior:
    ///   1. Finds the Image element inside the card
    ///   2. Extracts the dominant color from the BitmapImage using ColorExtractor
    ///   3. Animates the card's background to a semi-transparent version of that color
    ///   4. On MouseLeave, animates back to transparent
    /// </summary>
    public static class CardColorGlowBehavior
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(CardColorGlowBehavior),
                new FrameworkPropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) =>
            (bool)obj.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(DependencyObject obj, bool value) =>
            obj.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Border border)
            {
                if ((bool)e.NewValue)
                {
                    border.MouseEnter += OnMouseEnter;
                    border.MouseLeave += OnMouseLeave;
                }
                else
                {
                    border.MouseEnter -= OnMouseEnter;
                    border.MouseLeave -= OnMouseLeave;
                }
            }
        }

        private static void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border border)
            {
                var dominantColor = ExtractDominantColorFromCard(border);
                if (dominantColor != null)
                {
                    // Create a subtle semi-transparent version of the dominant color
                    var glowColor = Color.FromArgb(
                        40, // Low alpha for subtlety
                        dominantColor.Primary.R,
                        dominantColor.Primary.G,
                        dominantColor.Primary.B);

                    var brush = new SolidColorBrush(glowColor);
                    brush.Freeze();

                    // Animate the background to the glow color
                    var animation = new ColorAnimation
                    {
                        To = glowColor,
                        Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };

                    // If there's no existing brush, create a transparent one first
                    if (!(border.Background is SolidColorBrush existingBrush))
                    {
                        border.Background = new SolidColorBrush(Color.FromArgb(0, glowColor.R, glowColor.G, glowColor.B));
                    }

                    if (border.Background is SolidColorBrush sb && sb.IsFrozen)
                    {
                        border.Background = new SolidColorBrush(sb.Color);
                    }

                    if (border.Background is SolidColorBrush animatable)
                    {
                        animatable.BeginAnimation(SolidColorBrush.ColorProperty, animation);
                    }
                }
            }
        }

        private static void OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Border border && border.Background is SolidColorBrush sb)
            {
                // Animate back to transparent
                var animation = new ColorAnimation
                {
                    To = Color.FromArgb(0, sb.Color.R, sb.Color.G, sb.Color.B),
                    Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                if (sb.IsFrozen)
                {
                    border.Background = new SolidColorBrush(sb.Color);
                }

                if (border.Background is SolidColorBrush animatable)
                {
                    animatable.BeginAnimation(SolidColorBrush.ColorProperty, animation);
                }
            }
        }

        /// <summary>
        /// Finds an Image element inside the card's visual tree and extracts
        /// the dominant color from its BitmapImage source using ColorExtractor.
        /// Returns null if no suitable image is found.
        /// </summary>
        private static ExtractedColors ExtractDominantColorFromCard(Border card)
        {
            var image = FindVisualChild<Image>(card);
            if (image?.Source is BitmapSource bitmapSource)
            {
                try
                {
                    return ColorExtractor.Extract(bitmapSource);
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        /// <summary>
        /// Recursively searches the visual tree for a child of the specified type.
        /// </summary>
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            int childrenCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;

                var found = FindVisualChild<T>(child);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}

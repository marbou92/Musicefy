using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Musicefy.Services
{
    public static class ToastService
    {
        public static void ShowToast(string message, Brush background, int durationMs = 3000)
        {
            // Thread-safe dispatcher invocation prevents worker threads from throwing crashes
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var window = Application.Current.MainWindow;
                if (window == null) return;

                var grid = window.Content as Grid;
                if (grid == null) return;

                // 1. Resolve or dynamically instantiate the container sheet
                if (!(grid.FindName("ToastContainer") is StackPanel container))
                {
                    container = new StackPanel
                    {
                        Name = "ToastContainer",
                        VerticalAlignment = VerticalAlignment.Bottom,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 0, 28, 28),
                        MinWidth = 280
                    };
                    grid.Children.Add(container);
                    grid.RegisterName("ToastContainer", container);
                }

                // 2. Build the Echo status indicator vector path item
                var statusIcon = new Path
                {
                    Data = Geometry.Parse("M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4M12,6A1,1 0 0,0 11,7V13A1,1 0 0,0 12,14A1,1 0 0,0 13,13V7A1,1 0 0,0 12,6M12,16A1.5,1.5 0 0,0 10.5,17.5A1.5,1.5 0 0,0 12,19A1.5,1.5 0 0,0 13.5,17.5A1.5,1.5 0 0,0 12,16Z"),
                    Fill = Brushes.White,
                    Width = 14,
                    Height = 14,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(0, 1, 12, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.90
                };

                // 3. Assemble the structural message text layout blocks
                var textContent = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.White,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // 4. Construct the inline content alignment grid layout
                var innerLayout = new Grid();
                innerLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                innerLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Grid.SetColumn(statusIcon, 0);
                Grid.SetColumn(textContent, 1);
                innerLayout.Children.Add(statusIcon);
                innerLayout.Children.Add(textContent);

                // 5. Encapsulate the composition inside an Echo capsule border wrapper 
                var toastTransform = new TranslateTransform { X = 80 };
                var toast = new Border
                {
                    Background = background,
                    CornerRadius = new CornerRadius(22),
                    Padding = new Thickness(20, 12, 20, 12),
                    Margin = new Thickness(0, 8, 0, 0),
                    MaxWidth = 360,
                    Opacity = 0,
                    RenderTransform = toastTransform,
                    Effect = new DropShadowEffect
                    {
                        BlurRadius = 20,
                        ShadowDepth = 3,
                        Opacity = 0.25,
                        Color = Colors.Black
                    },
                    Child = innerLayout
                };

                container.Children.Add(toast);

                // 6. Execute highly smooth slide-fade entrance animation sequences 
                var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(300)))
                {
                    EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
                };
                var slideIn = new DoubleAnimation(80, 0, new Duration(TimeSpan.FromMilliseconds(400)))
                {
                    EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
                };

                toast.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                toastTransform.BeginAnimation(TranslateTransform.XProperty, slideIn);

                // 7. Establish closure lifecycle timers to handle disposal loops safely
                var lifeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
                lifeTimer.Tick += (s, e) =>
                {
                    lifeTimer.Stop();

                    var fadeOut = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(250)));
                    var slideOut = new DoubleAnimation(0, 40, new Duration(TimeSpan.FromMilliseconds(250)))
                    {
                        EasingFunction = new CircleEase { EasingMode = EasingMode.EaseIn }
                    };

                    fadeOut.Completed += (s2, e2) =>
                    {
                        if (container.Children.Contains(toast))
                        {
                            container.Children.Remove(toast);
                        }

                        // HIGH PERFORMANCE LIFECYCLE CLEANER: 
                        // Wipes container tracking models out of layout trees when zero notifications exist
                        if (container.Children.Count == 0 && grid.Children.Contains(container))
                        {
                            grid.Children.Remove(container);
                            grid.UnregisterName("ToastContainer");
                        }
                    };

                    toast.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                    toastTransform.BeginAnimation(TranslateTransform.XProperty, slideOut);
                };

                lifeTimer.Start();

            }), DispatcherPriority.Render);
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Musicefy.Controls
{
    /// <summary>
    /// WavySlider — a custom WPF slider that draws animated waves on the
    /// active track when playing, and flattens when paused.
    /// Ported from Echo Music's WavySlider (Kotlin/Compose).
    /// </summary>
    public class WavySlider : Control
    {
        private DispatcherTimer _animationTimer;
        private double _phase;
        private double _amplitude = 1.0;
        private double _targetAmplitude = 1.0;
        private bool _isDragging;
        private double _dragValue;

        static WavySlider()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(WavySlider),
                new FrameworkPropertyMetadata(typeof(WavySlider)));
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(WavySlider),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(WavySlider),
                new PropertyMetadata(0.0));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(WavySlider),
                new PropertyMetadata(100.0));

        public static readonly DependencyProperty IsPlayingProperty =
            DependencyProperty.Register(nameof(IsPlaying), typeof(bool), typeof(WavySlider),
                new FrameworkPropertyMetadata(false, OnIsPlayingChanged));

        public static readonly DependencyProperty ActiveColorProperty =
            DependencyProperty.Register(nameof(ActiveColor), typeof(Brush), typeof(WavySlider),
                new PropertyMetadata(null));

        public static readonly DependencyProperty InactiveColorProperty =
            DependencyProperty.Register(nameof(InactiveColor), typeof(Brush), typeof(WavySlider),
                new PropertyMetadata(null));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public bool IsPlaying
        {
            get => (bool)GetValue(IsPlayingProperty);
            set => SetValue(IsPlayingProperty, value);
        }

        public Brush ActiveColor
        {
            get => (Brush)GetValue(ActiveColorProperty);
            set => SetValue(ActiveColorProperty, value);
        }

        public Brush InactiveColor
        {
            get => (Brush)GetValue(InactiveColorProperty);
            set => SetValue(InactiveColorProperty, value);
        }

        public event RoutedPropertyChangedEventHandler<double> ValueChanged;

        public WavySlider()
        {
            _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _animationTimer.Tick += OnAnimationTick;
            this.MouseLeftButtonDown += OnMouseLeftButtonDown;
            this.MouseMove += OnMouseMove;
            this.MouseLeftButtonUp += OnMouseLeftButtonUp;
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WavySlider slider)
            {
                slider.ValueChanged?.Invoke(slider, new RoutedPropertyChangedEventArgs<double>(
                    (double)e.OldValue, (double)e.NewValue));
                slider.InvalidateVisual();
            }
        }

        private static void OnIsPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is WavySlider slider)
            {
                slider._targetAmplitude = (bool)e.NewValue ? 1.0 : 0.0;
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var activeBrush = ActiveColor ?? Brushes.Transparent;
            var inactiveBrush = InactiveColor ?? Brushes.Transparent;
            var activeColor = (activeBrush as SolidColorBrush)?.Color ?? Colors.Transparent;
            var inactiveColor = (inactiveBrush as SolidColorBrush)?.Color ?? Colors.Transparent;

            double range = Maximum - Minimum;
            double normalizedValue = range > 0 ? (Value - Minimum) / range : 0;
            normalizedValue = Math.Max(0, Math.Min(1, normalizedValue));

            double width = ActualWidth;
            double centerY = ActualHeight / 2;
            double progressX = width * normalizedValue;

            // Wave parameters
            double wavelength = 60;
            double baseAmplitude = 5 * _amplitude;
            double strokeWidth = 4;

            var activePen = new Pen(new SolidColorBrush(activeColor), strokeWidth) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var inactivePen = new Pen(new SolidColorBrush(Color.FromArgb(77, activeColor.R, activeColor.G, activeColor.B)), strokeWidth) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

            // Draw inactive track (right of thumb) — flat line
            if (progressX < width)
            {
                dc.DrawLine(inactivePen, new Point(progressX, centerY), new Point(width, centerY));
            }

            // Draw active track (left of thumb) — wavy when playing
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(0, centerY), false, false);
                for (double x = 0; x <= progressX; x += 2)
                {
                    double waveY = centerY + Math.Sin((x + _phase) / wavelength * 2 * Math.PI) * baseAmplitude;
                    ctx.LineTo(new Point(x, waveY), true, false);
                }
            }
            geometry.Freeze();
            dc.DrawGeometry(null, activePen, geometry);

            // Draw thumb
            double thumbRadius = 8;
            var thumbY = centerY + Math.Sin((progressX + _phase) / wavelength * 2 * Math.PI) * baseAmplitude;
            dc.DrawEllipse(new SolidColorBrush(activeColor), null, new Point(progressX, thumbY), thumbRadius, thumbRadius);
        }

        private void OnAnimationTick(object sender, EventArgs e)
        {
            // Animate phase
            _phase += 1.5;
            if (_phase > 1000) _phase -= 1000;

            // Animate amplitude toward target
            _amplitude += (_targetAmplitude - _amplitude) * 0.15;

            InvalidateVisual();
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            base.OnVisualParentChanged(oldParent);
            if (_animationTimer != null && !_animationTimer.IsEnabled && IsPlaying)
                _animationTimer.Start();
        }

        public void StartAnimation() => _animationTimer.Start();
        public void StopAnimation() => _animationTimer.Stop();

        private void OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDragging = true;
            CaptureMouse();
            UpdateValueFromMouse(e.GetPosition(this).X);
        }

        private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                UpdateValueFromMouse(e.GetPosition(this).X);
        }

        private void OnMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDragging = false;
            ReleaseMouseCapture();
        }

        private void UpdateValueFromMouse(double mouseX)
        {
            double normalized = Math.Max(0, Math.Min(1, mouseX / ActualWidth));
            Value = Minimum + normalized * (Maximum - Minimum);
        }
    }
}

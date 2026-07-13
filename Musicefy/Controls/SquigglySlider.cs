using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Musicefy.Controls
{
    /// <summary>
    /// SquigglySlider — a custom WPF slider that draws an animated sine wave
    /// across the entire track. The wave animates when playing and flattens
    /// when paused or dragging. The active portion is Primary color, the
    /// inactive is Primary at 30% opacity. A vertical bar marks the thumb.
    /// Ported from Echo Music's SquigglySlider (Kotlin/Compose).
    /// </summary>
    public class SquigglySlider : Control
    {
        private DispatcherTimer _animationTimer;
        private double _phaseOffset;
        private double _heightFraction = 1.0;
        private double _targetHeightFraction = 1.0;
        private bool _isDragging;

        static SquigglySlider()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SquigglySlider),
                new FrameworkPropertyMetadata(typeof(SquigglySlider)));
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(SquigglySlider),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(SquigglySlider),
                new PropertyMetadata(0.0));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(SquigglySlider),
                new PropertyMetadata(100.0));

        public static readonly DependencyProperty IsPlayingProperty =
            DependencyProperty.Register(nameof(IsPlaying), typeof(bool), typeof(SquigglySlider),
                new FrameworkPropertyMetadata(false, OnIsPlayingChanged));

        public static readonly DependencyProperty ActiveColorProperty =
            DependencyProperty.Register(nameof(ActiveColor), typeof(Brush), typeof(SquigglySlider),
                new PropertyMetadata(null));

        public static readonly DependencyProperty InactiveColorProperty =
            DependencyProperty.Register(nameof(InactiveColor), typeof(Brush), typeof(SquigglySlider),
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

        public SquigglySlider()
        {
            _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _animationTimer.Tick += OnAnimationTick;
            this.MouseLeftButtonDown += OnMouseLeftButtonDown;
            this.MouseMove += OnMouseMove;
            this.MouseLeftButtonUp += OnMouseLeftButtonUp;
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SquigglySlider slider)
            {
                slider.ValueChanged?.Invoke(slider, new RoutedPropertyChangedEventArgs<double>(
                    (double)e.OldValue, (double)e.NewValue));
                slider.InvalidateVisual();
            }
        }

        private static void OnIsPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SquigglySlider slider)
            {
                slider._targetHeightFraction = (bool)e.NewValue ? 1.0 : 0.0;
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var activeBrush = ActiveColor ?? Brushes.Transparent;
            var activeColor = (activeBrush as SolidColorBrush)?.Color ?? Colors.Transparent;
            var inactiveColor = Color.FromArgb(77, activeColor.R, activeColor.G, activeColor.B);

            double range = Maximum - Minimum;
            double normalizedValue = range > 0 ? (Value - Minimum) / range : 0;
            normalizedValue = Math.Max(0, Math.Min(1, normalizedValue));

            double width = ActualWidth;
            double centerY = ActualHeight / 2;
            double progressX = width * normalizedValue;

            // Wave parameters
            double wavelength = 80;
            double lineAmplitude = 6 * _heightFraction;
            double strokeWidth = 5;

            var activePen = new Pen(new SolidColorBrush(activeColor), strokeWidth)
            { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            var inactivePen = new Pen(new SolidColorBrush(inactiveColor), strokeWidth)
            { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

            // Build the full wave path
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                double waveStart = -_phaseOffset - wavelength / 2;
                ctx.BeginFigure(new Point(waveStart, centerY), false, false);
                for (double x = waveStart; x <= width; x += 2)
                {
                    double waveY = centerY + Math.Cos((x + _phaseOffset) / wavelength * 2 * Math.PI) * lineAmplitude;
                    ctx.LineTo(new Point(x, waveY), true, false);
                }
            }
            geometry.Freeze();

            // Draw active portion (clipped to progressX)
            var activeClip = new RectangleGeometry(new Rect(0, 0, progressX, ActualHeight));
            dc.PushClip(activeClip);
            dc.DrawGeometry(null, activePen, geometry);
            dc.Pop();

            // Draw inactive portion (clipped from progressX to width)
            var inactiveClip = new RectangleGeometry(new Rect(progressX, 0, width - progressX, ActualHeight));
            dc.PushClip(inactiveClip);
            dc.DrawGeometry(null, inactivePen, geometry);
            dc.Pop();

            // Draw vertical bar at thumb position
            double barHalfHeight = lineAmplitude + strokeWidth;
            if (barHalfHeight > 0.5)
            {
                var barPen = new Pen(new SolidColorBrush(activeColor), 5)
                { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                dc.DrawLine(barPen, new Point(progressX, centerY - barHalfHeight), new Point(progressX, centerY + barHalfHeight));
            }
        }

        private void OnAnimationTick(object sender, EventArgs e)
        {
            // Animate phase
            if (IsPlaying && !_isDragging)
            {
                _phaseOffset += 0.4;
                if (_phaseOffset > 1000) _phaseOffset -= 1000;
            }

            // Animate height fraction toward target
            _heightFraction += (_targetHeightFraction - _heightFraction) * 0.15;

            InvalidateVisual();
        }

        public void StartAnimation() => _animationTimer.Start();
        public void StopAnimation() => _animationTimer.Stop();

        private void OnMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDragging = true;
            _targetHeightFraction = 0;
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
            if (IsPlaying) _targetHeightFraction = 1.0;
            ReleaseMouseCapture();
        }

        private void UpdateValueFromMouse(double mouseX)
        {
            double normalized = Math.Max(0, Math.Min(1, mouseX / ActualWidth));
            Value = Minimum + normalized * (Maximum - Minimum);
        }
    }
}

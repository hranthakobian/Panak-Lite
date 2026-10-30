using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Panak.Controls {
    /// <summary>
    /// Material 3 Expressive Wavy Progress Bar Control.
    /// Draws a soft pill background track and a fluid sinusoidal active wave indicator
    /// with rounded caps, edge tapering, and continuous 60fps wave motion.
    /// </summary>
    public class WavyProgressBar : ProgressBar {
        public static readonly DependencyProperty WaveAmplitudeProperty =
            DependencyProperty.Register(nameof(WaveAmplitude), typeof(double), typeof(WavyProgressBar),
                new FrameworkPropertyMetadata(2.8, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty WaveLengthProperty =
            DependencyProperty.Register(nameof(WaveLength), typeof(double), typeof(WavyProgressBar),
                new FrameworkPropertyMetadata(24.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty WaveStrokeThicknessProperty =
            DependencyProperty.Register(nameof(WaveStrokeThickness), typeof(double), typeof(WavyProgressBar),
                new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TrackHeightProperty =
            DependencyProperty.Register(nameof(TrackHeight), typeof(double), typeof(WavyProgressBar),
                new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty WaveSpeedProperty =
            DependencyProperty.Register(nameof(WaveSpeed), typeof(double), typeof(WavyProgressBar),
                new FrameworkPropertyMetadata(28.0, FrameworkPropertyMetadataOptions.None));

        public double WaveAmplitude {
            get => (double)GetValue(WaveAmplitudeProperty);
            set => SetValue(WaveAmplitudeProperty, value);
        }

        public double WaveLength {
            get => (double)GetValue(WaveLengthProperty);
            set => SetValue(WaveLengthProperty, value);
        }

        public double WaveStrokeThickness {
            get => (double)GetValue(WaveStrokeThicknessProperty);
            set => SetValue(WaveStrokeThicknessProperty, value);
        }

        public double TrackHeight {
            get => (double)GetValue(TrackHeightProperty);
            set => SetValue(TrackHeightProperty, value);
        }

        public double WaveSpeed {
            get => (double)GetValue(WaveSpeedProperty);
            set => SetValue(WaveSpeedProperty, value);
        }

        private double _phase = 0.0;
        private double _indeterminateOffset = 0.0;
        private bool _isIndeterminateForward = true;
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private long _lastTicks = 0;
        private bool _isHooked = false;

        static WavyProgressBar() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(WavyProgressBar), new FrameworkPropertyMetadata(typeof(WavyProgressBar)));
            IsIndeterminateProperty.OverrideMetadata(typeof(WavyProgressBar),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, (d, e) => ((WavyProgressBar)d).UpdateAnimationState()));
        }

        public WavyProgressBar() {
            Height = 16;
            Background = null;
            Foreground = null;

            Loaded += (s, e) => UpdateAnimationState();
            Unloaded += (s, e) => UpdateAnimationState();
            IsVisibleChanged += (s, e) => UpdateAnimationState();
        }

        protected override void OnValueChanged(double oldValue, double newValue) {
            base.OnValueChanged(oldValue, newValue);
            UpdateAnimationState();
            InvalidateVisual();
        }

        private void UpdateAnimationState() {
            bool hasActiveWave = IsIndeterminate || (Value > Minimum && Value < Maximum);
            bool shouldAnimate = IsLoaded && IsVisible && hasActiveWave;
            if (shouldAnimate && !_isHooked) {
                _stopwatch.Restart();
                _lastTicks = _stopwatch.ElapsedTicks;
                CompositionTarget.Rendering += OnRendering;
                _isHooked = true;
            } else if (!shouldAnimate && _isHooked) {
                CompositionTarget.Rendering -= OnRendering;
                _isHooked = false;
                _stopwatch.Stop();
            }
        }

        private void OnRendering(object? sender, EventArgs e) {
            if (!IsLoaded || !IsVisible) {
                UpdateAnimationState();
                return;
            }

            long currentTicks = _stopwatch.ElapsedTicks;
            double dt = (double)(currentTicks - _lastTicks) / Stopwatch.Frequency;
            if (dt < 0.030) {
                return; // Cap wave animation at ~33 FPS to avoid saturating 144Hz/240Hz displays
            }
            _lastTicks = currentTicks;

            if (dt <= 0 || dt > 0.1) {
                dt = 0.033;
            }

            double speed = WaveSpeed;
            double wl = Math.Max(10.0, WaveLength);
            _phase = (_phase + dt * speed * (2.0 * Math.PI / wl)) % (2.0 * Math.PI);

            if (IsIndeterminate) {
                double travelSpeed = ActualWidth > 0 ? (ActualWidth * 0.55) : 90.0;
                double maxTravel = Math.Max(0.0, ActualWidth - (ActualWidth * 0.35));
                if (maxTravel > 0) {
                    if (_isIndeterminateForward) {
                        _indeterminateOffset += dt * travelSpeed;
                        if (_indeterminateOffset >= maxTravel) {
                            _indeterminateOffset = maxTravel;
                            _isIndeterminateForward = false;
                        }
                    } else {
                        _indeterminateOffset -= dt * travelSpeed;
                        if (_indeterminateOffset <= 0) {
                            _indeterminateOffset = 0;
                            _isIndeterminateForward = true;
                        }
                    }
                }
            }

            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc) {
            double width = ActualWidth;
            double height = ActualHeight;

            if (width <= 0 || height <= 0) {
                return;
            }

            double centerY = height / 2.0;
            double trackH = Math.Max(2.0, TrackHeight);
            double trackRadius = trackH / 2.0;
            const double m3Gap = 4.0; // Official Material 3 gap between active and inactive tracks

            Brush trackBrush = Background
                ?? (TryFindResource("M3TrackInactiveBrush") as Brush)
                ?? new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));

            Brush activeBrush = Foreground
                ?? (TryFindResource("M3PrimaryBrush") as Brush)
                ?? new SolidColorBrush(Color.FromRgb(0x7A, 0x54, 0x27));

            double strokeW = Math.Max(2.0, WaveStrokeThickness);
            var wavePen = new Pen(activeBrush, strokeW) {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            wavePen.Freeze();

            double amp = WaveAmplitude;
            double wl = Math.Max(10.0, WaveLength);

            if (IsIndeterminate) {
                double segW = Math.Max(20.0, width * 0.35);
                double startX = _indeterminateOffset;
                double endX = Math.Min(width, startX + segW);

                // M3 Expressive: Inactive track is drawn ONLY where the wave is not (with gap)
                // 1. Left inactive track segment
                if (startX > m3Gap + 1.0) {
                    double leftW = Math.Max(1.0, startX - m3Gap);
                    dc.DrawRoundedRectangle(trackBrush, null, new Rect(0, centerY - trackH / 2.0, leftW, trackH), trackRadius, trackRadius);
                }

                // 2. Right inactive track segment
                if (endX + m3Gap < width - 1.0) {
                    double rightX = endX + m3Gap;
                    double rightW = Math.Max(1.0, width - rightX);
                    dc.DrawRoundedRectangle(trackBrush, null, new Rect(rightX, centerY - trackH / 2.0, rightW, trackH), trackRadius, trackRadius);
                }

                // 3. Active Wave Indicator (no track underneath!)
                DrawWavySegment(dc, wavePen, startX, endX, centerY, amp, wl, _phase);
            } else {
                double range = Maximum - Minimum;
                double fraction = range > 0 ? Math.Clamp((Value - Minimum) / range, 0.0, 1.0) : 0.0;
                double activeW = width * fraction;

                if (activeW <= 1.5) {
                    // Full track when zero progress
                    dc.DrawRoundedRectangle(trackBrush, null, new Rect(0, centerY - trackH / 2.0, width, trackH), trackRadius, trackRadius);
                } else {
                    // M3 Expressive: Inactive track only for the remaining unfulfilled portion
                    if (activeW + m3Gap < width - 1.0) {
                        double remX = activeW + m3Gap;
                        double remW = Math.Max(1.0, width - remX);
                        dc.DrawRoundedRectangle(trackBrush, null, new Rect(remX, centerY - trackH / 2.0, remW, trackH), trackRadius, trackRadius);
                    }

                    // Active wave indicator (no straight track underneath!)
                    DrawWavySegment(dc, wavePen, 0.0, activeW, centerY, amp, wl, _phase);
                }
            }
        }

        private static void DrawWavySegment(
            DrawingContext dc,
            Pen pen,
            double startX,
            double endX,
            double centerY,
            double amplitude,
            double wavelength,
            double phase) {

            double length = endX - startX;
            if (length <= 1.0) {
                return;
            }

            if (length < 8.0) {
                // Short dot/pill
                dc.DrawLine(pen, new Point(startX, centerY), new Point(endX, centerY));
                return;
            }

            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open()) {
                ctx.BeginFigure(new Point(startX, centerY), isFilled: false, isClosed: false);

                // High-precision 1px step for mathematically flawless, delicate sinusoidal wave
                double step = 1.0;
                double taperDist = Math.Min(16.0, length * 0.25);

                for (double x = startX + step; x < endX; x += step) {
                    double distFromStart = x - startX;
                    double distFromEnd = endX - x;

                    // Ultra-smooth Hann window envelope at both endpoints for seamless baseline convergence
                    double taperStart = distFromStart < taperDist
                        ? 0.5 * (1.0 - Math.Cos((distFromStart / taperDist) * Math.PI))
                        : 1.0;
                    double taperEnd = distFromEnd < taperDist
                        ? 0.5 * (1.0 - Math.Cos((distFromEnd / taperDist) * Math.PI))
                        : 1.0;
                    double envelope = Math.Min(taperStart, taperEnd);

                    double y = centerY + amplitude * envelope * Math.Sin((2.0 * Math.PI * (x - startX) / wavelength) - phase);
                    ctx.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: true);
                }

                ctx.LineTo(new Point(endX, centerY), isStroked: true, isSmoothJoin: true);
            }
            geometry.Freeze();

            dc.DrawGeometry(null, pen, geometry);
        }
    }
}

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Panak.Services;

namespace Panak.Views {
    public partial class BackdropWindow : Window {
        public event Action? ClickedOutside;

        private DispatcherTimer? _realtimeBlurTimer;
        private Rect _currentScreenBounds;
        private bool _blurEnabled = false;
        private double _blurRadius = 28.0;
        private bool _isUpdatingBlur = false;

        public BackdropWindow() {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);
            try {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                IntPtr exStyle = ShellHelper.GetWindowLong(hwnd, ShellHelper.GWL_EXSTYLE);
                ShellHelper.SetWindowLong(hwnd, ShellHelper.GWL_EXSTYLE, (IntPtr)(exStyle.ToInt64() | ShellHelper.WS_EX_NOACTIVATE));
                if (ShellHelper.IsWindows10Build19041OrGreater) {
                    ShellHelper.SetWindowDisplayAffinity(hwnd, ShellHelper.WDA_EXCLUDEFROMCAPTURE);
                }
            } catch { }
        }

        public void ShowBackdrop(Rect screenBounds, double targetDimmingOpacity = 0.40, bool blurEnabled = true, double blurRadius = 28.0) {
            _currentScreenBounds = screenBounds;
            _blurEnabled = blurEnabled;
            _blurRadius = blurRadius;

            double dpi = 1.0;
            try {
                dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
            } catch { }
            if (dpi <= 0) dpi = 1.0;

            Left = screenBounds.Left / dpi;
            Top = screenBounds.Top / dpi;
            Width = screenBounds.Width / dpi;
            Height = screenBounds.Height / dpi;

            if (blurEnabled && blurRadius > 0) {
                double radius = Math.Clamp(blurRadius, 1.0, 40.0);
                bool isHwAccelerated = (RenderCapability.Tier >> 16) >= 2;
                if (!isHwAccelerated) {
                    radius = Math.Min(radius, 12.0);
                }
                if (BackdropBlurEffect == null) {
                    BackdropBlurEffect = new System.Windows.Media.Effects.BlurEffect { KernelType = System.Windows.Media.Effects.KernelType.Gaussian, RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance };
                }
                BackdropBlurEffect.Radius = radius;
                BlurImage.Effect = BackdropBlurEffect;

                UpdateBackdropFrame(force: true);
                StartRealtimeBlurTimer();
            } else {
                StopRealtimeBlurTimer();
                BlurImage.Source = null;
                BlurImage.Effect = null;
                BlurImage.Visibility = Visibility.Collapsed;
            }

            DimOverlay.Opacity = Math.Clamp(targetDimmingOpacity, 0.0, 1.0);

            Show();

            DoubleAnimation fadeIn = new DoubleAnimation {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BackdropGrid.BeginAnimation(OpacityProperty, fadeIn);
        }

        private void StartRealtimeBlurTimer() {
            StopRealtimeBlurTimer();
            if (!_blurEnabled || _blurRadius <= 0) return;

            int intervalMs = ShellHelper.IsLiveWallpaperEngineRunning() ? 80 : 150;
            _realtimeBlurTimer = new DispatcherTimer(DispatcherPriority.Background) {
                Interval = TimeSpan.FromMilliseconds(intervalMs)
            };
            _realtimeBlurTimer.Tick += (s, e) => UpdateBackdropFrame();
            _realtimeBlurTimer.Start();
        }

        private void StopRealtimeBlurTimer() {
            if (_realtimeBlurTimer != null) {
                try {
                    _realtimeBlurTimer.Stop();
                } catch { }
                _realtimeBlurTimer = null;
            }
        }

        private void UpdateBackdropFrame(bool force = false) {
            if (!force && !IsVisible && BackdropGrid.Opacity <= 0) return;
            if (_isUpdatingBlur) return;
            _isUpdatingBlur = true;

            try {
                // First try capturing the actual screen area (open apps, programs, videos, desktop)
                var screenBmp = ShellHelper.CaptureScreenArea(
                    (int)Math.Round(_currentScreenBounds.Left),
                    (int)Math.Round(_currentScreenBounds.Top),
                    (int)Math.Round(_currentScreenBounds.Width),
                    (int)Math.Round(_currentScreenBounds.Height),
                    dpiScale: 1.0,
                    downsampleForBlur: true);

                // If screen capture failed (e.g. desktop locked or transitioning), fall back to live wallpaper or static wallpaper
                if (screenBmp == null) {
                    if (ShellHelper.IsLiveWallpaperEngineRunning()) {
                        screenBmp = ShellHelper.CaptureLiveWallpaper();
                    }
                    if (screenBmp == null) {
                        screenBmp = ShellHelper.GetDesktopWallpaperBitmap();
                    }
                }

                if (screenBmp != null) {
                    BlurImage.Source = screenBmp;
                    BlurImage.Visibility = Visibility.Visible;
                } else {
                    BlurImage.Source = null;
                    BlurImage.Visibility = Visibility.Collapsed;
                }
            } catch {
                // Fallback to desktop wallpaper on error
                try {
                    var fallback = ShellHelper.GetDesktopWallpaperBitmap();
                    if (fallback != null) {
                        BlurImage.Source = fallback;
                        BlurImage.Visibility = Visibility.Visible;
                    } else {
                        BlurImage.Source = null;
                        BlurImage.Visibility = Visibility.Collapsed;
                    }
                } catch {
                    BlurImage.Source = null;
                    BlurImage.Visibility = Visibility.Collapsed;
                }
            } finally {
                _isUpdatingBlur = false;
            }
        }

        public void HideBackdrop(Action? onCompleted = null) {
            StopRealtimeBlurTimer();
            DoubleAnimation fadeOut = new DoubleAnimation {
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(180),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += (s, e) => {
                Hide();
                BlurImage.Source = null;
                BlurImage.Effect = null;
                BlurImage.Visibility = Visibility.Collapsed;
                onCompleted?.Invoke();
            };
            BackdropGrid.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void BackdropGrid_MouseDown(object sender, MouseButtonEventArgs e) {
            ClickedOutside?.Invoke();
        }
    }
}

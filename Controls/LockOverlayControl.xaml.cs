using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Panak.Models;
using Panak.Services;

namespace Panak.Controls {
    public partial class LockOverlayControl : UserControl {
        public event Action? Unlocked;
        public event Action? LockRemoved;
        public event Action? Cancelled;

        private string _targetFolderPath = string.Empty;
        private bool _isSettingsMode = false;
        private bool _isRemoveLockMode = false;
        private FolderLockInfo? _folderLockInfo;
        private string _currentPin = string.Empty;
        private readonly List<int> _currentPattern = new();
        private string _currentFolderColor = "#EA580C";
        private bool _isDarkTheme = true;
        private Brush _primaryBrush = new SolidColorBrush(Color.FromRgb(234, 88, 12));

        public LockOverlayControl() {
            InitializeComponent();
        }

        public void ApplyFolderTheme(string hexColor, bool isDarkTheme) {
            if (string.IsNullOrEmpty(hexColor)) {
                hexColor = "#EA580C";
            }
            _currentFolderColor = hexColor;
            _isDarkTheme = isDarkTheme;

            try {
                double hue = ShellHelper.GetHueFromHex(hexColor);
                var (_, _, primaryHex) = ShellHelper.GetMaterialYouTonalPaletteHex(hue);

                var primaryColor = (Color)ColorConverter.ConvertFromString(primaryHex);
                _primaryBrush = new SolidColorBrush(primaryColor);

                var textFg = isDarkTheme
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(Colors.Black);
                var subtextFg = isDarkTheme
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(Colors.Black);

                // Container background tinted with folder color
                Color containerColor = isDarkTheme
                    ? Color.FromArgb(50, primaryColor.R, primaryColor.G, primaryColor.B)
                    : Color.FromArgb(32, primaryColor.R, primaryColor.G, primaryColor.B);

                Color hoverColor = isDarkTheme
                    ? Color.FromArgb(85, primaryColor.R, primaryColor.G, primaryColor.B)
                    : Color.FromArgb(60, primaryColor.R, primaryColor.G, primaryColor.B);

                Color pressedColor = isDarkTheme
                    ? Color.FromArgb(125, primaryColor.R, primaryColor.G, primaryColor.B)
                    : Color.FromArgb(90, primaryColor.R, primaryColor.G, primaryColor.B);

                Color borderColor = isDarkTheme
                    ? Color.FromArgb(80, primaryColor.R, primaryColor.G, primaryColor.B)
                    : Color.FromArgb(60, primaryColor.R, primaryColor.G, primaryColor.B);

                Color badgeBg = Color.FromArgb(45, primaryColor.R, primaryColor.G, primaryColor.B);

                Resources["LockPrimaryBrush"] = _primaryBrush;
                Resources["LockOnPrimaryBrush"] = new SolidColorBrush(Colors.White);
                Resources["LockContainerBrush"] = new SolidColorBrush(containerColor);
                Resources["LockOnContainerBrush"] = textFg;
                Resources["LockBtnHoverBrush"] = new SolidColorBrush(hoverColor);
                Resources["LockBtnPressedBrush"] = new SolidColorBrush(pressedColor);
                Resources["LockBorderBrush"] = new SolidColorBrush(borderColor);
                Resources["LockBadgeBgBrush"] = new SolidColorBrush(badgeBg);
                Resources["LockTextBrush"] = textFg;
                Resources["LockSubtextBrush"] = subtextFg;

                LockTitleText.Foreground = textFg;
                LockSubtitleText.Foreground = subtextFg;

                if (PasswordInputContainer != null) {
                    PasswordInputContainer.Background = new SolidColorBrush(containerColor);
                    PasswordInputContainer.BorderBrush = new SolidColorBrush(borderColor);
                }

                // Refresh pattern graphics if initialized
                if (_patternPolyline != null) {
                    _patternPolyline.Stroke = _primaryBrush;
                }
                if (_patternActiveLine != null) {
                    _patternActiveLine.Stroke = new SolidColorBrush(Color.FromArgb(180, primaryColor.R, primaryColor.G, primaryColor.B));
                }
                for (int i = 0; i < 9; i++) {
                    if (_patternDotRings[i] != null) {
                        _patternDotRings[i].Stroke = new SolidColorBrush(Color.FromArgb(160, primaryColor.R, primaryColor.G, primaryColor.B));
                    }
                }
                UpdatePinDots();
            } catch { }
        }

        public void ApplyThemeContrast(bool isDarkTheme) {
            ApplyFolderTheme(_currentFolderColor, isDarkTheme);
        }

        public void SetupForFolder(string folderPath) {
            _targetFolderPath = folderPath;
            _isSettingsMode = false;
            _isRemoveLockMode = false;
            _folderLockInfo = FolderLockService.Instance.GetFolderLockInfo(folderPath);

            try {
                if (Directory.Exists(folderPath)) {
                    var data = PanakFolderData.Load(folderPath);
                    if (!string.IsNullOrEmpty(data.Color)) {
                        _currentFolderColor = data.Color;
                    }
                }
            } catch { }

            ApplyFolderTheme(_currentFolderColor, _isDarkTheme);
            if (RemoveLockCancelBtn != null) RemoveLockCancelBtn.Visibility = Visibility.Collapsed;
            SetupUI(_folderLockInfo?.LockType ?? "Password");
        }

        public void SetupForRemoveLock(string folderPath) {
            _targetFolderPath = folderPath;
            _isSettingsMode = false;
            _isRemoveLockMode = true;
            _folderLockInfo = FolderLockService.Instance.GetFolderLockInfo(folderPath);

            try {
                if (Directory.Exists(folderPath)) {
                    var data = PanakFolderData.Load(folderPath);
                    if (!string.IsNullOrEmpty(data.Color)) {
                        _currentFolderColor = data.Color;
                    }
                }
            } catch { }

            ApplyFolderTheme(_currentFolderColor, _isDarkTheme);
            SetupUI(_folderLockInfo?.LockType ?? "Password");

            var loc = LocalizationService.Instance;
            LockIconText.Text = "🔓";
            LockTitleText.Text = loc["RemoveLock"] ?? "Հեռացնել փականը";
            LockSubtitleText.Text = loc["RemoveLockSubtitle"] ?? "Մուտքագրեք ընթացիկ գաղտնաբառը կամ կոդը";
            if (RemoveLockCancelBtn != null) {
                RemoveLockCancelBtn.Content = loc["Cancel"] ?? "Չեղարկել";
                RemoveLockCancelBtn.Visibility = Visibility.Visible;
            }
        }

        public bool IsRemoveLockMode => _isRemoveLockMode;

        public void CancelRemoveLock() {
            _isRemoveLockMode = false;
            ErrorBadge.Visibility = Visibility.Collapsed;
            Cancelled?.Invoke();
        }

        private void RemoveLockCancelBtn_Click(object sender, RoutedEventArgs e) {
            CancelRemoveLock();
        }

        public void SetupForSettings() {
            _targetFolderPath = string.Empty;
            _isSettingsMode = true;
            _isRemoveLockMode = false;
            _folderLockInfo = null;
            _currentFolderColor = "#EA580C";
            ApplyFolderTheme(_currentFolderColor, _isDarkTheme);
            if (RemoveLockCancelBtn != null) RemoveLockCancelBtn.Visibility = Visibility.Collapsed;
            SetupUI(FolderLockService.Instance.SettingsLockType);
        }

        private void SetupUI(string lockType) {
            _currentPin = string.Empty;
            _currentPattern.Clear();
            ErrorBadge.Visibility = Visibility.Collapsed;

            PinContainer.Visibility = Visibility.Collapsed;
            PatternContainer.Visibility = Visibility.Collapsed;
            PasswordContainer.Visibility = Visibility.Collapsed;

            var loc = LocalizationService.Instance;

            if (PatternInstructionsText != null) {
                PatternInstructionsText.Text = loc["PatternDesc"] ?? "Քաշեք մկնիկով՝ նախշը միացնելու համար";
            }
            if (PatternClearBtn != null) {
                PatternClearBtn.Content = loc["ClearPattern"] ?? "Մաքրել նախշը";
            }
            if (RemoveLockCancelBtn != null) {
                RemoveLockCancelBtn.Content = loc["Cancel"] ?? "Չեղարկել";
            }

            switch (lockType) {
                case "PIN":
                    PinContainer.Visibility = Visibility.Visible;
                    LockTitleText.Text = loc["EnterPinToUnlock"] ?? "Մուտքագրեք PIN կոդը";
                    LockSubtitleText.Text = loc["PinDesc"] ?? "Հավաքեք թվային կոդը";
                    UpdatePinDots();
                    FocusInput();
                    break;
                case "Pattern":
                    PatternContainer.Visibility = Visibility.Visible;
                    LockTitleText.Text = loc["DrawPatternToUnlock"] ?? "Գծեք նախշը";
                    LockSubtitleText.Text = loc["PatternDesc"] ?? "Քաշեք մկնիկով՝ նախշը միացնելու համար";
                    InitPatternCanvas();
                    ResetPatternDots();
                    break;
                default:
                    PasswordContainer.Visibility = Visibility.Visible;
                    LockTitleText.Text = loc["EnterPasswordToUnlock"] ?? "Մուտքագրեք գաղտնաբառը";
                    LockSubtitleText.Text = loc["PasswordDesc"] ?? "Հավաքեք պանակի գաղտնաբառը";
                    PasswordInputBox.Password = string.Empty;
                    PasswordInputBox.Focus();
                    break;
            }
        }

        // --- PIN Handlers ---
        private static readonly Geometry[] ExpressiveGeometries = InitializeExpressiveGeometries();

        private static Geometry[] InitializeExpressiveGeometries() {
            var strings = new[] {
                // 0: Star (4-pointed star)
                "M59.2054 1.00063C76.0771 -4.17958 91.8739 11.6172 86.6937 28.4889L83.9608 37.3899C82.6688 41.5978 82.6688 46.0965 83.9608 50.3044L86.6937 59.2054C91.8739 76.0771 76.0771 91.8739 59.2054 86.6937L50.3044 83.9608C46.0965 82.6688 41.5978 82.6688 37.3899 83.9608L28.4889 86.6937C11.6172 91.8739 -4.17958 76.0771 1.00063 59.2054L3.73355 50.3044C5.02553 46.0965 5.02553 41.5978 3.73355 37.3899L1.00063 28.4889C-4.17958 11.6172 11.6172 -4.17958 28.4889 1.00063L37.3899 3.73355C41.5978 5.02553 46.0965 5.02553 50.3044 3.73355L59.2054 1.00063Z",
                // 1: Scallop (Flower)
                "M 12.00,0.80 Q 14.54,2.53 17.60,2.30 Q 18.93,5.07 21.70,6.40 Q 21.47,9.46 23.20,12.00 Q 21.47,14.54 21.70,17.60 Q 18.93,18.93 17.60,21.70 Q 14.54,21.47 12.00,23.20 Q 9.46,21.47 6.40,21.70 Q 5.07,18.93 2.30,17.60 Q 2.53,14.54 0.80,12.00 Q 2.53,9.46 2.30,6.40 Q 5.07,5.07 6.40,2.30 Q 9.46,2.53 12.00,0.80 Z",
                // 2: Squircle
                "M 7,1 L 17,1 A 6,6 0 0 1 23,7 L 23,17 A 6,6 0 0 1 17,23 L 7,23 A 6,6 0 0 1 1,17 L 1,7 A 6,6 0 0 1 7,1 Z",
                // 3: Arch
                "M 1,12 A 11,11 0 0 1 23,12 L 23,19 A 4,4 0 0 1 19,23 L 5,23 A 4,4 0 0 1 1,19 Z",
                // 4: Circle
                "M 12,1 A 11,11 0 1 1 11.99,1 Z"
            };

            var geoms = new Geometry[strings.Length];
            for (int i = 0; i < strings.Length; i++) {
                try {
                    geoms[i] = Geometry.Parse(strings[i]);
                    geoms[i].Freeze();
                } catch {
                    geoms[i] = Geometry.Parse("M 12,1 A 11,11 0 1 1 11.99,1 Z");
                    geoms[i].Freeze();
                }
            }
            return geoms;
        }

        private void NumpadBtn_Click(object sender, RoutedEventArgs e) {
            if (sender is Button b && b.Content is string digit) {
                if (_currentPin.Length < 32) {
                    _currentPin += digit;
                    UpdatePinDots();
                    CheckPinComplete();
                }
            }
        }

        private void PinClear_Click(object sender, RoutedEventArgs e) {
            _currentPin = string.Empty;
            UpdatePinDots();
            ErrorBadge.Visibility = Visibility.Collapsed;
        }

        private void PinBackspace_Click(object sender, RoutedEventArgs e) {
            if (_currentPin.Length > 0) {
                _currentPin = _currentPin.Substring(0, _currentPin.Length - 1);
                UpdatePinDots();
                ErrorBadge.Visibility = Visibility.Collapsed;
            }
        }

        private void PinSubmit_Click(object sender, RoutedEventArgs e) {
            if (!string.IsNullOrEmpty(_currentPin)) {
                ValidateCredential(_currentPin);
            }
        }

        private void UpdatePinDots() {
            if (PinDotsPanel == null) return;

            int targetCount = _currentPin.Length;
            int currentCount = PinDotsPanel.Children.Count;

            if (targetCount == 0) {
                PinDotsPanel.Children.Clear();
                if (PinDotsTranslate != null) {
                    PinDotsTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                    PinDotsTranslate.X = 0;
                }
                return;
            }

            // If digits were removed (backspace):
            if (targetCount < currentCount) {
                while (PinDotsPanel.Children.Count > targetCount) {
                    PinDotsPanel.Children.RemoveAt(PinDotsPanel.Children.Count - 1);
                }

                // Smooth slide backward into center
                if (PinDotsTranslate != null) {
                    PinDotsTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                    PinDotsTranslate.X = -11.0;
                    var slideBack = new System.Windows.Media.Animation.DoubleAnimation(-11.0, 0.0, TimeSpan.FromMilliseconds(200)) {
                        EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                    };
                    PinDotsTranslate.BeginAnimation(TranslateTransform.XProperty, slideBack);
                }
                return;
            }

            // If one or more digits were added:
            if (targetCount > currentCount) {
                // Settle any previously active shapes to regular circles
                SettlePreviousDots();

                while (PinDotsPanel.Children.Count < targetCount) {
                    int dotIndex = PinDotsPanel.Children.Count;
                    var dotElement = CreateExpressivePinDot(dotIndex);
                    PinDotsPanel.Children.Add(dotElement);
                }

                // Smooth push to the side when a new number is typed
                if (PinDotsPanel.Children.Count > 1 && PinDotsTranslate != null) {
                    PinDotsTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                    PinDotsTranslate.X = 11.0;
                    var pushAnim = new System.Windows.Media.Animation.DoubleAnimation(11.0, 0.0, TimeSpan.FromMilliseconds(240)) {
                        EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                    };
                    PinDotsTranslate.BeginAnimation(TranslateTransform.XProperty, pushAnim);
                }
            }
        }

        private UIElement CreateExpressivePinDot(int index) {
            double dotSize = _currentPin.Length > 10 ? 9 : 11;
            double shapeSize = _currentPin.Length > 10 ? 16 : 20;
            double containerWidth = _currentPin.Length > 10 ? 16 : 22;

            var container = new Grid {
                Width = containerWidth,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = false
            };

            var activeFill = Resources["LockPrimaryBrush"] as Brush ?? _primaryBrush;

            // Expressive Shape (starts large, e.g. 20px)
            Geometry geom = ExpressiveGeometries[index % ExpressiveGeometries.Length];
            var shapeScale = new ScaleTransform(1.15, 1.15);
            var shapeRotate = new RotateTransform(-8);
            var shapeGroup = new TransformGroup();
            shapeGroup.Children.Add(shapeScale);
            shapeGroup.Children.Add(shapeRotate);

            var shapePath = new System.Windows.Shapes.Path {
                Data = geom,
                Fill = activeFill,
                Stretch = Stretch.Uniform,
                Width = shapeSize,
                Height = shapeSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = shapeGroup,
                Opacity = 1.0
            };

            // Regular Circle (standard small round dot, e.g. 11px)
            var circleScale = new ScaleTransform(0.55, 0.55);
            var circleDot = new Ellipse {
                Width = dotSize,
                Height = dotSize,
                Fill = activeFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = circleScale,
                Opacity = 0.0
            };

            container.Children.Add(shapePath);
            container.Children.Add(circleDot);

            // 1. Initial settling (from 1.15 to 1.0, rotation from -8 to 0) - directly large, no small dot:
            var settleScaleX = new System.Windows.Media.Animation.DoubleAnimation(1.15, 1.0, TimeSpan.FromMilliseconds(120)) {
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            var settleScaleY = new System.Windows.Media.Animation.DoubleAnimation(1.15, 1.0, TimeSpan.FromMilliseconds(120)) {
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            var settleRotate = new System.Windows.Media.Animation.DoubleAnimation(-8, 0, TimeSpan.FromMilliseconds(150)) {
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };

            shapeScale.BeginAnimation(ScaleTransform.ScaleXProperty, settleScaleX);
            shapeScale.BeginAnimation(ScaleTransform.ScaleYProperty, settleScaleY);
            shapeRotate.BeginAnimation(RotateTransform.AngleProperty, settleRotate);

            // 2. Morph to Regular Circle after ~260ms delay
            var morphDelay = TimeSpan.FromMilliseconds(260);
            var morphDuration = TimeSpan.FromMilliseconds(200);
            var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut };

            // Shape fades out and shrinks to circle center
            var shapeFade = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, morphDuration) {
                BeginTime = morphDelay,
                EasingFunction = ease
            };
            var shapeShrinkX = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.55, morphDuration) {
                BeginTime = morphDelay,
                EasingFunction = ease
            };
            var shapeShrinkY = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.55, morphDuration) {
                BeginTime = morphDelay,
                EasingFunction = ease
            };

            // Circle fades in and scales to full 1.0 size
            var circleFade = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, morphDuration) {
                BeginTime = morphDelay,
                EasingFunction = ease
            };
            var circleGrowX = new System.Windows.Media.Animation.DoubleAnimation(0.55, 1.0, morphDuration) {
                BeginTime = morphDelay,
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            var circleGrowY = new System.Windows.Media.Animation.DoubleAnimation(0.55, 1.0, morphDuration) {
                BeginTime = morphDelay,
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };

            shapePath.BeginAnimation(UIElement.OpacityProperty, shapeFade);
            shapeScale.BeginAnimation(ScaleTransform.ScaleXProperty, shapeShrinkX);
            shapeScale.BeginAnimation(ScaleTransform.ScaleYProperty, shapeShrinkY);

            circleDot.BeginAnimation(UIElement.OpacityProperty, circleFade);
            circleScale.BeginAnimation(ScaleTransform.ScaleXProperty, circleGrowX);
            circleScale.BeginAnimation(ScaleTransform.ScaleYProperty, circleGrowY);

            return container;
        }

        private void SettlePreviousDots() {
            if (PinDotsPanel == null) return;
            for (int i = 0; i < PinDotsPanel.Children.Count; i++) {
                if (PinDotsPanel.Children[i] is Grid grid && grid.Children.Count >= 2) {
                    if (grid.Children[0] is FrameworkElement shape && grid.Children[1] is FrameworkElement circle) {
                        if (shape.Opacity > 0.05) {
                            shape.BeginAnimation(UIElement.OpacityProperty, null);
                            shape.Opacity = 0.0;

                            circle.BeginAnimation(UIElement.OpacityProperty, null);
                            circle.Opacity = 1.0;
                            if (circle.RenderTransform is ScaleTransform st) {
                                st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                                st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                                st.ScaleX = 1.0;
                                st.ScaleY = 1.0;
                            }
                        }
                    }
                }
            }
        }

        private void CheckPinComplete() {
            if (string.IsNullOrEmpty(_currentPin)) return;

            bool success = false;
            if (_isRemoveLockMode) {
                var info = FolderLockService.Instance.GetFolderLockInfo(_targetFolderPath);
                if (info != null && FolderLockService.Instance.VerifyCredential(info, _currentPin)) {
                    ValidateCredential(_currentPin);
                }
                return;
            }

            if (_isSettingsMode) {
                success = FolderLockService.Instance.VerifySettingsUnlock(_currentPin);
            } else if (!string.IsNullOrEmpty(_targetFolderPath)) {
                success = FolderLockService.Instance.UnlockFolderSession(_targetFolderPath, _currentPin);
            }

            if (success) {
                OnUnlockSuccess();
            }
        }

        // --- Drag-to-Draw Pattern Handlers ---
        private bool _isDrawingPattern = false;
        private readonly Point[] _patternDotPositions = new Point[9];
        private readonly Ellipse[] _patternDotElements = new Ellipse[9];
        private readonly Ellipse[] _patternDotRings = new Ellipse[9];
        private Polyline? _patternPolyline;
        private Line? _patternActiveLine;

        private void InitPatternCanvas() {
            if (PatternCanvas == null) return;
            PatternCanvas.Children.Clear();

            var activeStroke = Resources["LockPrimaryBrush"] as Brush ?? _primaryBrush;
            var activeLineStroke = Resources["LockPrimaryBrush"] is SolidColorBrush sb
                ? new SolidColorBrush(Color.FromArgb(180, sb.Color.R, sb.Color.G, sb.Color.B))
                : new SolidColorBrush(Color.FromArgb(180, 234, 88, 12));

            _patternPolyline = new Polyline {
                Stroke = activeStroke,
                StrokeThickness = 3.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            PatternCanvas.Children.Add(_patternPolyline);

            _patternActiveLine = new Line {
                Stroke = activeLineStroke,
                StrokeThickness = 3,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Visibility = Visibility.Collapsed
            };
            PatternCanvas.Children.Add(_patternActiveLine);

            double w = 180;
            double h = 180;
            double stepX = w / 3.0;
            double stepY = h / 3.0;

            var ringStroke = Resources["LockPrimaryBrush"] is SolidColorBrush rsb
                ? new SolidColorBrush(Color.FromArgb(160, rsb.Color.R, rsb.Color.G, rsb.Color.B))
                : new SolidColorBrush(Color.FromArgb(160, 234, 88, 12));

            var dotFill = Resources["LockOnContainerBrush"] as Brush ?? new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));

            for (int i = 0; i < 9; i++) {
                int row = i / 3;
                int col = i % 3;
                double cx = col * stepX + stepX / 2.0;
                double cy = row * stepY + stepY / 2.0;
                _patternDotPositions[i] = new Point(cx, cy);

                var ring = new Ellipse {
                    Width = 34,
                    Height = 34,
                    Stroke = ringStroke,
                    StrokeThickness = 2,
                    Visibility = Visibility.Collapsed
                };
                Canvas.SetLeft(ring, cx - 17);
                Canvas.SetTop(ring, cy - 17);
                PatternCanvas.Children.Add(ring);
                _patternDotRings[i] = ring;

                var dot = new Ellipse {
                    Width = 14,
                    Height = 14,
                    Fill = dotFill
                };
                Canvas.SetLeft(dot, cx - 7);
                Canvas.SetTop(dot, cy - 7);
                PatternCanvas.Children.Add(dot);
                _patternDotElements[i] = dot;
            }
        }

        private void PatternCanvas_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            _isDrawingPattern = true;
            _currentPattern.Clear();
            ResetPatternDots();
            ErrorBadge.Visibility = Visibility.Collapsed;

            Point p = e.GetPosition(PatternCanvas);
            CheckHitDot(p);
            PatternCanvas.CaptureMouse();
        }

        private void PatternCanvas_MouseMove(object sender, MouseEventArgs e) {
            if (!_isDrawingPattern) return;
            Point p = e.GetPosition(PatternCanvas);
            CheckHitDot(p);

            if (_currentPattern.Count > 0 && _patternActiveLine != null) {
                int lastIndex = _currentPattern.Last() - 1;
                if (lastIndex >= 0 && lastIndex < 9) {
                    var lastDot = _patternDotPositions[lastIndex];
                    _patternActiveLine.X1 = lastDot.X;
                    _patternActiveLine.Y1 = lastDot.Y;
                    _patternActiveLine.X2 = p.X;
                    _patternActiveLine.Y2 = p.Y;
                    _patternActiveLine.Visibility = Visibility.Visible;
                }
            }
        }

        private void PatternCanvas_MouseUp(object sender, MouseButtonEventArgs e) {
            FinishDrawingPattern();
        }

        private void PatternCanvas_MouseLeave(object sender, MouseEventArgs e) {
            if (_isDrawingPattern && !PatternCanvas.IsMouseCaptured) {
                FinishDrawingPattern();
            }
        }

        private void FinishDrawingPattern() {
            if (!_isDrawingPattern) return;
            _isDrawingPattern = false;
            if (PatternCanvas.IsMouseCaptured) {
                PatternCanvas.ReleaseMouseCapture();
            }
            if (_patternActiveLine != null) {
                _patternActiveLine.Visibility = Visibility.Collapsed;
            }

            if (_currentPattern.Count >= 4) {
                string pattern1 = string.Join("-", _currentPattern);
                string pattern0 = string.Join("-", _currentPattern.Select(x => x - 1));
                ValidatePatternCredential(pattern1, pattern0);
            } else if (_currentPattern.Count > 0) {
                ShowError("Նախշը պետք է ունենա առնվազն 4 կետ");
                _currentPattern.Clear();
                ResetPatternDots();
            }
        }

        private static int GetIntermediateDot(int a, int b) {
            if (a > b) (a, b) = (b, a);
            return (a, b) switch {
                (1, 3) => 2,
                (4, 6) => 5,
                (7, 9) => 8,
                (1, 7) => 4,
                (2, 8) => 5,
                (3, 9) => 6,
                (1, 9) => 5,
                (3, 7) => 5,
                _ => 0
            };
        }

        private void SelectDot(int val) {
            if (_currentPattern.Contains(val) || val < 1 || val > 9) return;
            int idx = val - 1;
            _currentPattern.Add(val);
            HighlightDot(idx);
            _patternPolyline?.Points.Add(_patternDotPositions[idx]);
        }

        private void CheckHitDot(Point p) {
            double radiusSq = 22 * 22; // detection radius

            if (_currentPattern.Count == 0) {
                // First point - check direct hit
                for (int i = 0; i < 9; i++) {
                    int val = i + 1;
                    var dotPos = _patternDotPositions[i];
                    double dx = p.X - dotPos.X;
                    double dy = p.Y - dotPos.Y;
                    if (dx * dx + dy * dy <= radiusSq) {
                        SelectDot(val);
                        break;
                    }
                }
                return;
            }

            // User is dragging from the last selected dot to current mouse position p
            Point start = _patternDotPositions[_currentPattern.Last() - 1];
            Point end = p;

            double segDx = end.X - start.X;
            double segDy = end.Y - start.Y;
            double segLenSq = segDx * segDx + segDy * segDy;

            // Find all unselected dots crossed by the line segment [start, end]
            var crossedDots = new List<(int val, double t)>();

            for (int i = 0; i < 9; i++) {
                int val = i + 1;
                if (_currentPattern.Contains(val)) continue;

                var dotPos = _patternDotPositions[i];
                double distSq;
                double t;

                if (segLenSq < 0.001) {
                    double ddx = p.X - dotPos.X;
                    double ddy = p.Y - dotPos.Y;
                    distSq = ddx * ddx + ddy * ddy;
                    t = 0;
                } else {
                    t = ((dotPos.X - start.X) * segDx + (dotPos.Y - start.Y) * segDy) / segLenSq;
                    double tClamped = Math.Max(0, Math.Min(1, t));
                    double projX = start.X + tClamped * segDx;
                    double projY = start.Y + tClamped * segDy;
                    double ddx = dotPos.X - projX;
                    double ddy = dotPos.Y - projY;
                    distSq = ddx * ddx + ddy * ddy;
                }

                if (distSq <= radiusSq) {
                    crossedDots.Add((val, t));
                }
            }

            // Sort by t ascending so dots are selected in the order the line crosses them
            crossedDots.Sort((a, b) => a.t.CompareTo(b.t));

            foreach (var (val, _) in crossedDots) {
                if (_currentPattern.Contains(val)) continue;
                if (_currentPattern.Count > 0) {
                    int mid = GetIntermediateDot(_currentPattern.Last(), val);
                    if (mid > 0 && !_currentPattern.Contains(mid)) {
                        SelectDot(mid);
                    }
                }
                SelectDot(val);
            }
        }

        private void HighlightDot(int i) {
            if (i >= 0 && i < 9) {
                if (_patternDotElements[i] != null) {
                    _patternDotElements[i].Fill = Resources["LockPrimaryBrush"] as Brush ?? _primaryBrush;
                }
                if (_patternDotRings[i] != null) {
                    _patternDotRings[i].Visibility = Visibility.Visible;
                }
            }
        }

        private void PatternClear_Click(object sender, RoutedEventArgs e) {
            _currentPattern.Clear();
            ResetPatternDots();
            ErrorBadge.Visibility = Visibility.Collapsed;
        }

        private void ResetPatternDots() {
            if (_patternPolyline != null) {
                _patternPolyline.Points.Clear();
            }
            if (_patternActiveLine != null) {
                _patternActiveLine.Visibility = Visibility.Collapsed;
            }
            var dotFill = Resources["LockOnContainerBrush"] as Brush ?? new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
            for (int i = 0; i < 9; i++) {
                if (_patternDotElements[i] != null) {
                    _patternDotElements[i].Fill = dotFill;
                }
                if (_patternDotRings[i] != null) {
                    _patternDotRings[i].Visibility = Visibility.Collapsed;
                }
            }
        }

        // --- Password Handlers ---
        private void PasswordInputBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                ValidateCredential(PasswordInputBox.Password);
            }
        }

        private void PasswordInputBox_GotFocus(object sender, RoutedEventArgs e) {
            if (PasswordInputContainer != null) {
                PasswordInputContainer.BorderBrush = Resources["LockPrimaryBrush"] as Brush ?? _primaryBrush;
                PasswordInputContainer.BorderThickness = new Thickness(2);
            }
        }

        private void PasswordInputBox_LostFocus(object sender, RoutedEventArgs e) {
            if (PasswordInputContainer != null) {
                PasswordInputContainer.BorderBrush = Resources["LockBorderBrush"] as Brush ?? new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
                PasswordInputContainer.BorderThickness = new Thickness(1.5);
            }
        }

        private void PasswordSubmit_Click(object sender, RoutedEventArgs e) {
            ValidateCredential(PasswordInputBox.Password);
        }

        // --- Credential Validation ---
        private void ValidateCredential(string input) {
            if (_isRemoveLockMode) {
                bool removed = FolderLockService.Instance.RemoveFolderLock(_targetFolderPath, input);
                if (removed) {
                    _isRemoveLockMode = false;
                    ErrorBadge.Visibility = Visibility.Collapsed;
                    LockRemoved?.Invoke();
                    Unlocked?.Invoke();
                    return;
                } else {
                    ShowError(LocalizationService.Instance["IncorrectCredential"] ?? "Սխալ գաղտնաբառ կամ կոդ");
                    _currentPin = string.Empty;
                    UpdatePinDots();
                    _currentPattern.Clear();
                    ResetPatternDots();
                    return;
                }
            }

            bool success = false;
            if (_isSettingsMode) {
                success = FolderLockService.Instance.VerifySettingsUnlock(input);
            } else if (!string.IsNullOrEmpty(_targetFolderPath)) {
                success = FolderLockService.Instance.UnlockFolderSession(_targetFolderPath, input);
            }

            if (success) {
                OnUnlockSuccess();
            } else {
                ShowError(LocalizationService.Instance["IncorrectCredential"] ?? "Սխալ գաղտնաբառ կամ կոդ");
                _currentPin = string.Empty;
                UpdatePinDots();
                _currentPattern.Clear();
                ResetPatternDots();
            }
        }

        private void ValidatePatternCredential(string pattern1, string pattern0) {
            if (_isRemoveLockMode) {
                bool removed = FolderLockService.Instance.RemoveFolderLock(_targetFolderPath, pattern1) ||
                               FolderLockService.Instance.RemoveFolderLock(_targetFolderPath, pattern0);
                if (removed) {
                    _isRemoveLockMode = false;
                    ErrorBadge.Visibility = Visibility.Collapsed;
                    LockRemoved?.Invoke();
                    Unlocked?.Invoke();
                    return;
                } else {
                    ShowError(LocalizationService.Instance["IncorrectCredential"] ?? "Սխալ գաղտնաբառ կամ կոդ");
                    _currentPattern.Clear();
                    ResetPatternDots();
                    return;
                }
            }

            bool success = false;
            if (_isSettingsMode) {
                success = FolderLockService.Instance.VerifySettingsUnlock(pattern1) ||
                          FolderLockService.Instance.VerifySettingsUnlock(pattern0);
            } else if (!string.IsNullOrEmpty(_targetFolderPath)) {
                success = FolderLockService.Instance.UnlockFolderSession(_targetFolderPath, pattern1) ||
                          FolderLockService.Instance.UnlockFolderSession(_targetFolderPath, pattern0);
            }

            if (success) {
                OnUnlockSuccess();
            } else {
                ShowError(LocalizationService.Instance["IncorrectCredential"] ?? "Սխալ գաղտնաբառ կամ կոդ");
                _currentPattern.Clear();
                ResetPatternDots();
            }
        }

        private void OnUnlockSuccess() {
            ErrorBadge.Visibility = Visibility.Collapsed;
            Unlocked?.Invoke();
        }

        private void ShowError(string msg) {
            ErrorText.Text = msg;
            ErrorBadge.Visibility = Visibility.Visible;
        }

        public void FocusInput() {
            Dispatcher.BeginInvoke(new Action(() => {
                if (PinContainer.Visibility == Visibility.Visible) {
                    Focus();
                    Keyboard.Focus(this);
                } else if (PasswordContainer.Visibility == Visibility.Visible) {
                    PasswordInputBox.Focus();
                    Keyboard.Focus(PasswordInputBox);
                } else {
                    Focus();
                }
            }));
        }

        private void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (Visibility == Visibility.Visible) {
                FocusInput();
            }
        }

        private Button? GetButtonForSymbol(string symbol) {
            return symbol switch {
                "0" => PinBtn0,
                "1" => PinBtn1,
                "2" => PinBtn2,
                "3" => PinBtn3,
                "4" => PinBtn4,
                "5" => PinBtn5,
                "6" => PinBtn6,
                "7" => PinBtn7,
                "8" => PinBtn8,
                "9" => PinBtn9,
                "⌫" => PinBtnBack,
                "➔" => PinSubmitBtn,
                _ => null
            };
        }

        public void AnimateDigitButton(string symbol) {
            var btn = GetButtonForSymbol(symbol);
            if (btn == null) return;
            try {
                if (btn.Template?.FindName("BtnScale", btn) is ScaleTransform scale) {
                    var anim = new DoubleAnimation(0.90, 1.0, TimeSpan.FromMilliseconds(160)) {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                }
            } catch { }
        }

        public bool HandleTextInput(string text) {
            if (Visibility != Visibility.Visible || PinContainer.Visibility != Visibility.Visible) return false;
            bool handled = false;
            foreach (char c in text) {
                if (char.IsDigit(c)) {
                    if (_currentPin.Length < 32) {
                        _currentPin += c;
                        AnimateDigitButton(c.ToString());
                        UpdatePinDots();
                        CheckPinComplete();
                        handled = true;
                    }
                }
            }
            return handled;
        }

        public bool HandlePreviewKeyDown(KeyEventArgs e) {
            if (Visibility != Visibility.Visible) return false;

            if (PinContainer.Visibility == Visibility.Visible) {
                Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;
                string? digit = null;

                if (key >= Key.D0 && key <= Key.D9) {
                    digit = ((int)(key - Key.D0)).ToString();
                } else if (key >= Key.NumPad0 && key <= Key.NumPad9) {
                    digit = ((int)(key - Key.NumPad0)).ToString();
                }

                if (digit != null) {
                    if (_currentPin.Length < 32) {
                        _currentPin += digit;
                        AnimateDigitButton(digit);
                        UpdatePinDots();
                        CheckPinComplete();
                    }
                    e.Handled = true;
                    return true;
                }

                if (key == Key.Back) {
                    PinBackspace_Click(this, new RoutedEventArgs());
                    AnimateDigitButton("⌫");
                    e.Handled = true;
                    return true;
                }

                if (key == Key.Enter || key == Key.Return) {
                    PinSubmit_Click(this, new RoutedEventArgs());
                    AnimateDigitButton("➔");
                    e.Handled = true;
                    return true;
                }

                if (key == Key.Escape) {
                    if (_currentPin.Length > 0) {
                        PinClear_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                        return true;
                    }
                    return false;
                }

                if (key == Key.Delete) {
                    PinClear_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    return true;
                }
            } else if (PasswordContainer.Visibility == Visibility.Visible) {
                if (e.Key == Key.Enter || e.Key == Key.Return) {
                    PasswordSubmit_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    return true;
                }
                if (!PasswordInputBox.IsFocused) {
                    PasswordInputBox.Focus();
                }
            }

            return false;
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e) {
            HandlePreviewKeyDown(e);
        }

        private void UserControl_KeyDown(object sender, KeyEventArgs e) {
            HandlePreviewKeyDown(e);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Panak.Services;

namespace Panak.Views {
    public partial class FolderLockSetupWindow : Window {
        private readonly string _folderPath;
        private readonly List<int> _patternDots = new();
        private FolderLockInfo? _currentInfo;

        public FolderLockSetupWindow(string folderPath) {
            InitializeComponent();
            ApplyDynamicColors();
            _folderPath = folderPath;
            Loaded += FolderLockSetupWindow_Loaded;
        }

        private void ApplyDynamicColors() {
            try {
                var s = SettingsService.Instance.Current;
                string themeHex = s?.ThemeColor ?? ShellHelper.GetDefaultPastelColorHex();
                double hue = ShellHelper.GetHueFromHex(themeHex);
                string mode = s?.ThemeMode ?? "Auto";
                bool isDark = mode == "Dark" || (mode == "Auto" && ShellHelper.IsWindowsDarkTheme());

                System.Windows.Media.Color toWpf(System.Drawing.Color c) =>
                    System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);

                System.Windows.Media.Color wpfSurface;
                System.Windows.Media.Color wpfContainer;
                System.Windows.Media.Color wpfCard;
                System.Windows.Media.Color wpfPrimary;
                System.Windows.Media.Color wpfOnPrimary;
                System.Windows.Media.Color wpfOnSurface;
                System.Windows.Media.Color wpfOnSurfaceVariant;
                System.Windows.Media.Color wpfTrackInactive;

                if (isDark) {
                    wpfSurface = toWpf(ShellHelper.ColorFromHsl(hue, 0.16, 0.11));
                    wpfContainer = toWpf(ShellHelper.ColorFromHsl(hue, 0.20, 0.18));
                    wpfCard = toWpf(ShellHelper.ColorFromHsl(hue, 0.18, 0.14));
                    wpfPrimary = toWpf(ShellHelper.ColorFromHsl(hue, 0.70, 0.72));
                    wpfOnPrimary = System.Windows.Media.Color.FromRgb(0x1C, 0x1B, 0x1F);
                    wpfOnSurface = System.Windows.Media.Color.FromRgb(0xE6, 0xE1, 0xE5);
                    wpfOnSurfaceVariant = System.Windows.Media.Color.FromRgb(0xCA, 0xC4, 0xD0);
                    wpfTrackInactive = toWpf(ShellHelper.ColorFromHsl(hue, 0.15, 0.26));
                } else {
                    var (surfaceColor, containerColor, primaryColor) = ShellHelper.GetMaterialYouTonalPalette(hue);
                    var cardColor = ShellHelper.ColorFromHsl(hue, 0.60, 0.94);
                    var onSurfaceColor = ShellHelper.ColorFromHsl(hue, 0.40, 0.16);
                    var onSurfaceVariantColor = ShellHelper.ColorFromHsl(hue, 0.28, 0.42);
                    var trackInactiveColor = ShellHelper.ColorFromHsl(hue, 0.45, 0.84);

                    wpfSurface = toWpf(surfaceColor);
                    wpfContainer = toWpf(containerColor);
                    wpfCard = toWpf(cardColor);
                    wpfPrimary = toWpf(primaryColor);
                    wpfOnPrimary = System.Windows.Media.Color.FromRgb(0xFE, 0xF4, 0xEB);
                    wpfOnSurface = toWpf(onSurfaceColor);
                    wpfOnSurfaceVariant = toWpf(onSurfaceVariantColor);
                    wpfTrackInactive = toWpf(trackInactiveColor);
                }

                void SetBrush(string key, System.Windows.Media.Color col) {
                    var brush = new System.Windows.Media.SolidColorBrush(col);
                    brush.Freeze();
                    Resources[key] = brush;
                }

                SetBrush("M3SurfaceBrush", wpfSurface);
                SetBrush("M3SurfaceContainerBrush", wpfContainer);
                SetBrush("M3CardBrush", wpfCard);
                SetBrush("M3PrimaryBrush", wpfPrimary);
                SetBrush("M3OnPrimaryBrush", wpfOnPrimary);
                SetBrush("M3OnSurfaceBrush", wpfOnSurface);
                SetBrush("M3OnSurfaceVariantBrush", wpfOnSurfaceVariant);
                SetBrush("M3TrackInactiveBrush", wpfTrackInactive);
            } catch { }
        }

        private void FolderLockSetupWindow_Loaded(object sender, RoutedEventArgs e) {
            try {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                Rect workArea = ShellHelper.GetMonitorWorkArea(hwnd != IntPtr.Zero ? hwnd : (IntPtr)0);
                if (!workArea.IsEmpty && workArea.Width > 0) {
                    double w = ActualWidth > 0 ? ActualWidth : Width;
                    double h = ActualHeight > 0 ? ActualHeight : Height;
                    if (Left + w > workArea.Right - 8) Left = workArea.Right - w - 8;
                    if (Left < workArea.Left + 8) Left = workArea.Left + 8;
                    if (Top + h > workArea.Bottom - 8) Top = workArea.Bottom - h - 8;
                    if (Top < workArea.Top + 8) Top = workArea.Top + 8;
                }
            } catch { }

            if (_folderPath == "__SETTINGS__") {
                FolderSubtitleText.Text = "Կայանքների կողպում";
                if (FolderLockService.Instance.IsSettingsLocked) {
                    CurrentLockBanner.Visibility = Visibility.Visible;
                    string typeArm = FolderLockService.Instance.SettingsLockType switch {
                        "PIN" => "PIN կոդ",
                        "Pattern" => "Նախշ",
                        _ => "Գաղտնաբառ"
                    };
                    CurrentLockDetailsText.Text = $"Կողպման եղանակ՝ {typeArm}";
                    switch (FolderLockService.Instance.SettingsLockType) {
                        case "PIN": MethodPinRadio.IsChecked = true; break;
                        case "Pattern": MethodPatternRadio.IsChecked = true; break;
                        default: MethodPasswordRadio.IsChecked = true; break;
                    }
                } else {
                    CurrentLockBanner.Visibility = Visibility.Collapsed;
                    MethodPasswordRadio.IsChecked = true;
                }
                return;
            }

            string folderName = System.IO.Path.GetFileName(_folderPath);
            if (string.IsNullOrEmpty(folderName)) {
                folderName = _folderPath;
            }
            FolderSubtitleText.Text = folderName;

            _currentInfo = FolderLockService.Instance.GetFolderLockInfo(_folderPath);
            if (_currentInfo != null && _currentInfo.IsLocked) {
                CurrentLockBanner.Visibility = Visibility.Visible;
                string typeArm = _currentInfo.LockType switch {
                    "PIN" => "PIN կոդ",
                    "Pattern" => "Նախշ",
                    _ => "Գաղտնաբառ"
                };
                CurrentLockDetailsText.Text = $"Կողպման եղանակ՝ {typeArm}";

                switch (_currentInfo.LockType) {
                    case "PIN":
                        MethodPinRadio.IsChecked = true;
                        break;
                    case "Pattern":
                        MethodPatternRadio.IsChecked = true;
                        break;
                    default:
                        MethodPasswordRadio.IsChecked = true;
                        break;
                }
            } else {
                CurrentLockBanner.Visibility = Visibility.Collapsed;
                MethodPasswordRadio.IsChecked = true;
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                DragMove();
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }

        private void MethodRadio_Checked(object sender, RoutedEventArgs e) {
            if (PanelPassword == null || PanelPin == null || PanelPattern == null) {
                return;
            }

            double oldHeight = ActualHeight;

            PanelPassword.Visibility = Visibility.Collapsed;
            PanelPin.Visibility = Visibility.Collapsed;
            PanelPattern.Visibility = Visibility.Collapsed;
            HideError();

            if (MethodPasswordRadio.IsChecked == true) {
                PanelPassword.Visibility = Visibility.Visible;
            } else if (MethodPinRadio.IsChecked == true) {
                PanelPin.Visibility = Visibility.Visible;
            } else if (MethodPatternRadio.IsChecked == true) {
                PanelPattern.Visibility = Visibility.Visible;
                InitSetupPatternCanvas();
                ResetSetupPatternVisuals();
            }

            if (IsLoaded && oldHeight > 0) {
                UpdateLayout();
                Measure(new Size(ActualWidth > 0 ? ActualWidth : Width, double.PositiveInfinity));
                double targetHeight = DesiredSize.Height;
                if (targetHeight > 0 && Math.Abs(targetHeight - oldHeight) > 3) {
                    var anim = new System.Windows.Media.Animation.DoubleAnimation(oldHeight, targetHeight, new Duration(TimeSpan.FromMilliseconds(220))) {
                        EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                    };
                    this.BeginAnimation(HeightProperty, anim);
                }
            }
        }

        // --- Drag-to-Draw Pattern Setup Handlers ---
        private bool _isSetupDrawing = false;
        private readonly Point[] _setupPatternDotPositions = new Point[9];
        private readonly Ellipse[] _setupPatternDotElements = new Ellipse[9];
        private readonly Ellipse[] _setupPatternDotRings = new Ellipse[9];
        private System.Windows.Shapes.Polyline? _setupPolyline;
        private System.Windows.Shapes.Line? _setupActiveLine;

        private void InitSetupPatternCanvas() {
            if (SetupPatternCanvas == null) return;
            SetupPatternCanvas.Children.Clear();

            _setupPolyline = new System.Windows.Shapes.Polyline {
                Stroke = (Brush)FindResource("M3PrimaryBrush"),
                StrokeThickness = 3.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            SetupPatternCanvas.Children.Add(_setupPolyline);

            _setupActiveLine = new System.Windows.Shapes.Line {
                Stroke = (Brush)FindResource("M3PrimaryBrush"),
                Opacity = 0.6,
                StrokeThickness = 3,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Visibility = Visibility.Collapsed
            };
            SetupPatternCanvas.Children.Add(_setupActiveLine);

            double w = 180;
            double h = 180;
            double stepX = w / 3.0;
            double stepY = h / 3.0;

            for (int i = 0; i < 9; i++) {
                int row = i / 3;
                int col = i % 3;
                double cx = col * stepX + stepX / 2.0;
                double cy = row * stepY + stepY / 2.0;
                _setupPatternDotPositions[i] = new Point(cx, cy);

                var ring = new Ellipse {
                    Width = 34,
                    Height = 34,
                    Stroke = (Brush)FindResource("M3PrimaryBrush"),
                    StrokeThickness = 2,
                    Visibility = Visibility.Collapsed
                };
                Canvas.SetLeft(ring, cx - 17);
                Canvas.SetTop(ring, cy - 17);
                SetupPatternCanvas.Children.Add(ring);
                _setupPatternDotRings[i] = ring;

                var dot = new Ellipse {
                    Width = 14,
                    Height = 14,
                    Fill = (Brush)FindResource("M3SurfaceContainerBrush")
                };
                Canvas.SetLeft(dot, cx - 7);
                Canvas.SetTop(dot, cy - 7);
                SetupPatternCanvas.Children.Add(dot);
                _setupPatternDotElements[i] = dot;
            }
        }

        private void SetupPatternCanvas_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            _isSetupDrawing = true;
            _patternDots.Clear();
            ResetSetupPatternVisuals();
            HideError();

            Point p = e.GetPosition(SetupPatternCanvas);
            CheckSetupHitDot(p);
            SetupPatternCanvas.CaptureMouse();
        }

        private void SetupPatternCanvas_MouseMove(object sender, MouseEventArgs e) {
            if (!_isSetupDrawing) return;
            Point p = e.GetPosition(SetupPatternCanvas);
            CheckSetupHitDot(p);

            if (_patternDots.Count > 0 && _setupActiveLine != null) {
                var lastDot = _setupPatternDotPositions[_patternDots.Last() - 1];
                _setupActiveLine.X1 = lastDot.X;
                _setupActiveLine.Y1 = lastDot.Y;
                _setupActiveLine.X2 = p.X;
                _setupActiveLine.Y2 = p.Y;
                _setupActiveLine.Visibility = Visibility.Visible;
            }
        }

        private void SetupPatternCanvas_MouseUp(object sender, MouseButtonEventArgs e) {
            FinishSetupDrawing();
        }

        private void SetupPatternCanvas_MouseLeave(object sender, MouseEventArgs e) {
            if (_isSetupDrawing && !SetupPatternCanvas.IsMouseCaptured) {
                FinishSetupDrawing();
            }
        }

        private void FinishSetupDrawing() {
            if (!_isSetupDrawing) return;
            _isSetupDrawing = false;
            if (SetupPatternCanvas.IsMouseCaptured) {
                SetupPatternCanvas.ReleaseMouseCapture();
            }
            if (_setupActiveLine != null) {
                _setupActiveLine.Visibility = Visibility.Collapsed;
            }

            UpdatePatternDisplay();
            if (_patternDots.Count > 0 && _patternDots.Count < 4) {
                ShowError("Նախշը պետք է ունենա առնվազն 4 կետ");
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

        private void SelectSetupDot(int val) {
            if (_patternDots.Contains(val) || val < 1 || val > 9) return;
            int idx = val - 1;
            _patternDots.Add(val);
            HighlightSetupDot(idx);
            _setupPolyline?.Points.Add(_setupPatternDotPositions[idx]);
            UpdatePatternDisplay();
        }

        private void CheckSetupHitDot(Point p) {
            double radiusSq = 22 * 22;

            if (_patternDots.Count == 0) {
                for (int i = 0; i < 9; i++) {
                    int val = i + 1;
                    var dotPos = _setupPatternDotPositions[i];
                    double dx = p.X - dotPos.X;
                    double dy = p.Y - dotPos.Y;
                    if (dx * dx + dy * dy <= radiusSq) {
                        SelectSetupDot(val);
                        break;
                    }
                }
                return;
            }

            Point start = _setupPatternDotPositions[_patternDots.Last() - 1];
            Point end = p;

            double segDx = end.X - start.X;
            double segDy = end.Y - start.Y;
            double segLenSq = segDx * segDx + segDy * segDy;

            var crossedDots = new List<(int val, double t)>();

            for (int i = 0; i < 9; i++) {
                int val = i + 1;
                if (_patternDots.Contains(val)) continue;

                var dotPos = _setupPatternDotPositions[i];
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

            crossedDots.Sort((a, b) => a.t.CompareTo(b.t));

            foreach (var (val, _) in crossedDots) {
                if (_patternDots.Contains(val)) continue;
                if (_patternDots.Count > 0) {
                    int mid = GetIntermediateDot(_patternDots.Last(), val);
                    if (mid > 0 && !_patternDots.Contains(mid)) {
                        SelectSetupDot(mid);
                    }
                }
                SelectSetupDot(val);
            }
        }

        private void HighlightSetupDot(int i) {
            if (i >= 0 && i < 9) {
                if (_setupPatternDotElements[i] != null) {
                    _setupPatternDotElements[i].Fill = (Brush)FindResource("M3PrimaryBrush");
                }
                if (_setupPatternDotRings[i] != null) {
                    _setupPatternDotRings[i].Visibility = Visibility.Visible;
                }
            }
        }

        private void SetupPatternClear_Click(object sender, RoutedEventArgs e) {
            _patternDots.Clear();
            ResetSetupPatternVisuals();
            UpdatePatternDisplay();
            HideError();
        }

        private void ResetSetupPatternVisuals() {
            if (_setupPolyline != null) {
                _setupPolyline.Points.Clear();
            }
            if (_setupActiveLine != null) {
                _setupActiveLine.Visibility = Visibility.Collapsed;
            }
            for (int i = 0; i < 9; i++) {
                if (_setupPatternDotElements[i] != null) {
                    _setupPatternDotElements[i].Fill = (Brush)FindResource("M3SurfaceContainerBrush");
                }
                if (_setupPatternDotRings[i] != null) {
                    _setupPatternDotRings[i].Visibility = Visibility.Collapsed;
                }
            }
        }

        private void UpdatePatternDisplay() {
            if (_patternDots.Count == 0) {
                SetupPatternSequenceText.Text = "Ընտրված կետեր՝ (չկա)";
            } else {
                SetupPatternSequenceText.Text = "Ընտրված կետեր՝ " + string.Join(" ➔ ", _patternDots);
            }
        }


        // --- Remove Lock ---
        private void RemoveLockBtn_Click(object sender, RoutedEventArgs e) {
            if (_folderPath == "__SETTINGS__") {
                if (!FolderLockService.Instance.IsSettingsLocked) {
                    DialogResult = true;
                    Close();
                    return;
                }
                bool removed = false;
                if (FolderLockService.Instance.SettingsLockType == "Password" && !string.IsNullOrEmpty(InputPassword.Password)) {
                    removed = FolderLockService.Instance.RemoveSettingsLock(InputPassword.Password);
                } else if (FolderLockService.Instance.SettingsLockType == "PIN" && !string.IsNullOrEmpty(InputPin.Password)) {
                    removed = FolderLockService.Instance.RemoveSettingsLock(InputPin.Password);
                } else if (FolderLockService.Instance.SettingsLockType == "Pattern" && _patternDots.Count >= 4) {
                    removed = FolderLockService.Instance.RemoveSettingsLock(string.Join("-", _patternDots));
                } else {
                    removed = FolderLockService.Instance.RemoveSettingsLock("");
                }
                if (removed) {
                    DialogResult = true;
                    Close();
                } else {
                    ShowError("Կողպումը հեռացնելու համար անհրաժեշտ է մուտքագրել ընթացիկ գաղտնաբառը/կոդը:");
                }
                return;
            }

            if (_currentInfo != null && _currentInfo.IsLocked) {
                // If folder is currently unlocked in session, allow remove directly
                if (!FolderLockService.Instance.IsFolderLocked(_folderPath)) {
                    FolderLockService.Instance.RemoveFolderLock(_folderPath, "");
                    CurrentLockBanner.Visibility = Visibility.Collapsed;
                    _currentInfo = null;
                    DialogResult = true;
                    Close();
                    return;
                }

                // Check verification
                bool removed = false;
                if (_currentInfo.LockType == "Password" && !string.IsNullOrEmpty(InputPassword.Password)) {
                    removed = FolderLockService.Instance.RemoveFolderLock(_folderPath, InputPassword.Password);
                } else if (_currentInfo.LockType == "PIN" && !string.IsNullOrEmpty(InputPin.Password)) {
                    removed = FolderLockService.Instance.RemoveFolderLock(_folderPath, InputPin.Password);
                } else if (_currentInfo.LockType == "Pattern" && _patternDots.Count >= 4) {
                    removed = FolderLockService.Instance.RemoveFolderLock(_folderPath, string.Join("-", _patternDots));
                } else {
                    // Direct remove if credentials are valid or session active
                    removed = FolderLockService.Instance.RemoveFolderLock(_folderPath, "");
                }

                if (removed) {
                    CurrentLockBanner.Visibility = Visibility.Collapsed;
                    _currentInfo = null;
                    DialogResult = true;
                    Close();
                } else {
                    ShowError("Կողպումը հեռացնելու համար անհրաժեշտ է մուտքագրել ընթացիկ գաղտնաբառը/կոդը:");
                }
            }
        }

        // --- Save Lock ---
        private void SaveLockBtn_Click(object sender, RoutedEventArgs e) {
            HideError();
            string lockType = "Password";
            string credential = string.Empty;

            if (MethodPasswordRadio.IsChecked == true) {
                lockType = "Password";
                string p1 = InputPassword.Password;
                string p2 = InputConfirmPassword.Password;
                if (string.IsNullOrEmpty(p1)) {
                    ShowError("Խնդրում ենք մուտքագրել գաղտնաբառ:");
                    return;
                }
                if (p1 != p2) {
                    ShowError("Գաղտնաբառերը չեն համընկնում:");
                    return;
                }
                credential = p1;
            } else if (MethodPinRadio.IsChecked == true) {
                lockType = "PIN";
                string pin1 = InputPin.Password;
                string pin2 = InputConfirmPin.Password;
                if (string.IsNullOrEmpty(pin1)) {
                    ShowError("Խնդրում ենք մուտքագրել PIN կոդ:");
                    return;
                }
                if (!pin1.All(char.IsDigit)) {
                    ShowError("PIN կոդը պետք է բաղկացած լինի միայն թվանշաններից:");
                    return;
                }
                if (pin1 != pin2) {
                    ShowError("PIN կոդերը չեն համընկնում:");
                    return;
                }
                credential = pin1;
            } else if (MethodPatternRadio.IsChecked == true) {
                lockType = "Pattern";
                if (_patternDots.Count < 4) {
                    ShowError("Նախշը պետք է պարունակի առնվազն 4 կետ:");
                    return;
                }
                credential = string.Join("-", _patternDots);
            }

            if (_folderPath == "__SETTINGS__") {
                FolderLockService.Instance.SetSettingsLock(lockType, credential, null);
            } else {
                FolderLockService.Instance.SetFolderLock(_folderPath, lockType, credential, null);
            }
            DialogResult = true;
            Close();
        }

        private void ShowError(string msg) {
            DialogErrorText.Text = msg;
            DialogErrorBadge.Visibility = Visibility.Visible;
        }

        private void HideError() {
            DialogErrorBadge.Visibility = Visibility.Collapsed;
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Panak.Models;
using Panak.Services;

namespace Panak.Views {
    public partial class WelcomeWindow : Window {
        private int _currentStep = 0;
        private bool _isInitializing = true;

        private string _selectedThemeMode = "Auto";
        private bool? _lastAppliedWindowOsDark;
        private string _selectedShape = "Squircle";
        private string _currentDisplayedShape = "Squircle";
        private string _selectedColor = ShellHelper.DefaultPastelColorHex;
        private double _folderOpacity = 0.85;
        private bool _folderBlurEnabled = true;
        private double _folderBlurRadius = 28.0;

        // --- Smooth Shape Morphing Engine ---
        private const int MORPH_POINT_COUNT = 64;
        private static readonly System.Collections.Generic.Dictionary<string, System.Windows.Point[]> _cachedShapePoints = new(StringComparer.OrdinalIgnoreCase);
        private DispatcherTimer? _shapeMorphTimer;
        private DateTime _shapeMorphStartTime;
        private System.Windows.Point[]? _morphStartPoints;
        private System.Windows.Point[]? _morphTargetPoints;

        private string _pagingMode = "Slider";
        private int _gridColumns = 3;
        private int _gridRows = 3;
        private bool _sliderArrows = true;
        private bool _sliderDrag = true;
        private bool _focusMode = true;
        private double _focusDimming = 0.45;
        private bool _focusBlurEnabled = true;
        private double _focusBlurRadius = 28.0;

        private DispatcherTimer? _liveWallpaperTimer;
        private bool _isPopup1Open = true;
        private bool _isPopup2Open = false;

        // Desktop Preview Zoom & Icon Dragging
        private double _welcomeDesktopZoomScale = 1.0;
        private Point _welcomeIcon1Pos = new Point(-1, -1);
        private Point _welcomeIcon2Pos = new Point(-1, -1);
        private bool _isDraggingWelcomeIcon1 = false;
        private bool _hasDraggedWelcomeIcon1 = false;
        private Point _dragWelcomeIcon1StartMouse;
        private Point _dragWelcomeIcon1StartPos;
        private bool _isDraggingWelcomeIcon2 = false;
        private bool _hasDraggedWelcomeIcon2 = false;
        private Point _dragWelcomeIcon2StartMouse;
        private Point _dragWelcomeIcon2StartPos;

        private int _currentPreviewPage = 0;
        private int _totalPreviewPages = 2;
        private double _previewPageWidth = 234.0;
        private bool _isDraggingSlider = false;
        private Point _dragStartPos;

        private static BitmapSource? _cachedFolderIcon;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BitmapSource?> _cachedMockIcons = new(StringComparer.OrdinalIgnoreCase);

        // Master item pool for Folder 1 (Panak): 13 realistic items (9 on Page 1, 4 on Page 2)
        private static readonly (string Name, bool IsFolder, string Ext)[] MasterFolder1Pool = new[] {
            ("data", true, ""),
            ("dist", true, ""),
            ("node_modules", true, ""),
            ("public", true, ""),
            ("src", true, ""),
            ("stitch_modern", true, ""),
            ("test", true, ""),
            ("views", true, ""),
            (".env", false, ".env"),
            ("package.json", false, ".json"),
            ("tsconfig.json", false, ".json"),
            ("README.md", false, ".md"),
            ("index.ts", false, ".ts")
        };

        // Master item pool for Folder 2 (Projects): 13 realistic items (9 on Page 1, 4 on Page 2)
        private static readonly (string Name, bool IsFolder, string Ext)[] MasterFolder2Pool = new[] {
            ("Components", true, ""),
            ("Designs", true, ""),
            ("Prototypes", true, ""),
            ("Screenshots", true, ""),
            ("Assets", true, ""),
            ("Specs", true, ""),
            ("Documentation", true, ""),
            ("Releases", true, ""),
            ("BrandGuidelines.pdf", false, ".pdf"),
            ("PitchDeck.pptx", false, ".pptx"),
            ("Roadmap.docx", false, ".docx"),
            ("Changelog.md", false, ".md"),
            ("Budget2026.xlsx", false, ".xlsx")
        };

        public WelcomeWindow() {
            InitializeComponent();
            LoadLogo();
            LoadInitialSettings();
            ApplyLocalization();
            StartWallpaperCapture();
            UpdateLivePreview(updateAllSteps: true);
            RebuildPreviewItems();
            UpdateStepDots();
            _isInitializing = false;

            Loaded += (s, e) => {
                bool isDark = _selectedThemeMode == "Dark" || (_selectedThemeMode == "Auto" && ShellHelper.IsWindowsDarkTheme());
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero) {
                    ShellHelper.OptimizeWindowForOs(hwnd, isDark, roundCorners: false);
                }
                double currentHue = ShellHelper.GetHueFromHex(_selectedColor);
                ApplyMaterialYouPalette(currentHue);
                int tIdx = _selectedThemeMode == "Light" ? 0 : (_selectedThemeMode == "Dark" ? 1 : 2);
                AnimateThemePill(tIdx, immediate: true);
                int sIdx = GetShapeIndex(_selectedShape);
                AnimateStep1ShapePill(sIdx, immediate: true);
                UpdateLivePreview(updateAllSteps: true);
                UpdateStepDots();
                UpdateWallpaperCrop();
            };
            LocationChanged += (s, e) => UpdateWallpaperCrop();
        }

        private void LoadLogo() {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            BitmapImage? iconImage = null;
            try {
                iconImage = new BitmapImage(new Uri("pack://application:,,,/Logo.png", UriKind.Absolute));
            } catch {
                try {
                    iconImage = new BitmapImage(new Uri("pack://application:,,,/Frame 121.png", UriKind.Absolute));
                } catch {
                    string[] iconCandidates = {
                        Path.Combine(baseDir, "src", "Logo.png"),
                        Path.Combine(baseDir, "Logo.png"),
                        Path.Combine(baseDir, "src", "Frame 121.png"),
                        Path.Combine(baseDir, "Frame 121.png"),
                        Path.Combine(baseDir, "PanakSetupLogo.png"),
                        Path.Combine(baseDir, "src", "PanakSetupLogo.png"),
                        Path.Combine(baseDir, "PanakLogo.png"),
                        Path.Combine(baseDir, "Panak.ico")
                    };
                    foreach (var path in iconCandidates) {
                        if (File.Exists(path)) {
                            iconImage = new BitmapImage(new Uri(path, UriKind.Absolute));
                            break;
                        }
                    }
                }
            }

            if (iconImage != null) {
                Icon = iconImage;
            }
        }

        private void LoadInitialSettings() {
            var s = SettingsService.Instance.Current;
            _selectedThemeMode = string.IsNullOrEmpty(s.ThemeMode) ? "Auto" : s.ThemeMode;
            _selectedShape = string.IsNullOrEmpty(s.IconShape) ? "Squircle" : s.IconShape;
            _currentDisplayedShape = _selectedShape;
            _selectedColor = string.IsNullOrEmpty(s.ThemeColor) ? ShellHelper.GetDefaultPastelColorHex() : s.ThemeColor;
            _folderOpacity = s.FolderOpacity;
            _folderBlurEnabled = s.FolderBlurEnabled;
            _folderBlurRadius = s.FolderBlurRadius;

            _pagingMode = string.IsNullOrEmpty(s.PagingMode) ? "Slider" : s.PagingMode;
            _gridColumns = s.GridColumns > 0 ? s.GridColumns : 5;
            _gridRows = s.GridRows > 0 ? s.GridRows : 3;
            _sliderArrows = s.SliderArrowsEnabled;
            _sliderDrag = s.SliderDragEnabled;
            _focusMode = s.FocusModeEnabled;
            _focusDimming = s.FocusDimmingOpacity;
            _focusBlurEnabled = s.FocusBlurEnabled;
            _focusBlurRadius = s.FocusBlurRadius > 0 ? s.FocusBlurRadius : 28.0;

            // Step 1 Theme Mode controls
            if (string.Equals(_selectedThemeMode, "Light", StringComparison.OrdinalIgnoreCase)) {
                if (Step1ThemeLight != null) Step1ThemeLight.IsChecked = true;
            } else if (string.Equals(_selectedThemeMode, "Dark", StringComparison.OrdinalIgnoreCase)) {
                if (Step1ThemeDark != null) Step1ThemeDark.IsChecked = true;
            } else {
                if (Step1ThemeAuto != null) Step1ThemeAuto.IsChecked = true;
            }
            int initialThemeIdx = string.Equals(_selectedThemeMode, "Light", StringComparison.OrdinalIgnoreCase) ? 0 :
                                  (string.Equals(_selectedThemeMode, "Dark", StringComparison.OrdinalIgnoreCase) ? 1 : 2);
            AnimateThemePill(initialThemeIdx, immediate: true);

            // Step 1 controls
            if (string.Equals(_selectedShape, "Circle", StringComparison.OrdinalIgnoreCase)) ShapeCircleRadio.IsChecked = true;
            else if (string.Equals(_selectedShape, "Clover", StringComparison.OrdinalIgnoreCase)) ShapeCloverRadio.IsChecked = true;
            else if (string.Equals(_selectedShape, "Scallop", StringComparison.OrdinalIgnoreCase)) ShapeScallopRadio.IsChecked = true;
            else if (string.Equals(_selectedShape, "Arch", StringComparison.OrdinalIgnoreCase)) ShapeArchRadio.IsChecked = true;
            else ShapeSquircleRadio.IsChecked = true;

            int initialShapeIdx = GetShapeIndex(_selectedShape);
            AnimateStep1ShapePill(initialShapeIdx, immediate: true);

            double hue = ShellHelper.GetHueFromHex(_selectedColor);
            Step1HueSlider.Value = hue;
            Step1OpacitySlider.Value = Math.Round(_folderOpacity * 100);
            Step1OpacityValueText.Text = $"{(int)Step1OpacitySlider.Value}%";
            if (Step1BlurCheckBox != null) Step1BlurCheckBox.IsChecked = _folderBlurEnabled;
            Step1BlurRadiusSlider.Value = _folderBlurRadius;
            Step1BlurRadiusSlider.IsEnabled = _folderBlurEnabled;
            Step1RadiusValueText.Text = $"{(int)_folderBlurRadius} px";
            RebuildStep1CustomSwatchesUI();

            if (Step1SwatchesPanel != null) {
                bool foundSwatch = false;
                foreach (var child in Step1SwatchesPanel.Children) {
                    if (child is RadioButton rb && rb.Tag is string tag) {
                        bool isMatch = string.Equals(tag, _selectedColor, StringComparison.OrdinalIgnoreCase);
                        rb.IsChecked = isMatch;
                        if (isMatch) foundSwatch = true;
                    }
                }
                if (!foundSwatch) {
                    double selHue = ShellHelper.GetHueFromHex(_selectedColor);
                    foreach (var child in Step1SwatchesPanel.Children) {
                        if (child is RadioButton rb && rb.Tag is string tag) {
                            double swHue = ShellHelper.GetHueFromHex(tag);
                            double diff = Math.Abs(swHue - selHue);
                            if (diff > 180) diff = 360 - diff;
                            if (diff < 4.0) {
                                rb.IsChecked = true;
                                break;
                            }
                        }
                    }
                }
            }


            // Step 2 controls
            UpdateStep2PagingModeCardsVisual();

            Step2ColsSlider.Value = _gridColumns;
            Step2ColsValueText.Text = _gridColumns.ToString();
            Step2RowsSlider.Value = _gridRows;
            Step2RowsValueText.Text = _gridRows.ToString();

            Step2FocusCheckBox.IsChecked = _focusMode;
            Step2DimmingSlider.Value = Math.Round(_focusDimming * 100);
            Step2DimmingValueText.Text = $"{(int)Step2DimmingSlider.Value}%";
            if (Step2FocusBlurCheckBox != null) Step2FocusBlurCheckBox.IsChecked = _focusBlurEnabled;
            if (Step2FocusBlurSlider != null) {
                Step2FocusBlurSlider.Value = _focusBlurRadius;
                Step2FocusBlurSlider.IsEnabled = _focusBlurEnabled;
            }
            if (Step2FocusBlurValueText != null) Step2FocusBlurValueText.Text = $"{(int)_focusBlurRadius} px";
            Step2ArrowsCheckBox.IsChecked = _sliderArrows;
            Step2DragCheckBox.IsChecked = _sliderDrag;

            ApplyMaterialYouPalette(hue);
        }

        private BitmapSource? _fullWallpaper = null;

        private void StartWallpaperCapture() {
            try {
                _fullWallpaper = ShellHelper.GetDesktopWallpaperBitmap();
                UpdateWallpaperCrop();
            } catch { }

            try {
                if (ShellHelper.IsLiveWallpaperEngineRunning()) {
                    _liveWallpaperTimer = new DispatcherTimer(DispatcherPriority.Background) {
                        Interval = TimeSpan.FromMilliseconds(250)
                    };
                    _liveWallpaperTimer.Tick += (s, e) => {
                        try {
                            if (ShellHelper.IsLiveWallpaperEngineRunning()) {
                                var liveFrame = ShellHelper.CaptureLiveWallpaper();
                                if (liveFrame != null) {
                                    _fullWallpaper = liveFrame;
                                    UpdateWallpaperCrop();
                                }
                            }
                        } catch { }
                    };
                    _liveWallpaperTimer.Start();
                }
            } catch { }
        }

        private void UpdateBorderCrop(Border? border, Image? wallImg, Image? blurImg1, Image? blurImg2, Grid? popupContainer1 = null, Grid? popupContainer2 = null) {
            if (_fullWallpaper == null) return;
            if (border == null || !IsLoaded || !border.IsVisible || border.ActualWidth <= 0 || border.ActualHeight <= 0) {
                if (wallImg != null && wallImg.Source != _fullWallpaper) wallImg.Source = _fullWallpaper;
                if (blurImg1 != null && blurImg1.Source != _fullWallpaper) blurImg1.Source = _fullWallpaper;
                if (blurImg2 != null && blurImg2.Source != _fullWallpaper) blurImg2.Source = _fullWallpaper;
                return;
            }

            try {
                double dpiScaleX = 1.0;
                double dpiScaleY = 1.0;
                try {
                    var dpi = VisualTreeHelper.GetDpi(this);
                    dpiScaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                    dpiScaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;
                } catch { }

                Point screenPt = border.PointToScreen(new Point(0, 0));
                double pxW = border.ActualWidth * dpiScaleX;
                double pxH = border.ActualHeight * dpiScaleY;

                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                Rect monBounds = ShellHelper.GetMonitorBounds(hwnd);

                double monW = monBounds.Width > 0 ? monBounds.Width : SystemParameters.PrimaryScreenWidth * dpiScaleX;
                double monH = monBounds.Height > 0 ? monBounds.Height : SystemParameters.PrimaryScreenHeight * dpiScaleY;

                double relX = screenPt.X - monBounds.Left;
                double relY = screenPt.Y - monBounds.Top;

                double scaleX = (double)_fullWallpaper.PixelWidth / monW;
                double scaleY = (double)_fullWallpaper.PixelHeight / monH;

                int cropX = (int)Math.Round(relX * scaleX);
                int cropY = (int)Math.Round(relY * scaleY);
                int cropW = (int)Math.Round(pxW * scaleX);
                int cropH = (int)Math.Round(pxH * scaleY);

                if (cropX < 0) { cropW += cropX; cropX = 0; }
                if (cropY < 0) { cropH += cropY; cropY = 0; }
                if (cropX + cropW > _fullWallpaper.PixelWidth) cropW = _fullWallpaper.PixelWidth - cropX;
                if (cropY + cropH > _fullWallpaper.PixelHeight) cropH = _fullWallpaper.PixelHeight - cropY;

                if (cropW > 20 && cropH > 20) {
                    var cropped = new CroppedBitmap(_fullWallpaper, new Int32Rect(cropX, cropY, cropW, cropH));
                    cropped.Freeze();
                    if (wallImg != null) wallImg.Source = cropped;

                    // Folder 1 Card Sub-crop
                    try {
                        double cardW = (Step1CardBorderInner != null && Step1CardBorderInner.Width > 20) ? Step1CardBorderInner.Width : 258.0;
                        double cardH = (Step1CardBorderInner != null && Step1CardBorderInner.Height > 20) ? Step1CardBorderInner.Height : 340.0;
                        double cardXInBorder = 12.0;
                        double cardYInBorder = Math.Max(0, (border.ActualHeight > 0 ? border.ActualHeight : 450) - 76.0 - cardH);

                        if (popupContainer1 != null && border.IsVisible && popupContainer1.IsVisible) {
                            try {
                                var pt = popupContainer1.TranslatePoint(new Point(0, 0), border);
                                cardXInBorder = pt.X;
                                cardYInBorder = pt.Y;
                                cardW *= _welcomeDesktopZoomScale;
                                cardH *= _welcomeDesktopZoomScale;
                            } catch { }
                        }

                        int subX = cropX + (int)Math.Round(cardXInBorder * scaleX);
                        int subY = cropY + (int)Math.Round(cardYInBorder * scaleY);
                        int subW = (int)Math.Round(cardW * scaleX);
                        int subH = (int)Math.Round(cardH * scaleY);

                        if (subX < 0) { subW += subX; subX = 0; }
                        if (subY < 0) { subH += subY; subY = 0; }
                        if (subX + subW > _fullWallpaper.PixelWidth) subW = _fullWallpaper.PixelWidth - subX;
                        if (subY + subH > _fullWallpaper.PixelHeight) subH = _fullWallpaper.PixelHeight - subY;

                        if (subW > 20 && subH > 20) {
                            var cardCropped = new CroppedBitmap(_fullWallpaper, new Int32Rect(subX, subY, subW, subH));
                            cardCropped.Freeze();
                            if (blurImg1 != null) blurImg1.Source = cardCropped;
                        } else {
                            if (blurImg1 != null) blurImg1.Source = cropped;
                        }
                    } catch {
                        if (blurImg1 != null) blurImg1.Source = cropped;
                    }

                    // Folder 2 Card Sub-crop
                    try {
                        double cardW2 = (Step1CardBorderInner2 != null && Step1CardBorderInner2.Width > 20) ? Step1CardBorderInner2.Width : 258.0;
                        double cardH2 = (Step1CardBorderInner2 != null && Step1CardBorderInner2.Height > 20) ? Step1CardBorderInner2.Height : 340.0;
                        double cardXInBorder2 = 12.0;
                        double cardYInBorder2 = Math.Max(0, (border.ActualHeight > 0 ? border.ActualHeight : 450) - 76.0 - cardH2);

                        if (popupContainer2 != null && border.IsVisible && popupContainer2.IsVisible) {
                            try {
                                var pt2 = popupContainer2.TranslatePoint(new Point(0, 0), border);
                                cardXInBorder2 = pt2.X;
                                cardYInBorder2 = pt2.Y;
                                cardW2 *= _welcomeDesktopZoomScale;
                                cardH2 *= _welcomeDesktopZoomScale;
                            } catch { }
                        }

                        int subX2 = cropX + (int)Math.Round(cardXInBorder2 * scaleX);
                        int subY2 = cropY + (int)Math.Round(cardYInBorder2 * scaleY);
                        int subW2 = (int)Math.Round(cardW2 * scaleX);
                        int subH2 = (int)Math.Round(cardH2 * scaleY);

                        if (subX2 < 0) { subW2 += subX2; subX2 = 0; }
                        if (subY2 < 0) { subH2 += subY2; subY2 = 0; }
                        if (subX2 + subW2 > _fullWallpaper.PixelWidth) subW2 = _fullWallpaper.PixelWidth - subX2;
                        if (subY2 + subH2 > _fullWallpaper.PixelHeight) subH2 = _fullWallpaper.PixelHeight - subY2;

                        if (subW2 > 20 && subH2 > 20) {
                            var cardCropped2 = new CroppedBitmap(_fullWallpaper, new Int32Rect(subX2, subY2, subW2, subH2));
                            cardCropped2.Freeze();
                            if (blurImg2 != null) blurImg2.Source = cardCropped2;
                        } else {
                            if (blurImg2 != null) blurImg2.Source = cropped;
                        }
                    } catch {
                        if (blurImg2 != null) blurImg2.Source = cropped;
                    }
                } else {
                    if (wallImg != null) wallImg.Source = _fullWallpaper;
                    if (blurImg1 != null) blurImg1.Source = _fullWallpaper;
                    if (blurImg2 != null) blurImg2.Source = _fullWallpaper;
                }
            } catch {
                if (wallImg != null) wallImg.Source = _fullWallpaper;
                if (blurImg1 != null) blurImg1.Source = _fullWallpaper;
                if (blurImg2 != null) blurImg2.Source = _fullWallpaper;
            }
        }

        public void UpdateWallpaperCrop() {
            try {
                if (_fullWallpaper == null) {
                    _fullWallpaper = ShellHelper.GetDesktopWallpaperBitmap();
                    if (_fullWallpaper == null) return;
                }

                UpdateBorderCrop(Step1PreviewBorder, Step1WallpaperImage, Step1BlurredWallpaperImage, Step1BlurredWallpaperImage2, Step1PopupContainer, Step1PopupContainer2);
                UpdateBorderCrop(Step2PreviewBorder, Step2WallpaperImage, Step2BlurredWallpaperImage, Step2BlurredWallpaperImage2, Step2PopupContainer, Step2PopupContainer2);
                UpdateBorderCrop(Step3PreviewBorder, Step3WallpaperImage, Step3BlurredWallpaperImage, Step3BlurredWallpaperImage2, Step3PopupContainer, Step3PopupContainer2);
            } catch { }
        }

        private void ApplyLocalization() {
            var loc = LocalizationService.Instance;

            Title = loc["WelcomeToPanak"];
            StepIndicatorText.Text = $"{loc["Step"]} {_currentStep + 1} / 3";
            Step1TitleText.Text = loc["Step1Title"] ?? "Գույն, ձեւ եւ թափանցիկություն";
            Step1SubtitleText.Text = loc["Step1Subtitle"];
            if (Step1ThemeLabel != null) Step1ThemeLabel.Text = loc["Theme"] ?? (loc.CurrentLanguage == "hy" ? "Թեմա" : "Theme");
            if (Step1ThemeDesc != null) Step1ThemeDesc.Text = loc["ThemeDesc"] ?? (loc.CurrentLanguage == "hy" ? "Բաց, մուգ կամ համակարգի թեմա" : "Light, Dark or System Theme");
            if (Step1ThemeLight != null) Step1ThemeLight.Content = loc["ThemeLight"] ?? "☀️ Light";
            if (Step1ThemeDark != null) Step1ThemeDark.Content = loc["ThemeDark"] ?? "🌙 Dark";
            if (Step1ThemeAuto != null) Step1ThemeAuto.Content = loc["ThemeAuto"] ?? "⚙️ Auto";
            Step1ShapeLabel.Text = loc["IconShape"];
            ShapeCircleRadio.ToolTip = loc["Circle"];
            ShapeSquircleRadio.ToolTip = loc["Squircle"];
            ShapeCloverRadio.ToolTip = loc["Clover"];
            ShapeScallopRadio.ToolTip = loc["Scallop"];
            ShapeArchRadio.ToolTip = loc["Arch"];
            Step1ColorLabel.Text = loc["DefaultFolderColor"];
            Step1OpacityLabel.Text = loc["GeneralFolderOpacity"];
            Step1BlurLabel.Text = loc["FolderBlur"];
            if (Step1BlurWarningBadge != null) Step1BlurWarningBadge.Text = loc["SlowsDownSystemWarning"];
            if (Step1RadiusLabel != null) Step1RadiusLabel.Text = loc["FolderBlurRadius"];
            Step1LivePreviewBadge.Text = loc["LivePreview"];
            if (Step1PreviewMoreBtn != null) Step1PreviewMoreBtn.ToolTip = loc["MoreActions"];
            UpdateStep1MoreActionsMenuTheme();

            string countFormatted = string.Format(loc["ItemsCountBadge"] ?? "{0} նիշք", 24);
            if (Step1PreviewCountText != null) Step1PreviewCountText.Text = countFormatted;
            if (Step2PreviewCountText != null) Step2PreviewCountText.Text = countFormatted;
            if (Step3PreviewCountText != null) Step3PreviewCountText.Text = countFormatted;
            if (Step1PreviewCountText2 != null) Step1PreviewCountText2.Text = countFormatted;
            if (Step2PreviewCountText2 != null) Step2PreviewCountText2.Text = countFormatted;
            if (Step3PreviewCountText2 != null) Step3PreviewCountText2.Text = countFormatted;

            Step2TitleText.Text = loc["Step2Title"] ?? "Դիրք եւ վարքագիծ";
            Step2SubtitleText.Text = loc["Step2Subtitle"];
            Step2PagingModeLabel.Text = loc["PagingMode"];
            if (Step2PagingSliderLabel != null) Step2PagingSliderLabel.Text = loc["PagingModeSlider"] ?? "Սլայդեր";
            if (Step2PagingScrollLabel != null) Step2PagingScrollLabel.Text = loc["PagingModeScrollbar"] ?? "Գլորակ";
            Step2ColsLabel.Text = loc["ItemsPerRow"];
            Step2RowsLabel.Text = loc["GridRows"];
            Step2FocusModeLabel.Text = loc["FocusMode"];
            Step2DimmingLabel.Text = loc["FocusDimming"];
            if (Step2FocusBlurLabel != null) Step2FocusBlurLabel.Text = loc["FocusBlur"];
            if (Step2FocusBlurWarningBadge != null) Step2FocusBlurWarningBadge.Text = loc["SlowsDownSystemWarning"];
            if (Step2FocusBlurRadiusLabel != null) Step2FocusBlurRadiusLabel.Text = loc["FocusBlurRadius"];
            Step2ArrowsLabel.Text = loc["SliderArrows"];
            Step2DragLabel.Text = loc["SliderDrag"];

            Step3TitleText.Text = loc["WelcomeToPanak"];
            Step3SubtitleText.Text = loc["WelcomeSubtitle"];
            Feature1Text.Text = loc["WelcomeFeature1"];
            Feature2Text.Text = loc["WelcomeFeature2"];
            Feature3Text.Text = loc["WelcomeFeature3"];
            KofiBtn.Content = loc["DonateOnKofi"];
            if (CrowdinBtn != null) CrowdinBtn.ToolTip = loc["TranslateOnCrowdin"];

            BackBtn.Content = loc["Back"];
            SkipBtn.Content = loc["Skip"];
            if (CloseWizardBtn != null) CloseWizardBtn.Content = loc["Close"] ?? (loc.CurrentLanguage == "hy" ? "Փակել" : "Close");
            UpdateStepButtons();
        }

        public void ApplyMaterialYouPalette(double hue, bool animate = false) {
            try {
                hue = (hue % 360.0 + 360.0) % 360.0;

                bool isDark = _selectedThemeMode == "Dark" || (_selectedThemeMode == "Auto" && ShellHelper.IsWindowsDarkTheme());
                if (_lastAppliedWindowOsDark != isDark) {
                    _lastAppliedWindowOsDark = isDark;
                    var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    if (hwnd != IntPtr.Zero) {
                        ShellHelper.OptimizeWindowForOs(hwnd, isDark, roundCorners: false);
                    }
                }

                static Color toWpf(System.Drawing.Color c) => Color.FromArgb(c.A, c.R, c.G, c.B);

                Color wpfSurface;
                Color wpfContainer;
                Color wpfCard;
                Color wpfPrimary;
                Color wpfOnPrimary;
                Color wpfOnSurface;
                Color wpfOnSurfaceVariant;
                Color wpfTrackInactive;
                Color wpfContainerHigh;
                Color wpfContainerHighest;
                Color wpfOutline;

                if (isDark) {
                    wpfSurface = toWpf(ShellHelper.ColorFromHsl(hue, 0.16, 0.11)); // Deep dark surface #16161D
                    wpfContainer = toWpf(ShellHelper.ColorFromHsl(hue, 0.20, 0.18));
                    wpfCard = toWpf(ShellHelper.ColorFromHsl(hue, 0.18, 0.14));
                    wpfPrimary = toWpf(ShellHelper.ColorFromHsl(hue, 0.70, 0.72));
                    wpfOnPrimary = Color.FromRgb(0x1C, 0x1B, 0x1F);
                    wpfOnSurface = Color.FromRgb(0xE6, 0xE1, 0xE5);
                    wpfOnSurfaceVariant = Color.FromRgb(0xCA, 0xC4, 0xD0);
                    wpfTrackInactive = toWpf(ShellHelper.ColorFromHsl(hue, 0.15, 0.26));
                    wpfContainerHigh = toWpf(ShellHelper.ColorFromHsl(hue, 0.20, 0.22));
                    wpfContainerHighest = toWpf(ShellHelper.ColorFromHsl(hue, 0.20, 0.26));
                    wpfOutline = toWpf(ShellHelper.ColorFromHsl(hue, 0.15, 0.38));
                } else {
                    var (surfaceColor, containerColor, primaryColor) = ShellHelper.GetMaterialYouTonalPalette(hue);
                    var cardColor = ShellHelper.ColorFromHsl(hue, 0.60, 0.94);
                    var onSurfaceColor = ShellHelper.ColorFromHsl(hue, 0.40, 0.16);
                    var onSurfaceVariantColor = ShellHelper.ColorFromHsl(hue, 0.28, 0.42);
                    var trackInactiveColor = ShellHelper.ColorFromHsl(hue, 0.45, 0.84);
                    var containerHighColor = ShellHelper.ColorFromHsl(hue, 0.50, 0.88);
                    var containerHighestColor = ShellHelper.ColorFromHsl(hue, 0.45, 0.82);
                    var outlineColor = ShellHelper.ColorFromHsl(hue, 0.25, 0.60);

                    wpfSurface = toWpf(surfaceColor);
                    wpfContainer = toWpf(containerColor);
                    wpfCard = toWpf(cardColor);
                    wpfPrimary = toWpf(primaryColor);
                    wpfOnPrimary = Colors.White;
                    wpfOnSurface = toWpf(onSurfaceColor);
                    wpfOnSurfaceVariant = toWpf(onSurfaceVariantColor);
                    wpfTrackInactive = toWpf(trackInactiveColor);
                    wpfContainerHigh = toWpf(containerHighColor);
                    wpfContainerHighest = toWpf(containerHighestColor);
                    wpfOutline = toWpf(outlineColor);
                }

                var animDuration = TimeSpan.FromMilliseconds(260);

                void SetBrush(string key, Color col) {
                    if (animate) {
                        ShellHelper.AnimateResourceBrushColor(Resources, key, col, animDuration);
                    } else {
                        ShellHelper.SetResourceBrushColor(Resources, key, col);
                    }
                }

                SetBrush("M3SurfaceBrush", wpfSurface);
                SetBrush("M3SurfaceContainerBrush", wpfContainer);
                SetBrush("M3SurfaceContainerHighBrush", wpfContainerHigh);
                SetBrush("M3SurfaceContainerHighestBrush", wpfContainerHighest);
                SetBrush("M3OutlineBrush", wpfOutline);
                SetBrush("M3CardBrush", wpfCard);
                SetBrush("M3PrimaryBrush", wpfPrimary);
                SetBrush("M3OnPrimaryBrush", wpfOnPrimary);
                SetBrush("M3OnSurfaceBrush", wpfOnSurface);
                SetBrush("M3OnSurfaceVariantBrush", wpfOnSurfaceVariant);
                SetBrush("M3TrackInactiveBrush", wpfTrackInactive);

                Color selColor = ShellHelper.ParseWpfColorFast(_selectedColor, wpfPrimary);
                SetBrush("SettingsHueThumbBrush", selColor);

                var capsuleBg = Color.FromArgb(42, selColor.R, selColor.G, selColor.B);
                SetBrush("ShapeCapsuleBgBrush", capsuleBg);

                var (cContainer, cTriangle, cCloud, cShield, cEllipse) = ShellHelper.GetLogoDynamicColors(hue, isDark);
                SetBrush("LogoContainerBrush", toWpf(cContainer));
                SetBrush("LogoTriangleBrush", toWpf(cTriangle));
                SetBrush("LogoCloudBrush", toWpf(cCloud));
                SetBrush("LogoShieldBrush", toWpf(cShield));
                SetBrush("LogoEllipseBrush", toWpf(cEllipse));
                SetBrush("LogoTextBrush", toWpf(cTriangle));

                if (Step1ColorIndicator != null) {
                    if (animate) {
                        Step1ColorIndicator.Background = ShellHelper.AnimateOrCreateBrush(Step1ColorIndicator.Background, selColor, animDuration);
                    } else {
                        Step1ColorIndicator.Background = ShellHelper.UpdateOrCreateBrush(Step1ColorIndicator.Background, selColor);
                    }
                }

                UpdateStepDots();
                UpdateAllPreviewDots();
            } catch { }
        }

        private void RebuildPreviewItems() {
            int actualCols = Math.Clamp(_gridColumns, 1, 8);
            int actualRows = Math.Clamp(_gridRows, 1, 5);
            int itemsPerPage = Math.Max(1, actualCols * actualRows);

            var f1PagesList = new System.Collections.Generic.List<(string Name, bool IsFolder, string Ext)[]>();
            for (int i = 0; i < MasterFolder1Pool.Length; i += itemsPerPage) {
                f1PagesList.Add(MasterFolder1Pool.Skip(i).Take(itemsPerPage).ToArray());
            }
            if (f1PagesList.Count == 0) f1PagesList.Add(MasterFolder1Pool);
            var folder1Pages = f1PagesList.ToArray();
            int totalFolder1Items = MasterFolder1Pool.Length;
            var f1Page0 = folder1Pages[0];

            var f2PagesList = new System.Collections.Generic.List<(string Name, bool IsFolder, string Ext)[]>();
            for (int i = 0; i < MasterFolder2Pool.Length; i += itemsPerPage) {
                f2PagesList.Add(MasterFolder2Pool.Skip(i).Take(itemsPerPage).ToArray());
            }
            if (f2PagesList.Count == 0) f2PagesList.Add(MasterFolder2Pool);
            var folder2Pages = f2PagesList.ToArray();
            int totalFolder2Items = MasterFolder2Pool.Length;
            var f2Page0 = folder2Pages[0];

            _previewPageWidth = actualCols * 78.0;
            _totalPreviewPages = Math.Max(1, folder1Pages.Length);

            if (_currentPreviewPage >= _totalPreviewPages) _currentPreviewPage = _totalPreviewPages - 1;
            if (_currentPreviewPage < 0) _currentPreviewPage = 0;

            bool isSlider = string.Equals(_pagingMode, "Slider", StringComparison.OrdinalIgnoreCase);
            bool showPager = isSlider && _totalPreviewPages > 1;

            double naturalW = actualCols * 78.0 + 24.0;
            double naturalH = (actualRows * 86.0) + 36.0 /* header */ + (showPager ? 30.0 : 0.0) /* footer */ + 22.0 /* inner padding */;

            void SetCardDimensions(Border? cardBorder, Border? innerClip, Image? blurImg) {
                if (cardBorder != null) {
                    cardBorder.Width = naturalW;
                    cardBorder.Height = naturalH;
                }
                if (innerClip != null) {
                    innerClip.Width = naturalW;
                    innerClip.Height = naturalH;
                    if (innerClip.Clip is RectangleGeometry rg) {
                        rg.Rect = new Rect(0, 0, naturalW, naturalH);
                    }
                }
                if (blurImg != null) {
                    blurImg.Width = naturalW;
                    blurImg.Height = naturalH;
                }
            }

            SetCardDimensions(Step1CardBorderInner, Step1InnerClippingBorder, Step1BlurredWallpaperImage);
            SetCardDimensions(Step1CardBorderInner2, Step1InnerClippingBorder2, Step1BlurredWallpaperImage2);
            SetCardDimensions(Step2CardBorderInner, Step2InnerClippingBorder, Step2BlurredWallpaperImage);
            SetCardDimensions(Step2CardBorderInner2, Step2InnerClippingBorder2, Step2BlurredWallpaperImage2);
            SetCardDimensions(Step3CardBorderInner, Step3InnerClippingBorder, Step3BlurredWallpaperImage);
            SetCardDimensions(Step3CardBorderInner2, Step3InnerClippingBorder2, Step3BlurredWallpaperImage2);

            void ConfigureViews(Grid? sliderVp, ScrollViewer? scrollVw, Grid? pagerGrid, Button? prevBtn, Button? nextBtn) {
                if (sliderVp != null) sliderVp.Visibility = isSlider ? Visibility.Visible : Visibility.Collapsed;
                if (scrollVw != null) scrollVw.Visibility = isSlider ? Visibility.Collapsed : Visibility.Visible;
                if (pagerGrid != null) pagerGrid.Visibility = showPager ? Visibility.Visible : Visibility.Collapsed;
                if (prevBtn != null) prevBtn.Visibility = (showPager && _sliderArrows) ? Visibility.Visible : Visibility.Collapsed;
                if (nextBtn != null) nextBtn.Visibility = (showPager && _sliderArrows) ? Visibility.Visible : Visibility.Collapsed;
            }

            ConfigureViews(Step1SliderViewport, Step1ScrollViewer, Step1FooterPagerGrid, Step1PrevBtn, Step1NextBtn);
            ConfigureViews(Step1SliderViewport2, Step1ScrollViewer2, Step1FooterPagerGrid2, null, null);
            ConfigureViews(Step2SliderViewport, Step2ScrollViewer, Step2FooterPagerGrid, Step2PrevBtn, Step2NextBtn);
            ConfigureViews(Step2SliderViewport2, Step2ScrollViewer2, Step2FooterPagerGrid2, null, null);
            ConfigureViews(Step3SliderViewport, Step3ScrollViewer, Step3FooterPagerGrid, Step3PrevBtn, Step3NextBtn);
            ConfigureViews(Step3SliderViewport2, Step3ScrollViewer2, Step3FooterPagerGrid2, null, null);

            // Folder 1 Slider Pages (Panak)
            BuildSliderPages(Step1SliderPagesPanel, actualCols, actualRows, folder1Pages);
            BuildSliderPages(Step2SliderPagesPanel, actualCols, actualRows, folder1Pages);
            BuildSliderPages(Step3SliderPagesPanel, actualCols, actualRows, folder1Pages);

            // Folder 2 Slider Pages (Projects)
            BuildSliderPages(Step1SliderPagesPanel2, actualCols, actualRows, folder2Pages);
            BuildSliderPages(Step2SliderPagesPanel2, actualCols, actualRows, folder2Pages);
            BuildSliderPages(Step3SliderPagesPanel2, actualCols, actualRows, folder2Pages);

            // Scroll Grids
            BuildScrollGrid(Step1ScrollGrid, actualCols, folder1Pages);
            BuildScrollGrid(Step2ScrollGrid, actualCols, folder1Pages);
            BuildScrollGrid(Step3ScrollGrid, actualCols, folder1Pages);

            BuildScrollGrid(Step1ScrollGrid2, actualCols, folder2Pages);
            BuildScrollGrid(Step2ScrollGrid2, actualCols, folder2Pages);
            BuildScrollGrid(Step3ScrollGrid2, actualCols, folder2Pages);

            ApplyWelcomeDesktopZoom();

            // Desktop folder icons: 2x2 mini grid showing the EXACT first 4 items of each popup
            UpdateDesktopIconMiniGrid(Step1DesktopIcon1MiniGrid, f1Page0);
            UpdateDesktopIconMiniGrid(Step2DesktopIcon1MiniGrid, f1Page0);
            UpdateDesktopIconMiniGrid(Step3DesktopIcon1MiniGrid, f1Page0);

            UpdateDesktopIconMiniGrid(Step1DesktopIcon2MiniGrid, f2Page0);
            UpdateDesktopIconMiniGrid(Step2DesktopIcon2MiniGrid, f2Page0);
            UpdateDesktopIconMiniGrid(Step3DesktopIcon2MiniGrid, f2Page0);

            UpdateAllPreviewDots();
            AnimatePreviewPage(false);

            var loc = LocalizationService.Instance;
            string countFormatted1 = string.Format(loc["ItemsCountBadge"] ?? "{0} նիշք", totalFolder1Items);
            string countFormatted2 = string.Format(loc["ItemsCountBadge"] ?? "{0} նիշք", totalFolder2Items);
            if (Step1PreviewCountText != null) Step1PreviewCountText.Text = countFormatted1;
            if (Step2PreviewCountText != null) Step2PreviewCountText.Text = countFormatted1;
            if (Step3PreviewCountText != null) Step3PreviewCountText.Text = countFormatted1;
            if (Step1PreviewCountText2 != null) Step1PreviewCountText2.Text = countFormatted2;
            if (Step2PreviewCountText2 != null) Step2PreviewCountText2.Text = countFormatted2;
            if (Step3PreviewCountText2 != null) Step3PreviewCountText2.Text = countFormatted2;
        }

        private static ImageSource? GetMockIconSource(bool isFolder, string ext) {
            try {
                if (isFolder) {
                    if (_cachedFolderIcon != null) return _cachedFolderIcon;
                    _cachedFolderIcon = ShellHelper.GetFolderIconSource(isLarge: true);
                    return _cachedFolderIcon;
                }

                if (!string.IsNullOrEmpty(ext)) {
                    if (_cachedMockIcons.TryGetValue(ext, out var cached)) return cached;
                    var src = ShellHelper.GetFileIconSource("sample" + ext, isLarge: true);
                    if (src != null) {
                        _cachedMockIcons[ext] = src;
                        return src;
                    }
                }
            } catch { }
            return null;
        }

        private static FrameworkElement CreateCrispFileBadge(string ext) {
            string normExt = (ext ?? "").TrimStart('.').ToUpperInvariant();
            string label = string.IsNullOrEmpty(normExt) ? "FILE" : (normExt.Length > 4 ? normExt.Substring(0, 4) : normExt);

            Color bg;
            switch (normExt) {
                case "DOCX":
                case "DOC":
                    bg = Color.FromRgb(43, 87, 154);
                    break;
                case "XLSX":
                case "XLS":
                    bg = Color.FromRgb(33, 115, 70);
                    break;
                case "PDF":
                    bg = Color.FromRgb(211, 47, 47);
                    break;
                case "PNG":
                case "JPG":
                case "JPEG":
                case "SVG":
                case "GIF":
                    bg = Color.FromRgb(2, 136, 209);
                    break;
                case "MP4":
                case "MOV":
                case "AVI":
                    bg = Color.FromRgb(123, 31, 162);
                    break;
                case "MP3":
                case "WAV":
                case "FLAC":
                    bg = Color.FromRgb(230, 81, 0);
                    break;
                case "ZIP":
                case "RAR":
                case "7Z":
                    bg = Color.FromRgb(245, 127, 23);
                    break;
                case "EXE":
                case "MSI":
                    bg = Color.FromRgb(57, 73, 171);
                    break;
                case "CS":
                case "JS":
                case "TS":
                case "JSON":
                case "SQL":
                    bg = Color.FromRgb(0, 137, 123);
                    break;
                default:
                    bg = Color.FromRgb(84, 110, 122);
                    break;
            }

            var badge = new Border {
                Width = 26,
                Height = 30,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(bg),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect {
                    BlurRadius = 3,
                    ShadowDepth = 1,
                    Opacity = 0.25,
                    Color = Colors.Black
                }
            };

            var grid = new Grid();
            var fold = new Border {
                Width = 7,
                Height = 7,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Background = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
                CornerRadius = new CornerRadius(0, 4, 0, 4)
            };
            grid.Children.Add(fold);

            var txt = new TextBlock {
                Text = label,
                FontSize = label.Length > 3 ? 6.5 : 7.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(1, 0, 1, 3)
            };
            grid.Children.Add(txt);

            badge.Child = grid;
            return badge;
        }

        private static FrameworkElement CreateVectorFolderIcon() {
            return new System.Windows.Shapes.Path {
                Data = Geometry.Parse("M 2,4 L 10,4 L 12,6 L 26,6 C 27.1,6 28,6.9 28,8 L 28,22 C 28,23.1 27.1,24 26,24 L 2,24 C 0.9,24 0,23.1 0,22 L 0,6 C 0,4.9 0.9,4 2,4 Z"),
                Fill = new SolidColorBrush(Color.FromRgb(255, 179, 0)),
                Stretch = Stretch.Uniform,
                Width = 30,
                Height = 26,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect {
                    BlurRadius = 3,
                    ShadowDepth = 1,
                    Opacity = 0.25,
                    Color = Colors.Black
                }
            };
        }

        private static FrameworkElement BuildMockItemIconVisual(bool isFolder, string ext) {
            if (isFolder) {
                var src = GetMockIconSource(true, "");
                if (src != null) {
                    var img = new Image {
                        Source = src,
                        Width = 43,
                        Height = 43,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    return img;
                }
                return CreateVectorFolderIcon();
            } else {
                var src = GetMockIconSource(false, ext);
                if (src != null) {
                    var img = new Image {
                        Source = src,
                        Width = 43,
                        Height = 43,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    return img;
                }
                return CreateCrispFileBadge(ext);
            }
        }

        private static FrameworkElement BuildMiniMockItemVisual(bool isFolder, string ext) {
            if (isFolder) {
                var src = GetMockIconSource(true, "");
                if (src != null) {
                    var img = new Image {
                        Source = src,
                        Width = 13,
                        Height = 13,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    return img;
                }
                return new System.Windows.Shapes.Path {
                    Data = Geometry.Parse("M 2,4 L 10,4 L 12,6 L 26,6 C 27.1,6 28,6.9 28,8 L 28,22 C 28,23.1 27.1,24 26,24 L 2,24 C 0.9,24 0,23.1 0,22 L 0,6 C 0,4.9 0.9,4 2,4 Z"),
                    Fill = new SolidColorBrush(Color.FromRgb(255, 179, 0)),
                    Stretch = Stretch.Uniform,
                    Width = 12,
                    Height = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            } else {
                var src = GetMockIconSource(false, ext);
                if (src != null) {
                    var img = new Image {
                        Source = src,
                        Width = 13,
                        Height = 13,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    return img;
                }
                return CreateMiniCrispFileBadge(ext);
            }
        }

        private static FrameworkElement CreateMiniCrispFileBadge(string ext) {
            string normExt = (ext ?? "").TrimStart('.').ToUpperInvariant();
            string label = string.IsNullOrEmpty(normExt) ? "F" : (normExt.Length > 3 ? normExt.Substring(0, 3) : normExt);
            Color bg;
            switch (normExt) {
                case "DOCX": case "DOC": bg = Color.FromRgb(43, 87, 154); break;
                case "XLSX": case "XLS": bg = Color.FromRgb(33, 115, 70); break;
                case "PDF": bg = Color.FromRgb(211, 47, 47); break;
                case "PNG": case "JPG": case "JPEG": case "SVG": bg = Color.FromRgb(2, 136, 209); break;
                case "MP4": case "MOV": case "AVI": bg = Color.FromRgb(123, 31, 162); break;
                case "MP3": case "WAV": bg = Color.FromRgb(230, 81, 0); break;
                case "ZIP": case "RAR": bg = Color.FromRgb(245, 127, 23); break;
                case "EXE": case "MSI": bg = Color.FromRgb(57, 73, 171); break;
                default: bg = Color.FromRgb(84, 110, 122); break;
            }
            var border = new Border {
                Width = 12,
                Height = 13,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(bg),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var text = new TextBlock {
                Text = label,
                FontSize = 6,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            border.Child = text;
            return border;
        }

        private static void UpdateDesktopIconMiniGrid(System.Windows.Controls.Primitives.UniformGrid? grid, (string Name, bool IsFolder, string Ext)[] page0Items) {
            if (grid == null) return;
            grid.Children.Clear();
            var firstFour = page0Items.Take(4).ToList();
            foreach (var item in firstFour) {
                var cell = new Border {
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Color.FromArgb(0x35, 255, 255, 255)),
                    Margin = new Thickness(1.5),
                    Child = BuildMiniMockItemVisual(item.IsFolder, item.Ext)
                };
                grid.Children.Add(cell);
            }
            while (grid.Children.Count < 4) {
                grid.Children.Add(new Border {
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Color.FromArgb(0x18, 255, 255, 255)),
                    Margin = new Thickness(1.5)
                });
            }
        }

        private FrameworkElement CreateMockItemView(string name, bool isFolder, string ext, double cellW, double cellH) {
            double itemW = Math.Min(cellW - 6.0, 68.0);
            double itemH = Math.Min(cellH - 8.0, 76.0);
            var border = new Border {
                Width = itemW > 0 ? itemW : 68.0,
                Height = itemH > 0 ? itemH : 76.0,
                Margin = new Thickness(Math.Max(1.0, (cellW - itemW) / 2.0), Math.Max(1.0, (cellH - itemH) / 2.0), Math.Max(1.0, (cellW - itemW) / 2.0), Math.Max(1.0, (cellH - itemH) / 2.0)),
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Padding = new Thickness(2, 4, 2, 4)
            };
            border.MouseEnter += (s, e) => border.Background = Resources["PopupItemHoverBrush"] as Brush ?? new SolidColorBrush(Color.FromArgb(45, 255, 255, 255));
            border.MouseLeave += (s, e) => border.Background = Brushes.Transparent;

            var sp = new StackPanel {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = Math.Max(20, itemW - 4.0)
            };

            var iconContainer = new Grid {
                Width = 47,
                Height = 47,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconContainer.Children.Add(BuildMockItemIconVisual(isFolder, ext));

            var nameText = new TextBlock {
                Text = name,
                FontSize = 11.0,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 24,
                Margin = new Thickness(0, 2, 0, 0),
                ToolTip = name
            };
            nameText.SetResourceReference(TextBlock.ForegroundProperty, "PopupTextBrush");

            sp.Children.Add(iconContainer);
            sp.Children.Add(nameText);
            border.Child = sp;
            return border;
        }

        private void BuildSliderPages(StackPanel? panel, int cols, int rows, (string Name, bool IsFolder, string Ext)[][] folderPages) {
            if (panel == null) return;
            panel.Children.Clear();
            panel.HorizontalAlignment = HorizontalAlignment.Left;
            panel.Width = _totalPreviewPages * _previewPageWidth;
            double contentH = rows * 86.0;
            panel.Height = contentH;

            double cellW = _previewPageWidth / Math.Max(1, cols);
            double cellH = 86.0;
            int itemsPerPage = Math.Max(1, cols * rows);

            for (int p = 0; p < _totalPreviewPages; p++) {
                var grid = new UniformGrid {
                    Columns = cols,
                    Rows = rows,
                    Width = _previewPageWidth,
                    Height = contentH,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var pageItems = folderPages[p % folderPages.Length];
                int count = Math.Min(itemsPerPage, pageItems.Length);
                for (int i = 0; i < count; i++) {
                    var item = pageItems[i];
                    grid.Children.Add(CreateMockItemView(item.Name, item.IsFolder, item.Ext, cellW, cellH));
                }

                panel.Children.Add(grid);
            }
        }

        private void BuildScrollGrid(UniformGrid? grid, int cols, (string Name, bool IsFolder, string Ext)[][] folderPages) {
            if (grid == null) return;
            grid.Children.Clear();
            grid.Columns = cols;
            grid.Width = _previewPageWidth;

            double cellW = _previewPageWidth / Math.Max(1, cols);
            double cellH = 86.0;

            foreach (var page in folderPages) {
                foreach (var item in page) {
                    grid.Children.Add(CreateMockItemView(item.Name, item.IsFolder, item.Ext, cellW, cellH));
                }
            }
        }

        private void UpdatePreviewDots(StackPanel? panel) {
            if (panel == null) return;
            panel.Children.Clear();

            if (_totalPreviewPages <= 1) return;

            var activeBrush = Resources["M3PrimaryBrush"] as Brush ?? Brushes.DarkKhaki;
            var inactiveBrush = Resources["M3TrackInactiveBrush"] as Brush ?? Brushes.LightGray;

            for (int i = 0; i < _totalPreviewPages; i++) {
                int pageIndex = i;
                bool isActive = (i == _currentPreviewPage);
                var dot = new Border {
                    Width = isActive ? 18 : 6,
                    Height = 6,
                    CornerRadius = new CornerRadius(3),
                    Background = isActive ? activeBrush : inactiveBrush,
                    Margin = new Thickness(2.5, 0, 2.5, 0),
                    Cursor = Cursors.Hand
                };
                dot.MouseDown += (s, e) => {
                    _currentPreviewPage = pageIndex;
                    AnimatePreviewPage(true);
                    UpdateAllPreviewDots();
                };
                panel.Children.Add(dot);
            }
        }

        private void UpdateAllPreviewDots() {
            UpdatePreviewDots(Step1DotsPanel);
            UpdatePreviewDots(Step1DotsPanel2);
            UpdatePreviewDots(Step2DotsPanel);
            UpdatePreviewDots(Step2DotsPanel2);
            UpdatePreviewDots(Step3DotsPanel);
            UpdatePreviewDots(Step3DotsPanel2);
        }

        private void SetAllPagesTranslateX(double x) {
            if (Step1PagesTranslate != null) { Step1PagesTranslate.BeginAnimation(TranslateTransform.XProperty, null); Step1PagesTranslate.X = x; }
            if (Step1PagesTranslate2 != null) { Step1PagesTranslate2.BeginAnimation(TranslateTransform.XProperty, null); Step1PagesTranslate2.X = x; }
            if (Step2PagesTranslate != null) { Step2PagesTranslate.BeginAnimation(TranslateTransform.XProperty, null); Step2PagesTranslate.X = x; }
            if (Step2PagesTranslate2 != null) { Step2PagesTranslate2.BeginAnimation(TranslateTransform.XProperty, null); Step2PagesTranslate2.X = x; }
            if (Step3PagesTranslate != null) { Step3PagesTranslate.BeginAnimation(TranslateTransform.XProperty, null); Step3PagesTranslate.X = x; }
            if (Step3PagesTranslate2 != null) { Step3PagesTranslate2.BeginAnimation(TranslateTransform.XProperty, null); Step3PagesTranslate2.X = x; }
        }

        private void AnimatePreviewPage(bool animate) {
            double targetX = -_currentPreviewPage * _previewPageWidth;
            if (animate) {
                var anim = new DoubleAnimation {
                    To = targetX,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                if (Step1PagesTranslate != null) Step1PagesTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
                if (Step1PagesTranslate2 != null) Step1PagesTranslate2.BeginAnimation(TranslateTransform.XProperty, anim);
                if (Step2PagesTranslate != null) Step2PagesTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
                if (Step2PagesTranslate2 != null) Step2PagesTranslate2.BeginAnimation(TranslateTransform.XProperty, anim);
                if (Step3PagesTranslate != null) Step3PagesTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
                if (Step3PagesTranslate2 != null) Step3PagesTranslate2.BeginAnimation(TranslateTransform.XProperty, anim);
            } else {
                SetAllPagesTranslateX(targetX);
            }
        }

        private void PreviewSlider_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (string.Equals(_pagingMode, "Slider", StringComparison.OrdinalIgnoreCase)) {
                if (e.Delta < 0 && _currentPreviewPage < _totalPreviewPages - 1) {
                    _currentPreviewPage++;
                    AnimatePreviewPage(true);
                    UpdateAllPreviewDots();
                    e.Handled = true;
                } else if (e.Delta > 0 && _currentPreviewPage > 0) {
                    _currentPreviewPage--;
                    AnimatePreviewPage(true);
                    UpdateAllPreviewDots();
                    e.Handled = true;
                }
            }
        }

        private void PreviewSlider_MouseDown(object sender, MouseButtonEventArgs e) {
            if (_sliderDrag && e.LeftButton == MouseButtonState.Pressed) {
                _isDraggingSlider = true;
                _dragStartPos = e.GetPosition(this);
                if (sender is UIElement el) el.CaptureMouse();
            }
        }

        private void PreviewSlider_MouseMove(object sender, MouseEventArgs e) {
            if (_isDraggingSlider && _sliderDrag && e.LeftButton == MouseButtonState.Pressed) {
                var curPos = e.GetPosition(this);
                double deltaX = curPos.X - _dragStartPos.X;
                double baseX = -_currentPreviewPage * _previewPageWidth;
                SetAllPagesTranslateX(baseX + deltaX);
            }
        }

        private void PreviewSlider_MouseUp(object sender, MouseButtonEventArgs e) {
            if (_isDraggingSlider) {
                _isDraggingSlider = false;
                if (sender is UIElement el) el.ReleaseMouseCapture();
                var endPos = e.GetPosition(this);
                double deltaX = endPos.X - _dragStartPos.X;
                if (deltaX < -30 && _currentPreviewPage < _totalPreviewPages - 1) {
                    _currentPreviewPage++;
                } else if (deltaX > 30 && _currentPreviewPage > 0) {
                    _currentPreviewPage--;
                }
                AnimatePreviewPage(true);
                UpdateAllPreviewDots();
            }
        }

        private void Step1PrevBtn_Click(object sender, RoutedEventArgs e) {
            if (_currentPreviewPage > 0) {
                _currentPreviewPage--;
                AnimatePreviewPage(true);
                UpdateAllPreviewDots();
            }
        }

        private void Step1NextBtn_Click(object sender, RoutedEventArgs e) {
            if (_currentPreviewPage < _totalPreviewPages - 1) {
                _currentPreviewPage++;
                AnimatePreviewPage(true);
                UpdateAllPreviewDots();
            }
        }

        private void TogglePopup1() {
            _isPopup1Open = !_isPopup1Open;
            if (_isPopup1Open && _isPopup2Open) {
                _isPopup2Open = false;
                AnimatePopupState(
                    false,
                    new[] { Step1PopupContainer2, Step2PopupContainer2, Step3PopupContainer2 },
                    new[] { Step1PopupScale2, Step2PopupScale2, Step3PopupScale2 }
                );
            }
            if (!_isPopup1Open) {
                CloseStep1MoreActionsMenuImmediate();
            } else {
                UpdateWelcomeAllIconAndPopupPositions();
            }
            AnimatePopupState(
                _isPopup1Open,
                new[] { Step1PopupContainer, Step2PopupContainer, Step3PopupContainer },
                new[] { Step1PopupScale, Step2PopupScale, Step3PopupScale }
            );
            UpdateFocusModeVisuals(animate: true);
        }

        private void TogglePopup2() {
            _isPopup2Open = !_isPopup2Open;
            if (_isPopup2Open && _isPopup1Open) {
                _isPopup1Open = false;
                CloseStep1MoreActionsMenuImmediate();
                AnimatePopupState(
                    false,
                    new[] { Step1PopupContainer, Step2PopupContainer, Step3PopupContainer },
                    new[] { Step1PopupScale, Step2PopupScale, Step3PopupScale }
                );
            }
            if (_isPopup2Open) {
                UpdateWelcomeAllIconAndPopupPositions();
            }
            AnimatePopupState(
                _isPopup2Open,
                new[] { Step1PopupContainer2, Step2PopupContainer2, Step3PopupContainer2 },
                new[] { Step1PopupScale2, Step2PopupScale2, Step3PopupScale2 }
            );
            UpdateFocusModeVisuals(animate: true);
        }

        private void ClosePopup1() {
            if (_isPopup1Open) {
                CloseStep1MoreActionsMenuImmediate();
                TogglePopup1();
            }
        }

        private void ClosePopup2() {
            if (_isPopup2Open) TogglePopup2();
        }

        private void AnimatePopupState(bool open, Grid?[] containers, ScaleTransform?[] scales) {
            if (open) {
                UpdateWelcomeAllIconAndPopupPositions();
            }

            if (!open) {
                var scaleAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                var opacityAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(180));
                scaleAnim.Completed += (s, e) => {
                    foreach (var container in containers) {
                        if (container != null) {
                            container.Visibility = Visibility.Collapsed;
                            container.BeginAnimation(UIElement.OpacityProperty, null);
                            container.Opacity = 0.0;
                        }
                    }
                    for (int i = 0; i < scales.Length; i++) {
                        scales[i]?.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                        scales[i]?.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                        if (scales[i] != null) {
                            scales[i]!.ScaleX = 0.0;
                            scales[i]!.ScaleY = 0.0;
                        }
                    }
                };
                for (int i = 0; i < scales.Length; i++) {
                    scales[i]?.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    scales[i]?.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                }
                foreach (var container in containers) {
                    container?.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
                }
            } else {
                foreach (var container in containers) {
                    if (container != null) {
                        container.Visibility = Visibility.Visible;
                        container.Opacity = 0.0;
                    }
                }
                UpdateWallpaperCrop();
                UpdateLivePreview();
                var scaleAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var opacityAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200));
                scaleAnim.Completed += (s, e) => {
                    for (int i = 0; i < scales.Length; i++) {
                        scales[i]?.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                        scales[i]?.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                        if (scales[i] != null) {
                            scales[i]!.ScaleX = 1.0;
                            scales[i]!.ScaleY = 1.0;
                        }
                    }
                    UpdateWallpaperCrop();
                };
                opacityAnim.Completed += (s, e) => {
                    foreach (var container in containers) {
                        if (container != null) {
                            container.BeginAnimation(UIElement.OpacityProperty, null);
                            container.Opacity = 1.0;
                        }
                    }
                };
                for (int i = 0; i < scales.Length; i++) {
                    scales[i]?.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    scales[i]?.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                }
                foreach (var container in containers) {
                    container?.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
                }
            }
        }

        private (Point targetPos, Point origin) CalculateSmartPopupPosition(Point iconPos, double iconW, double iconH, double popupW, double popupH, double canvasW, double canvasH) {
            double iconLeft = iconPos.X;
            double iconTop = iconPos.Y;
            double iconRight = iconLeft + iconW;
            double iconBottom = iconTop + iconH;

            double spaceRight = canvasW - iconLeft;
            double spaceLeft = iconRight;
            double spaceUp = iconBottom;
            double spaceDown = canvasH - iconTop;

            bool openLeft = spaceRight >= popupW ? false : (spaceLeft >= popupW ? true : (spaceLeft > spaceRight));
            bool openUp = spaceUp >= popupH ? true : (spaceDown >= popupH ? false : (spaceUp >= spaceDown));

            Point origin;
            double targetLeft;
            double targetTop;

            if (!openLeft && openUp) {
                // Bottom-left anchor (opens up & right)
                origin = new Point(0.0, 1.0);
                targetLeft = iconLeft;
                targetTop = iconBottom - popupH;
            } else if (openLeft && openUp) {
                // Bottom-right anchor (opens up & left)
                origin = new Point(1.0, 1.0);
                targetLeft = iconRight - popupW;
                targetTop = iconBottom - popupH;
            } else if (!openLeft && !openUp) {
                // Top-left anchor (opens down & right)
                origin = new Point(0.0, 0.0);
                targetLeft = iconLeft;
                targetTop = iconTop;
            } else {
                // Top-right anchor (opens down & left)
                origin = new Point(1.0, 0.0);
                targetLeft = iconRight - popupW;
                targetTop = iconTop;
            }

            targetLeft = Math.Clamp(targetLeft, 4.0, Math.Max(4.0, canvasW - popupW - 4.0));
            targetTop = Math.Clamp(targetTop, 4.0, Math.Max(4.0, canvasH - popupH - 4.0));

            return (new Point(targetLeft, targetTop), origin);
        }

        private static Point SnapToDesktopGrid(double rawX, double rawY, double canvasW, double canvasH) {
            const double marginX = 12.0;
            const double marginY = 12.0;
            const double gridStepX = 68.0;
            const double gridStepY = 76.0;

            int maxCol = Math.Max(0, (int)Math.Floor((canvasW - 56.0 - marginX) / gridStepX));
            int maxRow = Math.Max(0, (int)Math.Floor((canvasH - 72.0 - marginY) / gridStepY));

            int col = (int)Math.Round((rawX - marginX) / gridStepX);
            int row = (int)Math.Round((rawY - marginY) / gridStepY);

            col = Math.Clamp(col, 0, maxCol);
            row = Math.Clamp(row, 0, maxRow);

            return new Point(marginX + col * gridStepX, marginY + row * gridStepY);
        }

        private void UpdateWelcomeAllIconAndPopupPositions() {
            var activeCanvas = (_currentStep == 0 ? Step1DesktopCanvas : (_currentStep == 1 ? Step2DesktopCanvas : Step3DesktopCanvas)) ?? Step1DesktopCanvas;
            double canvasW = activeCanvas?.Width > 0 ? activeCanvas.Width : 380.0;
            double canvasH = activeCanvas?.Height > 0 ? activeCanvas.Height : 450.0;

            if (_welcomeIcon1Pos.X < 0) {
                _welcomeIcon1Pos = new Point(12.0, 12.0);
            }
            _welcomeIcon1Pos = SnapToDesktopGrid(_welcomeIcon1Pos.X, _welcomeIcon1Pos.Y, canvasW, canvasH);

            if (_welcomeIcon2Pos.X < 0) {
                _welcomeIcon2Pos = new Point(80.0, 12.0);
            }
            _welcomeIcon2Pos = SnapToDesktopGrid(_welcomeIcon2Pos.X, _welcomeIcon2Pos.Y, canvasW, canvasH);

            void SetIconPos(UIElement? el, Point pt) {
                if (el != null) {
                    Canvas.SetLeft(el, pt.X);
                    Canvas.SetTop(el, pt.Y);
                }
            }

            SetIconPos(Step1DesktopIcon1, _welcomeIcon1Pos);
            SetIconPos(Step2DesktopIcon1, _welcomeIcon1Pos);
            SetIconPos(Step3DesktopIcon1, _welcomeIcon1Pos);

            SetIconPos(Step1DesktopIcon2, _welcomeIcon2Pos);
            SetIconPos(Step2DesktopIcon2, _welcomeIcon2Pos);
            SetIconPos(Step3DesktopIcon2, _welcomeIcon2Pos);

            double cardW = (Step1CardBorderInner != null && Step1CardBorderInner.Width > 20) ? Step1CardBorderInner.Width : 258.0;
            double cardH = (Step1CardBorderInner != null && Step1CardBorderInner.Height > 20) ? Step1CardBorderInner.Height : 340.0;
            double cardW2 = (Step1CardBorderInner2 != null && Step1CardBorderInner2.Width > 20) ? Step1CardBorderInner2.Width : 258.0;
            double cardH2 = (Step1CardBorderInner2 != null && Step1CardBorderInner2.Height > 20) ? Step1CardBorderInner2.Height : 340.0;

            var (pos1, origin1) = CalculateSmartPopupPosition(_welcomeIcon1Pos, 52.0, 68.0, cardW, cardH, canvasW, canvasH);
            var (pos2, origin2) = CalculateSmartPopupPosition(_welcomeIcon2Pos, 52.0, 68.0, cardW2, cardH2, canvasW, canvasH);

            void ApplyPopupPos(Grid?[] grids, Point pos, Point origin) {
                foreach (var g in grids) {
                    if (g != null) {
                        Canvas.SetLeft(g, pos.X);
                        Canvas.SetTop(g, pos.Y);
                        g.RenderTransformOrigin = origin;
                    }
                }
            }

            ApplyPopupPos(new[] { Step1PopupContainer, Step2PopupContainer, Step3PopupContainer }, pos1, origin1);
            ApplyPopupPos(new[] { Step1PopupContainer2, Step2PopupContainer2, Step3PopupContainer2 }, pos2, origin2);
        }

        private void ApplyWelcomeDesktopZoom() {
            var canvases = new[] { Step1DesktopCanvas, Step2DesktopCanvas, Step3DesktopCanvas };
            var transforms = new[] { Step1DesktopScaleTransform, Step2DesktopScaleTransform, Step3DesktopScaleTransform };
            var borders = new[] { Step1PreviewBorder, Step2PreviewBorder, Step3PreviewBorder };

            for (int i = 0; i < canvases.Length; i++) {
                var canvas = canvases[i];
                var transform = transforms[i];
                var border = borders[i];
                if (canvas == null || transform == null || border == null) continue;

                double bW = border.ActualWidth > 0 ? border.ActualWidth : 380.0;
                double bH = border.ActualHeight > 0 ? border.ActualHeight : 450.0;
                double vW = bW / _welcomeDesktopZoomScale;
                double vH = bH / _welcomeDesktopZoomScale;

                canvas.Width = vW;
                canvas.Height = vH;
                transform.ScaleX = _welcomeDesktopZoomScale;
                transform.ScaleY = _welcomeDesktopZoomScale;
            }

            UpdateWelcomeAllIconAndPopupPositions();
        }

        private void PreviewDesktop_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
                e.Handled = true;
                double step = e.Delta > 0 ? 0.05 : -0.05;
                _welcomeDesktopZoomScale = Math.Clamp(_welcomeDesktopZoomScale + step, 0.35, 1.50);
                ApplyWelcomeDesktopZoom();
                UpdateWallpaperCrop();
            }
        }

        private void LivePreviewBadge_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 2 || e.ChangedButton == MouseButton.Middle) {
                _welcomeDesktopZoomScale = 1.0;
                ApplyWelcomeDesktopZoom();
                UpdateWallpaperCrop();
                e.Handled = true;
            }
        }

        private void DesktopIcon1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (sender is UIElement el) {
                _isDraggingWelcomeIcon1 = true;
                _hasDraggedWelcomeIcon1 = false;
                var canvas = (VisualTreeHelper.GetParent(el) as Canvas) ?? Step1DesktopCanvas;
                _dragWelcomeIcon1StartMouse = canvas != null ? e.GetPosition(canvas) : e.GetPosition(this);
                _dragWelcomeIcon1StartPos = _welcomeIcon1Pos;
                el.CaptureMouse();
                e.Handled = true;
            }
        }

        private void DesktopIcon1_MouseMove(object sender, MouseEventArgs e) {
            if (_isDraggingWelcomeIcon1 && sender is UIElement el) {
                var canvas = (VisualTreeHelper.GetParent(el) as Canvas) ?? Step1DesktopCanvas;
                var curMouse = canvas != null ? e.GetPosition(canvas) : e.GetPosition(this);
                var delta = curMouse - _dragWelcomeIcon1StartMouse;
                if (!_hasDraggedWelcomeIcon1 && (Math.Abs(delta.X) > 3 || Math.Abs(delta.Y) > 3)) {
                    _hasDraggedWelcomeIcon1 = true;
                }
                if (_hasDraggedWelcomeIcon1) {
                    double cW = canvas?.Width > 0 ? canvas.Width : 380.0;
                    double cH = canvas?.Height > 0 ? canvas.Height : 450.0;
                    _welcomeIcon1Pos = SnapToDesktopGrid(_dragWelcomeIcon1StartPos.X + delta.X, _dragWelcomeIcon1StartPos.Y + delta.Y, cW, cH);
                    UpdateWelcomeAllIconAndPopupPositions();
                    UpdateWallpaperCrop();
                }
                e.Handled = true;
            }
        }

        private void DesktopIcon1_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (_isDraggingWelcomeIcon1 && sender is UIElement el) {
                _isDraggingWelcomeIcon1 = false;
                el.ReleaseMouseCapture();
                if (!_hasDraggedWelcomeIcon1) {
                    TogglePopup1();
                }
                e.Handled = true;
            }
        }

        private void DesktopIcon2_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (sender is UIElement el) {
                _isDraggingWelcomeIcon2 = true;
                _hasDraggedWelcomeIcon2 = false;
                var canvas = (VisualTreeHelper.GetParent(el) as Canvas) ?? Step1DesktopCanvas;
                _dragWelcomeIcon2StartMouse = canvas != null ? e.GetPosition(canvas) : e.GetPosition(this);
                _dragWelcomeIcon2StartPos = _welcomeIcon2Pos;
                el.CaptureMouse();
                e.Handled = true;
            }
        }

        private void DesktopIcon2_MouseMove(object sender, MouseEventArgs e) {
            if (_isDraggingWelcomeIcon2 && sender is UIElement el) {
                var canvas = (VisualTreeHelper.GetParent(el) as Canvas) ?? Step1DesktopCanvas;
                var curMouse = canvas != null ? e.GetPosition(canvas) : e.GetPosition(this);
                var delta = curMouse - _dragWelcomeIcon2StartMouse;
                if (!_hasDraggedWelcomeIcon2 && (Math.Abs(delta.X) > 3 || Math.Abs(delta.Y) > 3)) {
                    _hasDraggedWelcomeIcon2 = true;
                }
                if (_hasDraggedWelcomeIcon2) {
                    double cW = canvas?.Width > 0 ? canvas.Width : 380.0;
                    double cH = canvas?.Height > 0 ? canvas.Height : 450.0;
                    _welcomeIcon2Pos = SnapToDesktopGrid(_dragWelcomeIcon2StartPos.X + delta.X, _dragWelcomeIcon2StartPos.Y + delta.Y, cW, cH);
                    UpdateWelcomeAllIconAndPopupPositions();
                    UpdateWallpaperCrop();
                }
                e.Handled = true;
            }
        }

        private void PreviewDesktopCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            var source = e.OriginalSource as DependencyObject;
            if (source == null) return;

            bool insidePopup1 = (Step1PopupContainer != null && (Step1PopupContainer == source || Step1PopupContainer.IsAncestorOf(source))) ||
                                (Step2PopupContainer != null && (Step2PopupContainer == source || Step2PopupContainer.IsAncestorOf(source))) ||
                                (Step3PopupContainer != null && (Step3PopupContainer == source || Step3PopupContainer.IsAncestorOf(source)));

            bool insideIcon1 = (Step1DesktopIcon1 != null && (Step1DesktopIcon1 == source || Step1DesktopIcon1.IsAncestorOf(source))) ||
                               (Step2DesktopIcon1 != null && (Step2DesktopIcon1 == source || Step2DesktopIcon1.IsAncestorOf(source))) ||
                               (Step3DesktopIcon1 != null && (Step3DesktopIcon1 == source || Step3DesktopIcon1.IsAncestorOf(source)));

            bool insidePopup2 = (Step1PopupContainer2 != null && (Step1PopupContainer2 == source || Step1PopupContainer2.IsAncestorOf(source))) ||
                                (Step2PopupContainer2 != null && (Step2PopupContainer2 == source || Step2PopupContainer2.IsAncestorOf(source))) ||
                                (Step3PopupContainer2 != null && (Step3PopupContainer2 == source || Step3PopupContainer2.IsAncestorOf(source)));

            bool insideIcon2 = (Step1DesktopIcon2 != null && (Step1DesktopIcon2 == source || Step1DesktopIcon2.IsAncestorOf(source))) ||
                               (Step2DesktopIcon2 != null && (Step2DesktopIcon2 == source || Step2DesktopIcon2.IsAncestorOf(source))) ||
                               (Step3DesktopIcon2 != null && (Step3DesktopIcon2 == source || Step3DesktopIcon2.IsAncestorOf(source)));

            if (_isPopup1Open && !insidePopup1 && !insideIcon1) {
                ClosePopup1();
            }
            if (_isPopup2Open && !insidePopup2 && !insideIcon2) {
                ClosePopup2();
            }
        }

        private void DesktopIcon2_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (_isDraggingWelcomeIcon2 && sender is UIElement el) {
                _isDraggingWelcomeIcon2 = false;
                el.ReleaseMouseCapture();
                if (!_hasDraggedWelcomeIcon2) {
                    TogglePopup2();
                }
                e.Handled = true;
            }
        }

        private void DesktopIcon1_MouseDown(object sender, MouseButtonEventArgs e) {
            TogglePopup1();
        }

        private void DesktopIcon_MouseDown(object sender, MouseButtonEventArgs e) {
            TogglePopup1();
        }

        private void DesktopIcon2_MouseDown(object sender, MouseButtonEventArgs e) {
            TogglePopup2();
        }

        private void PreviewCloseBtn_Click(object sender, RoutedEventArgs e) {
            ClosePopup1();
        }

        private void PreviewCloseBtn2_Click(object sender, RoutedEventArgs e) {
            ClosePopup2();
        }

        private void Step1PreviewCloseBtn_Click(object sender, RoutedEventArgs e) {
            ClosePopup1();
        }

        private void Step1PreviewCloseBtn2_Click(object sender, RoutedEventArgs e) {
            ClosePopup2();
        }

        private void Step2PreviewCloseBtn_Click(object sender, RoutedEventArgs e) {
            ClosePopup1();
        }

        private void Step2PreviewCloseBtn2_Click(object sender, RoutedEventArgs e) {
            ClosePopup2();
        }

        private void Step3PreviewCloseBtn_Click(object sender, RoutedEventArgs e) {
            ClosePopup1();
        }

        private void Step3PreviewCloseBtn2_Click(object sender, RoutedEventArgs e) {
            ClosePopup2();
        }

        private void PreviewBorder_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (sender is Border b && b.Clip is RectangleGeometry rectGeom) {
                rectGeom.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
                rectGeom.RadiusX = 28;
                rectGeom.RadiusY = 28;
            }
            ApplyWelcomeDesktopZoom();
            UpdateWallpaperCrop();
        }

        private void UpdateLivePreview(bool updateAllSteps = false, bool animate = false) {
            try {
                Color baseCol = ShellHelper.ParseWpfColorFast(_selectedColor, Color.FromRgb(0xFC, 0xDD, 0xC5));

                byte alpha = (byte)(Math.Clamp(_folderOpacity, 0.2, 1.0) * 255);
                var folderCol = Color.FromArgb(alpha, baseCol.R, baseCol.G, baseCol.B);

                bool isDark = _selectedThemeMode == "Dark" || (_selectedThemeMode == "Auto" && ShellHelper.IsWindowsDarkTheme());
                bool isLight = !isDark;
                var animDuration = TimeSpan.FromMilliseconds(260);

                void SetPopupBrush(string key, Color col) {
                    if (animate) {
                        ShellHelper.AnimateResourceBrushColor(Resources, key, col, animDuration);
                    } else {
                        ShellHelper.SetResourceBrushColor(Resources, key, col);
                    }
                }

                if (isLight) {
                    SetPopupBrush("PopupTextBrush", Color.FromRgb(15, 23, 42));
                    SetPopupBrush("PopupSubtextBrush", Color.FromRgb(100, 116, 139));
                    SetPopupBrush("PopupHeaderBtnBg", Color.FromArgb(30, 0, 0, 0));
                    SetPopupBrush("PopupHeaderBtnFg", Color.FromRgb(30, 41, 59));
                    SetPopupBrush("PopupItemBadgeBg", Color.FromArgb(25, 0, 0, 0));
                    SetPopupBrush("PopupItemBadgeFg", Color.FromRgb(71, 85, 105));
                    SetPopupBrush("PopupItemHoverBrush", Color.FromArgb(25, 0, 0, 0));
                } else {
                    SetPopupBrush("PopupTextBrush", Color.FromRgb(248, 250, 252));
                    SetPopupBrush("PopupSubtextBrush", Color.FromRgb(148, 163, 184));
                    SetPopupBrush("PopupHeaderBtnBg", Color.FromArgb(37, 255, 255, 255));
                    SetPopupBrush("PopupHeaderBtnFg", Color.FromRgb(226, 232, 240));
                    SetPopupBrush("PopupItemBadgeBg", Color.FromArgb(37, 255, 255, 255));
                    SetPopupBrush("PopupItemBadgeFg", Color.FromRgb(203, 213, 225));
                    SetPopupBrush("PopupItemHoverBrush", Color.FromArgb(45, 255, 255, 255));
                }

                Color frostedCol = !isLight
                    ? (_folderBlurEnabled ? Color.FromArgb(194, 22, 22, 29) : Color.FromArgb(235, 24, 24, 30))
                    : (_folderBlurEnabled ? Color.FromArgb(220, 248, 249, 250) : Color.FromArgb(245, 248, 249, 250));
                Color tintCol = Color.FromArgb(95, baseCol.R, baseCol.G, baseCol.B);
                double clampedOpacity = Math.Clamp(_folderOpacity, 0.20, 1.0);
                double tintOpacity = Math.Min(1.0, clampedOpacity * 1.1);
                Visibility blurVis = _folderBlurEnabled ? Visibility.Visible : Visibility.Collapsed;

                bool updateStep1 = updateAllSteps || _currentStep == 0 || (Step1Grid != null && Step1Grid.Visibility == Visibility.Visible);
                bool updateStep2 = updateAllSteps || _currentStep == 1 || (Step2Grid != null && Step2Grid.Visibility == Visibility.Visible);
                bool updateStep3 = updateAllSteps || _currentStep == 2 || (Step3Grid != null && Step3Grid.Visibility == Visibility.Visible);

                Brush ApplyBrush(Brush? current, Color col) =>
                    animate ? ShellHelper.AnimateOrCreateBrush(current, col, animDuration) : ShellHelper.UpdateOrCreateBrush(current, col);

                if (updateStep1) {
                    // Step 1 - Folder 1
                    if (Step1FrostedTintLayer != null) {
                        Step1FrostedTintLayer.Background = ApplyBrush(Step1FrostedTintLayer.Background, frostedCol);
                        Step1FrostedTintLayer.Opacity = clampedOpacity;
                    }
                    if (Step1TintLayer != null) {
                        Step1TintLayer.Background = ApplyBrush(Step1TintLayer.Background, tintCol);
                        Step1TintLayer.Opacity = tintOpacity;
                    }
                    if (Step1FolderBlurFx != null) Step1FolderBlurFx.Radius = _folderBlurRadius;
                    if (Step1BlurredWallpaperImage != null) Step1BlurredWallpaperImage.Visibility = blurVis;
                    if (Step1DesktopIconShapePath != null) {
                        Step1DesktopIconShapePath.Fill = ApplyBrush(Step1DesktopIconShapePath.Fill, folderCol);
                        ApplyShapeToDesktopIconPlate(Step1DesktopIconShapePath, null, _selectedShape);
                    }

                    // Step 1 - Folder 2
                    if (Step1FrostedTintLayer2 != null) {
                        Step1FrostedTintLayer2.Background = ApplyBrush(Step1FrostedTintLayer2.Background, frostedCol);
                        Step1FrostedTintLayer2.Opacity = clampedOpacity;
                    }
                    if (Step1TintLayer2 != null) {
                        Step1TintLayer2.Background = ApplyBrush(Step1TintLayer2.Background, tintCol);
                        Step1TintLayer2.Opacity = tintOpacity;
                    }
                    if (Step1FolderBlurFx2 != null) Step1FolderBlurFx2.Radius = _folderBlurRadius;
                    if (Step1BlurredWallpaperImage2 != null) Step1BlurredWallpaperImage2.Visibility = blurVis;
                    if (Step1DesktopIcon2ShapePath != null) {
                        Step1DesktopIcon2ShapePath.Fill = ApplyBrush(Step1DesktopIcon2ShapePath.Fill, folderCol);
                        ApplyShapeToDesktopIconPlate(Step1DesktopIcon2ShapePath, null, _selectedShape);
                    }
                }

                if (updateStep2) {
                    // Step 2 - Folder 1
                    if (Step2FrostedTintLayer != null) {
                        Step2FrostedTintLayer.Background = ApplyBrush(Step2FrostedTintLayer.Background, frostedCol);
                        Step2FrostedTintLayer.Opacity = clampedOpacity;
                    }
                    if (Step2TintLayer != null) {
                        Step2TintLayer.Background = ApplyBrush(Step2TintLayer.Background, tintCol);
                        Step2TintLayer.Opacity = tintOpacity;
                    }
                    if (Step2FolderBlurFx != null) Step2FolderBlurFx.Radius = _folderBlurRadius;
                    if (Step2BlurredWallpaperImage != null) Step2BlurredWallpaperImage.Visibility = blurVis;
                    if (Step2DesktopIconShapePath != null) {
                        Step2DesktopIconShapePath.Fill = ApplyBrush(Step2DesktopIconShapePath.Fill, folderCol);
                        ApplyShapeToDesktopIconPlate(Step2DesktopIconShapePath, null, _selectedShape);
                    }

                    // Step 2 - Folder 2
                    if (Step2FrostedTintLayer2 != null) {
                        Step2FrostedTintLayer2.Background = ApplyBrush(Step2FrostedTintLayer2.Background, frostedCol);
                        Step2FrostedTintLayer2.Opacity = clampedOpacity;
                    }
                    if (Step2TintLayer2 != null) {
                        Step2TintLayer2.Background = ApplyBrush(Step2TintLayer2.Background, tintCol);
                        Step2TintLayer2.Opacity = tintOpacity;
                    }
                    if (Step2FolderBlurFx2 != null) Step2FolderBlurFx2.Radius = _folderBlurRadius;
                    if (Step2BlurredWallpaperImage2 != null) Step2BlurredWallpaperImage2.Visibility = blurVis;
                    if (Step2DesktopIcon2ShapePath != null) {
                        Step2DesktopIcon2ShapePath.Fill = ApplyBrush(Step2DesktopIcon2ShapePath.Fill, folderCol);
                        ApplyShapeToDesktopIconPlate(Step2DesktopIcon2ShapePath, null, _selectedShape);
                    }
                }

                if (updateStep3) {
                    // Step 3 - Folder 1
                    if (Step3FrostedTintLayer != null) {
                        Step3FrostedTintLayer.Background = ApplyBrush(Step3FrostedTintLayer.Background, frostedCol);
                        Step3FrostedTintLayer.Opacity = clampedOpacity;
                    }
                    if (Step3TintLayer != null) {
                        Step3TintLayer.Background = ApplyBrush(Step3TintLayer.Background, tintCol);
                        Step3TintLayer.Opacity = tintOpacity;
                    }
                    if (Step3FolderBlurFx != null) Step3FolderBlurFx.Radius = _folderBlurRadius;
                    if (Step3BlurredWallpaperImage != null) Step3BlurredWallpaperImage.Visibility = blurVis;
                    if (Step3DesktopIconShapePath != null) {
                        Step3DesktopIconShapePath.Fill = ApplyBrush(Step3DesktopIconShapePath.Fill, folderCol);
                        ApplyShapeToDesktopIconPlate(Step3DesktopIconShapePath, null, _selectedShape);
                    }

                    // Step 3 - Folder 2
                    if (Step3FrostedTintLayer2 != null) {
                        Step3FrostedTintLayer2.Background = ApplyBrush(Step3FrostedTintLayer2.Background, frostedCol);
                        Step3FrostedTintLayer2.Opacity = clampedOpacity;
                    }
                    if (Step3TintLayer2 != null) {
                        Step3TintLayer2.Background = ApplyBrush(Step3TintLayer2.Background, tintCol);
                        Step3TintLayer2.Opacity = tintOpacity;
                    }
                    if (Step3FolderBlurFx2 != null) Step3FolderBlurFx2.Radius = _folderBlurRadius;
                    if (Step3BlurredWallpaperImage2 != null) Step3BlurredWallpaperImage2.Visibility = blurVis;
                    if (Step3DesktopIcon2ShapePath != null) {
                        Step3DesktopIcon2ShapePath.Fill = ApplyBrush(Step3DesktopIcon2ShapePath.Fill, folderCol);
                        ApplyShapeToDesktopIconPlate(Step3DesktopIcon2ShapePath, null, _selectedShape);
                    }
                }

                UpdateFocusModeVisuals(animate: animate);
                if (updateStep1) {
                    UpdateStep1MoreActionsMenuTheme();
                }
                if (updateStep2) {
                    UpdateStep2PagingModeCardsVisual();
                }

                try {
                    double hue = ShellHelper.GetHueFromHex(_selectedColor);
                    QueueDynamicIconUpdate(hue, isDark);
                } catch { }
            } catch { }
        }

        private void ApplyShapeToPreviewBadge(System.Windows.Shapes.Path? path, Border? badge, string shape) {
            if (path == null) return;
            string geomKey = "GeomSquircle";
            if (string.Equals(shape, "Circle", StringComparison.OrdinalIgnoreCase)) geomKey = "GeomCircle";
            else if (string.Equals(shape, "Clover", StringComparison.OrdinalIgnoreCase)) geomKey = "GeomClover";
            else if (string.Equals(shape, "Scallop", StringComparison.OrdinalIgnoreCase)) geomKey = "GeomScallop";
            else if (string.Equals(shape, "Arch", StringComparison.OrdinalIgnoreCase)) geomKey = "GeomArch";

            if (Resources.Contains(geomKey) && Resources[geomKey] is Geometry g) {
                path.Data = g;
            }
        }

        private void ApplyShapeToDesktopIconPlate(System.Windows.Shapes.Path? path, Border? plate, string shape) {
            if (path == null) return;
            if (_shapeMorphTimer != null && _shapeMorphTimer.IsEnabled) return;
            string geomKey = "GeomSquircle";
            if (string.Equals(shape, "Circle", StringComparison.OrdinalIgnoreCase)) geomKey = "GeomCircle";
            else if (string.Equals(shape, "Clover", StringComparison.OrdinalIgnoreCase)) geomKey = "GeomClover";
            else if (string.Equals(shape, "Scallop", StringComparison.OrdinalIgnoreCase)) geomKey = "GeomScallop";
            else if (string.Equals(shape, "Arch", StringComparison.OrdinalIgnoreCase)) geomKey = "GeomArch";

            if (Resources.Contains(geomKey) && Resources[geomKey] is Geometry g) {
                path.Data = g;
            }
        }

        private void GoToStep(int targetStep, bool animate = true) {
            if (targetStep < 0 || targetStep > 2 || targetStep == _currentStep) return;

            Grid currentGrid = _currentStep switch {
                0 => Step1Grid,
                1 => Step2Grid,
                _ => Step3Grid
            };

            TranslateTransform currentTranslate = _currentStep switch {
                0 => Step1Translate,
                1 => Step2Translate,
                _ => Step3Translate
            };

            Grid targetGrid = targetStep switch {
                0 => Step1Grid,
                1 => Step2Grid,
                _ => Step3Grid
            };

            TranslateTransform targetTranslate = targetStep switch {
                0 => Step1Translate,
                1 => Step2Translate,
                _ => Step3Translate
            };

            int oldStep = _currentStep;
            _currentStep = targetStep;

            if (animate && oldStep == 0 && targetStep == 1) {
                // Step 0 -> Step 1 morph transition:
                // Step 1 controls slide left and fade out
                // Step 1 preview slides to the left into Step 2 preview position
                // Step 2 controls slide in from right
                double shiftX = -(Step1PreviewBorder.ActualWidth > 50 ? Step1PreviewBorder.ActualWidth + 16 : 400.0);
                var duration = TimeSpan.FromMilliseconds(320);
                var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

                Step2Grid.Visibility = Visibility.Visible;
                Step2Grid.Opacity = 1.0;
                Step1Grid.Visibility = Visibility.Visible;
                Step1Grid.Opacity = 1.0;

                // Step 1 Controls slide left
                var ctrlExitAnim = new DoubleAnimation(0, -320, duration) { EasingFunction = ease };
                var ctrlFadeAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(240));
                Step1ControlsTranslate?.BeginAnimation(TranslateTransform.XProperty, ctrlExitAnim);
                Step1ControlsPanel?.BeginAnimation(UIElement.OpacityProperty, ctrlFadeAnim);

                // Step 1 Preview shifts left into Step 2's position
                var p1ShiftAnim = new DoubleAnimation(0, shiftX, duration) { EasingFunction = ease };
                var p1FadeAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(280));
                Step1PreviewTranslate?.BeginAnimation(TranslateTransform.XProperty, p1ShiftAnim);
                Step1PreviewBorder?.BeginAnimation(UIElement.OpacityProperty, p1FadeAnim);

                // Step 2 Preview starts shifted right and slides to 0
                if (Step2PreviewTranslate != null) {
                    Step2PreviewTranslate.X = -shiftX;
                    var p2ShiftAnim = new DoubleAnimation(-shiftX, 0, duration) { EasingFunction = ease };
                    var p2FadeAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(260));
                    Step2PreviewTranslate.BeginAnimation(TranslateTransform.XProperty, p2ShiftAnim);
                    Step2PreviewBorder?.BeginAnimation(UIElement.OpacityProperty, p2FadeAnim);
                }

                // Step 2 Controls slide in from the right
                if (Step2ControlsTranslate != null) {
                    Step2ControlsTranslate.X = 320;
                    var ctrlEnterAnim = new DoubleAnimation(320, 0, duration) { EasingFunction = ease };
                    var ctrlFadeIn = new DoubleAnimation(0.0, 1.0, duration);
                    Step2ControlsTranslate.BeginAnimation(TranslateTransform.XProperty, ctrlEnterAnim);
                    Step2ControlsPanel?.BeginAnimation(UIElement.OpacityProperty, ctrlFadeIn);
                }

                p1ShiftAnim.Completed += (s, e) => {
                    Step1Grid.Visibility = Visibility.Collapsed;
                    if (Step1ControlsTranslate != null) Step1ControlsTranslate.X = 0;
                    if (Step1PreviewTranslate != null) Step1PreviewTranslate.X = 0;
                    if (Step1ControlsPanel != null) Step1ControlsPanel.Opacity = 1.0;
                    if (Step1PreviewBorder != null) Step1PreviewBorder.Opacity = 1.0;
                    UpdateWallpaperCrop();
                };
            } else if (animate && oldStep == 1 && targetStep == 0) {
                // Step 1 -> Step 0 (Back):
                double shiftX = -(Step1PreviewBorder.ActualWidth > 50 ? Step1PreviewBorder.ActualWidth + 16 : 400.0);
                var duration = TimeSpan.FromMilliseconds(320);
                var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

                Step1Grid.Visibility = Visibility.Visible;
                Step1Grid.Opacity = 1.0;
                Step2Grid.Visibility = Visibility.Visible;
                Step2Grid.Opacity = 1.0;

                // Step 2 Controls slide right
                var ctrlExitAnim = new DoubleAnimation(0, 320, duration) { EasingFunction = ease };
                var ctrlFadeAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(240));
                Step2ControlsTranslate?.BeginAnimation(TranslateTransform.XProperty, ctrlExitAnim);
                Step2ControlsPanel?.BeginAnimation(UIElement.OpacityProperty, ctrlFadeAnim);

                // Step 2 Preview shifts right
                var p2ShiftAnim = new DoubleAnimation(0, -shiftX, duration) { EasingFunction = ease };
                var p2FadeAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(280));
                Step2PreviewTranslate?.BeginAnimation(TranslateTransform.XProperty, p2ShiftAnim);
                Step2PreviewBorder?.BeginAnimation(UIElement.OpacityProperty, p2FadeAnim);

                // Step 1 Preview starts shifted left and slides to 0
                if (Step1PreviewTranslate != null) {
                    Step1PreviewTranslate.X = shiftX;
                    var p1ShiftAnim = new DoubleAnimation(shiftX, 0, duration) { EasingFunction = ease };
                    var p1FadeAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(260));
                    Step1PreviewTranslate.BeginAnimation(TranslateTransform.XProperty, p1ShiftAnim);
                    Step1PreviewBorder?.BeginAnimation(UIElement.OpacityProperty, p1FadeAnim);
                }

                // Step 1 Controls slide in from the left
                if (Step1ControlsTranslate != null) {
                    Step1ControlsTranslate.X = -320;
                    var ctrlEnterAnim = new DoubleAnimation(-320, 0, duration) { EasingFunction = ease };
                    var ctrlFadeIn = new DoubleAnimation(0.0, 1.0, duration);
                    Step1ControlsTranslate.BeginAnimation(TranslateTransform.XProperty, ctrlEnterAnim);
                    Step1ControlsPanel?.BeginAnimation(UIElement.OpacityProperty, ctrlFadeIn);
                }

                p2ShiftAnim.Completed += (s, e) => {
                    Step2Grid.Visibility = Visibility.Collapsed;
                    if (Step2ControlsTranslate != null) Step2ControlsTranslate.X = 0;
                    if (Step2PreviewTranslate != null) Step2PreviewTranslate.X = 0;
                    if (Step2ControlsPanel != null) Step2ControlsPanel.Opacity = 1.0;
                    if (Step2PreviewBorder != null) Step2PreviewBorder.Opacity = 1.0;
                    UpdateWallpaperCrop();
                };
            } else if (animate) {
                bool movingForward = targetStep > oldStep;
                double currentExitX = movingForward ? -320 : 320;
                double targetStartX = movingForward ? 320 : -320;

                var currentAnim = new DoubleAnimation(0, currentExitX, TimeSpan.FromMilliseconds(260)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                };
                var currentFade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200));
                currentAnim.Completed += (s, e) => {
                    currentGrid.Visibility = Visibility.Collapsed;
                    currentTranslate.X = 0;
                };
                currentTranslate.BeginAnimation(TranslateTransform.XProperty, currentAnim);
                currentGrid.BeginAnimation(UIElement.OpacityProperty, currentFade);

                targetGrid.Visibility = Visibility.Visible;
                targetGrid.Opacity = 0.0;
                targetTranslate.X = targetStartX;
                var targetAnim = new DoubleAnimation(targetStartX, 0, TimeSpan.FromMilliseconds(280)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var targetFade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(240));
                targetAnim.Completed += (s, e) => UpdateWallpaperCrop();
                targetTranslate.BeginAnimation(TranslateTransform.XProperty, targetAnim);
                targetGrid.BeginAnimation(UIElement.OpacityProperty, targetFade);
            } else {
                currentGrid.Visibility = Visibility.Collapsed;
                targetGrid.Visibility = Visibility.Visible;
                targetGrid.Opacity = 1.0;
                targetTranslate.X = 0;
            }

            UpdateStepDots();
            UpdateStepButtons();
            UpdateLivePreview();
            UpdateWallpaperCrop();
            UpdateFocusModeVisuals(animate: false);
        }

        private void UpdateStepDots() {
            var activeBrush = Resources["M3PrimaryBrush"] as Brush ?? Brushes.DarkKhaki;
            var inactiveBrush = Resources["M3TrackInactiveBrush"] as Brush ?? Brushes.LightGray;

            Border[] dots = { StepDot0, StepDot1, StepDot2 };
            for (int i = 0; i < dots.Length; i++) {
                if (dots[i] == null) continue;
                bool isActive = (i == _currentStep);
                dots[i].Background = isActive ? activeBrush : inactiveBrush;

                var anim = new DoubleAnimation {
                    To = isActive ? 26.0 : 8.0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                dots[i].BeginAnimation(FrameworkElement.WidthProperty, anim);
            }

            var loc = LocalizationService.Instance;
            StepIndicatorText.Text = $"{loc["Step"]} {_currentStep + 1} / 3";
        }

        private void UpdateStepButtons() {
            var loc = LocalizationService.Instance;
            BackBtn.Visibility = (_currentStep > 0) ? Visibility.Visible : Visibility.Collapsed;
            SkipBtn.Visibility = (_currentStep < 2) ? Visibility.Visible : Visibility.Collapsed;
            if (CloseWizardBtn != null) {
                CloseWizardBtn.Visibility = (_currentStep == 2) ? Visibility.Visible : Visibility.Collapsed;
                CloseWizardBtn.Content = loc["Close"] ?? (loc.CurrentLanguage == "hy" ? "Փակել" : "Close");
            }
            NextBtn.Content = (_currentStep == 2) ? (loc["GetStarted"] ?? loc["Finish"] ?? "Սկսել") : (loc["Next"] ?? "Next");
        }

        private void SaveConfiguredSettings() {
            var s = SettingsService.Instance.Current;
            s.ThemeMode = _selectedThemeMode;
            s.IconShape = _selectedShape;
            s.ThemeColor = _selectedColor;
            s.FolderOpacity = _folderOpacity;
            s.FolderBlurEnabled = _folderBlurEnabled;
            s.FolderBlurRadius = _folderBlurRadius;

            s.PagingMode = _pagingMode;
            s.GridColumns = _gridColumns;
            s.GridRows = _gridRows;
            s.SliderArrowsEnabled = _sliderArrows;
            s.SliderDragEnabled = _sliderDrag;
            s.FocusModeEnabled = _focusMode;
            s.FocusDimmingOpacity = _focusDimming;
            s.FocusBlurEnabled = _focusBlurEnabled;
            s.FocusBlurRadius = _focusBlurRadius;

            SettingsService.Instance.SaveSettings(s);
            PanakManager.Instance.UpdateOpenPopupSettings();
            PanakManager.Instance.UpdateOpenPopupThemeMode(_selectedThemeMode);
        }

        private bool _isStep1MoreMenuOpen = false;
        private bool _isStep1Pinned = false;

        private void Step1PreviewMoreBtn_Click(object sender, RoutedEventArgs e) {
            if (Step1MoreActionsMenuBorder == null) return;
            if (_isStep1MoreMenuOpen) {
                CloseStep1MoreActionsMenuAnimated();
            } else {
                OpenStep1MoreActionsMenuAnimated();
            }
            e.Handled = true;
        }

        private void OpenStep1MoreActionsMenuAnimated() {
            if (Step1MoreActionsMenuBorder == null) return;
            UpdateStep1MoreActionsMenuTheme();
            _isStep1MoreMenuOpen = true;
            Step1MoreActionsMenuBorder.Visibility = Visibility.Visible;
            Step1MoreActionsMenuBorder.IsHitTestVisible = true;

            if (Step1MainContentGrid != null && _folderBlurEnabled) {
                var blur = new BlurEffect { Radius = 0, RenderingBias = RenderingBias.Performance };
                Step1MainContentGrid.Effect = blur;
                var blurAnim = new DoubleAnimation(0.0, 10.0, TimeSpan.FromMilliseconds(200)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                blur.BeginAnimation(BlurEffect.RadiusProperty, blurAnim);
            }

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var opacityAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease };
            var scaleAnim = new DoubleAnimation(0.85, 1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease };

            Step1MoreActionsMenuBorder.BeginAnimation(OpacityProperty, opacityAnim);
            Step1MoreActionsMenuScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            Step1MoreActionsMenuScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        private void CloseStep1MoreActionsMenuAnimated() {
            if (Step1MoreActionsMenuBorder == null || !_isStep1MoreMenuOpen) {
                if (Step1MoreActionsMenuBorder != null) {
                    Step1MoreActionsMenuBorder.Visibility = Visibility.Collapsed;
                    Step1MoreActionsMenuBorder.IsHitTestVisible = false;
                }
                _isStep1MoreMenuOpen = false;
                if (Step1MainContentGrid?.Effect != null) {
                    Step1MainContentGrid.Effect = null;
                }
                return;
            }
            _isStep1MoreMenuOpen = false;
            Step1MoreActionsMenuBorder.IsHitTestVisible = false;

            if (Step1MainContentGrid?.Effect is BlurEffect blur) {
                var blurAnim = new DoubleAnimation(blur.Radius, 0.0, TimeSpan.FromMilliseconds(160)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                blurAnim.Completed += (s, e) => {
                    if (!_isStep1MoreMenuOpen && Step1MainContentGrid != null) {
                        Step1MainContentGrid.Effect = null;
                    }
                };
                blur.BeginAnimation(BlurEffect.RadiusProperty, blurAnim);
            }

            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            var opacityAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease };
            var scaleAnim = new DoubleAnimation(1.0, 0.85, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease };

            opacityAnim.Completed += (s, e) => {
                if (!_isStep1MoreMenuOpen && Step1MoreActionsMenuBorder != null) {
                    Step1MoreActionsMenuBorder.Visibility = Visibility.Collapsed;
                }
            };

            Step1MoreActionsMenuBorder.BeginAnimation(OpacityProperty, opacityAnim);
            Step1MoreActionsMenuScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            Step1MoreActionsMenuScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        private void CloseStep1MoreActionsMenuImmediate() {
            if (Step1MoreActionsMenuBorder == null) return;
            _isStep1MoreMenuOpen = false;
            if (Step1MainContentGrid?.Effect != null) {
                Step1MainContentGrid.Effect = null;
            }
            Step1MoreActionsMenuBorder.BeginAnimation(OpacityProperty, null);
            Step1MoreActionsMenuScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            Step1MoreActionsMenuScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            Step1MoreActionsMenuBorder.Opacity = 0;
            Step1MoreActionsMenuScale.ScaleX = 0.85;
            Step1MoreActionsMenuScale.ScaleY = 0.85;
            Step1MoreActionsMenuBorder.Visibility = Visibility.Collapsed;
            Step1MoreActionsMenuBorder.IsHitTestVisible = false;
        }

        private void Step1PreviewPinBtn_Click(object sender, RoutedEventArgs e) {
            _isStep1Pinned = !_isStep1Pinned;
            if (Step1PreviewPinBtn != null) {
                Step1PreviewPinBtn.Visibility = _isStep1Pinned ? Visibility.Visible : Visibility.Collapsed;
            }
            UpdateStep1MoreActionsMenuTheme();
        }

        private void Step1MenuPin_Click(object sender, MouseButtonEventArgs e) {
            e.Handled = true;
            CloseStep1MoreActionsMenuAnimated();
            Step1PreviewPinBtn_Click(sender, e);
        }

        private void Step1MenuEdit_Click(object sender, MouseButtonEventArgs e) {
            e.Handled = true;
            CloseStep1MoreActionsMenuAnimated();
            if (Step1PreviewFolderTitleEdit != null && Step1PreviewFolderTitle != null) {
                Step1PreviewFolderTitle.Visibility = Visibility.Collapsed;
                Step1PreviewFolderTitleEdit.Visibility = Visibility.Visible;
                Step1PreviewFolderTitleEdit.Text = Step1PreviewFolderTitle.Text;
                Step1PreviewFolderTitleEdit.Focus();
                Step1PreviewFolderTitleEdit.SelectAll();
            }
        }

        private void Step1PreviewFolderTitleEdit_LostFocus(object sender, RoutedEventArgs e) {
            SaveStep1InlineRename();
        }

        private void Step1PreviewFolderTitleEdit_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                SaveStep1InlineRename();
            } else if (e.Key == Key.Escape) {
                if (Step1PreviewFolderTitleEdit != null && Step1PreviewFolderTitle != null) {
                    Step1PreviewFolderTitleEdit.Visibility = Visibility.Collapsed;
                    Step1PreviewFolderTitle.Visibility = Visibility.Visible;
                }
            }
        }

        private void SaveStep1InlineRename() {
            if (Step1PreviewFolderTitleEdit != null && Step1PreviewFolderTitle != null) {
                if (!string.IsNullOrWhiteSpace(Step1PreviewFolderTitleEdit.Text)) {
                    Step1PreviewFolderTitle.Text = Step1PreviewFolderTitleEdit.Text.Trim();
                }
                Step1PreviewFolderTitleEdit.Visibility = Visibility.Collapsed;
                Step1PreviewFolderTitle.Visibility = Visibility.Visible;
            }
        }

        private void Step1MenuExplorer_Click(object sender, MouseButtonEventArgs e) {
            e.Handled = true;
            CloseStep1MoreActionsMenuAnimated();
            try {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                Process.Start(new ProcessStartInfo {
                    FileName = desktopPath,
                    UseShellExecute = true
                });
            } catch { }
        }

        public void UpdateStep1MoreActionsMenuTheme() {
            if (Step1MoreActionsMenuBorder == null) return;
            try {
                var loc = LocalizationService.Instance;
                if (Step1MenuPinText != null) Step1MenuPinText.Text = _isStep1Pinned ? loc["Unpin"] : loc["Pin"];
                if (Step1MenuEditText != null) Step1MenuEditText.Text = loc["Rename"];
                if (Step1MenuExplorerText != null) Step1MenuExplorerText.Text = loc["OpenInExplorer"];

                Color baseCol;
                try {
                    baseCol = (Color)ColorConverter.ConvertFromString(_selectedColor);
                } catch {
                    baseCol = Color.FromRgb(0xFC, 0xDD, 0xC5);
                }
                double luminance = (0.299 * baseCol.R + 0.587 * baseCol.G + 0.114 * baseCol.B) / 255.0;
                bool isLight = luminance > 0.55;

                Step1MoreActionsMenuBorder.BorderThickness = new Thickness(0);
                Step1MoreActionsMenuBorder.BorderBrush = Brushes.Transparent;

                if (!isLight) {
                    byte alpha = _folderBlurEnabled ? (byte)185 : (byte)235;
                    var menuBg = Color.FromArgb(alpha,
                        (byte)Math.Clamp(baseCol.R * 0.22 + 34 * 0.78, 0, 255),
                        (byte)Math.Clamp(baseCol.G * 0.22 + 34 * 0.78, 0, 255),
                        (byte)Math.Clamp(baseCol.B * 0.22 + 42 * 0.78, 0, 255));
                    Step1MoreActionsMenuBorder.Background = new SolidColorBrush(menuBg);
                    var lightFg = Brushes.White;
                    if (Step1MenuPinText != null) Step1MenuPinText.Foreground = lightFg;
                    if (Step1MenuEditText != null) Step1MenuEditText.Foreground = lightFg;
                    if (Step1MenuExplorerText != null) Step1MenuExplorerText.Foreground = lightFg;
                    if (Step1MenuPinIconPath != null) Step1MenuPinIconPath.Fill = _isStep1Pinned ? (Brush)FindResource("M3PrimaryBrush") : lightFg;
                    if (Step1MenuEditIconPath != null) Step1MenuEditIconPath.Fill = lightFg;
                    if (Step1MenuExplorerIconPath != null) Step1MenuExplorerIconPath.Fill = lightFg;
                } else {
                    byte alpha = _folderBlurEnabled ? (byte)190 : (byte)245;
                    var menuBg = Color.FromArgb(alpha,
                        (byte)Math.Clamp(baseCol.R * 0.18 + 248 * 0.82, 0, 255),
                        (byte)Math.Clamp(baseCol.G * 0.18 + 250 * 0.82, 0, 255),
                        (byte)Math.Clamp(baseCol.B * 0.18 + 252 * 0.82, 0, 255));
                    Step1MoreActionsMenuBorder.Background = new SolidColorBrush(menuBg);
                    var darkFg = new SolidColorBrush(Color.FromRgb(28, 27, 31));
                    if (Step1MenuPinText != null) Step1MenuPinText.Foreground = darkFg;
                    if (Step1MenuEditText != null) Step1MenuEditText.Foreground = darkFg;
                    if (Step1MenuExplorerText != null) Step1MenuExplorerText.Foreground = darkFg;
                    if (Step1MenuPinIconPath != null) Step1MenuPinIconPath.Fill = _isStep1Pinned ? (Brush)FindResource("M3PrimaryBrush") : darkFg;
                    if (Step1MenuEditIconPath != null) Step1MenuEditIconPath.Fill = darkFg;
                    if (Step1MenuExplorerIconPath != null) Step1MenuExplorerIconPath.Fill = darkFg;
                }
            } catch { }
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            if (_isStep1MoreMenuOpen && Step1MoreActionsMenuBorder != null) {
                var src = e.OriginalSource as DependencyObject;
                bool isInsideMenu = IsDescendantOf(src, Step1MoreActionsMenuBorder);
                bool isMoreBtn = IsDescendantOf(src, Step1PreviewMoreBtn);
                if (!isInsideMenu && !isMoreBtn) {
                    CloseStep1MoreActionsMenuAnimated();
                }
            }
        }

        private static bool IsDescendantOf(DependencyObject? node, DependencyObject? parent) {
            if (node == null || parent == null) return false;
            while (node != null) {
                if (node == parent) return true;
                node = VisualTreeHelper.GetParent(node);
            }
            return false;
        }

        private bool _isPreviewPinned = false;
        private void PreviewPinBtn_Click(object sender, RoutedEventArgs e) {
            _isPreviewPinned = !_isPreviewPinned;
            var activeBrush = Resources["M3PrimaryBrush"] as Brush ?? Brushes.SkyBlue;
            var normalBrush = Resources["PopupHeaderBtnBg"] as Brush ?? new SolidColorBrush(Color.FromArgb(37, 255, 255, 255));
            if (sender is Button btn) {
                btn.Background = _isPreviewPinned ? activeBrush : normalBrush;
            } else if (Step1PreviewPinBtn != null) {
                Step1PreviewPinBtn.Background = _isPreviewPinned ? activeBrush : normalBrush;
            }
        }

        private void Step1ColorIndicator_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                var s = SettingsService.Instance.Current;
                if (!s.CustomPaletteColors.Contains(_selectedColor, StringComparer.OrdinalIgnoreCase)) {
                    s.CustomPaletteColors.Add(_selectedColor);
                    SettingsService.Instance.SaveSettings(s);
                    RebuildStep1CustomSwatchesUI();
                    var anim = new DoubleAnimation(1.3, 1.0, TimeSpan.FromMilliseconds(200));
                    var scale = new ScaleTransform(1, 1);
                    Step1ColorIndicator.RenderTransformOrigin = new Point(0.5, 0.5);
                    Step1ColorIndicator.RenderTransform = scale;
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                }
            }
        }

        private void RebuildStep1CustomSwatchesUI() {
            if (Step1CustomSwatchesPanel == null) return;
            Step1CustomSwatchesPanel.Children.Clear();
            var s = SettingsService.Instance.Current;
            if (s.CustomPaletteColors == null || s.CustomPaletteColors.Count == 0) return;

            foreach (var hex in s.CustomPaletteColors) {
                string colHex = hex;
                var grid = new Grid {
                    Width = 28,
                    Height = 28,
                    Margin = new Thickness(3, 0, 3, 0),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };

                var rb = new RadioButton {
                    GroupName = "WizardColorGroup",
                    Style = (Style)FindResource("ColorSwatchStyle"),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colHex)),
                    Tag = colHex,
                    ToolTip = colHex,
                    Margin = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                if (string.Equals(_selectedColor, colHex, StringComparison.OrdinalIgnoreCase)) {
                    rb.IsChecked = true;
                }
                rb.Checked += (snd, ev) => {
                    if (_isInitializing) return;
                    _selectedColor = colHex;
                    double hue = ShellHelper.GetHueFromHex(colHex);
                    ApplyMaterialYouPalette(hue, animate: true);
                    _isInitializing = true;
                    try {
                        Step1HueSlider.Value = hue;
                    } finally {
                        _isInitializing = false;
                    }
                    UpdateLivePreview(animate: true);
                };
                grid.Children.Add(rb);

                var deleteBadge = new Border {
                    Width = 12,
                    Height = 12,
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromArgb(210, 30, 30, 30)),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 0, 0, 0),
                    Visibility = Visibility.Collapsed,
                    Cursor = Cursors.Hand,
                    ToolTip = LocalizationService.Instance["Delete"] ?? "Delete"
                };

                var crossPath = new System.Windows.Shapes.Path {
                    Data = Geometry.Parse("M 3,3 L 9,9 M 9,3 L 3,9"),
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                deleteBadge.Child = crossPath;

                grid.MouseEnter += (snd, ev) => deleteBadge.Visibility = Visibility.Visible;
                grid.MouseLeave += (snd, ev) => deleteBadge.Visibility = Visibility.Collapsed;
                deleteBadge.MouseEnter += (snd, ev) => deleteBadge.Background = new SolidColorBrush(Color.FromRgb(229, 57, 53));
                deleteBadge.MouseLeave += (snd, ev) => deleteBadge.Background = new SolidColorBrush(Color.FromArgb(210, 30, 30, 30));

                deleteBadge.PreviewMouseLeftButtonDown += (snd, ev) => {
                    ev.Handled = true;
                    var curSettings = SettingsService.Instance.Current;
                    curSettings.CustomPaletteColors.Remove(colHex);
                    SettingsService.Instance.SaveSettings(curSettings);
                    RebuildStep1CustomSwatchesUI();
                    if (string.Equals(_selectedColor, colHex, StringComparison.OrdinalIgnoreCase)) {
                        string defaultPastel = ShellHelper.DefaultPastelColorHex;
                        _selectedColor = defaultPastel;
                        double hue = ShellHelper.GetHueFromHex(defaultPastel);
                        ApplyMaterialYouPalette(hue, animate: true);
                        _isInitializing = true;
                        try {
                            Step1HueSlider.Value = hue;
                        } finally {
                            _isInitializing = false;
                        }
                        UpdateLivePreview(animate: true);
                    }
                };

                grid.Children.Add(deleteBadge);
                Step1CustomSwatchesPanel.Children.Add(grid);
            }
        }

        // Event Handlers for Step 1
        private void WizardTheme_Click(object sender, RoutedEventArgs e) {
            if (_isInitializing) return;
            if (sender is RadioButton rb && rb.Tag is string mode) {
                _selectedThemeMode = mode;
                int tIdx = mode == "Light" ? 0 : (mode == "Dark" ? 1 : 2);
                AnimateThemePill(tIdx, immediate: false);

                double hue = ShellHelper.GetHueFromHex(_selectedColor);
                ApplyMaterialYouPalette(hue, animate: true);

                UpdateLivePreview(animate: true);
            }
        }

        private void Step1ThemeSegmentGrid_SizeChanged(object sender, SizeChangedEventArgs e) {
            int idx = _selectedThemeMode == "Light" ? 0 : (_selectedThemeMode == "Dark" ? 1 : 2);
            AnimateThemePill(idx, immediate: true);
        }

        private void AnimateThemePill(int index, bool immediate = false) {
            if (Step1ThemeIndicatorPill == null || Step1ThemePillTranslate == null || Step1ThemeSegmentGrid == null) return;
            double totalW = Step1ThemeSegmentGrid.ActualWidth;
            if (totalW <= 0) totalW = 194;
            double pillW = totalW / 3.0;
            Step1ThemeIndicatorPill.Width = pillW;
            double targetX = index * pillW;

            if (immediate) {
                Step1ThemePillTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                Step1ThemePillTranslate.X = targetX;
            } else {
                var anim = new DoubleAnimation(Step1ThemePillTranslate.X, targetX, TimeSpan.FromMilliseconds(260)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Step1ThemePillTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
            }
        }

        private void ShapeRadio_Click(object sender, RoutedEventArgs e) {
            if (_isInitializing) return;
            if (sender == ShapeCircleRadio) _selectedShape = "Circle";
            else if (sender == ShapeCloverRadio) _selectedShape = "Clover";
            else if (sender == ShapeScallopRadio) _selectedShape = "Scallop";
            else if (sender == ShapeArchRadio) _selectedShape = "Arch";
            else _selectedShape = "Squircle";

            int sIdx = GetShapeIndex(_selectedShape);
            AnimateStep1ShapePill(sIdx, immediate: false);
            MorphPreviewDesktopIconShape(_selectedShape);
        }

        private void AnimateStep1ShapePill(int index, bool immediate = false) {
            if (Step1ShapePill == null || Step1ShapePillTranslate == null) return;
            double targetX = index * 40.0 + 2.0;
            if (immediate) {
                Step1ShapePillTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                Step1ShapePillTranslate.X = targetX;
            } else {
                var anim = new DoubleAnimation(Step1ShapePillTranslate.X, targetX, TimeSpan.FromMilliseconds(260)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Step1ShapePillTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
            }
        }

        private static int GetShapeIndex(string shape) => shape switch {
            "Circle" => 0,
            "Clover" or "Star" => 2,
            "Scallop" => 3,
            "Arch" or "Teardrop" => 4,
            _ => 1
        };

        // --- Smooth Shape Morphing Engine ---
        private System.Windows.Point[] GetOrCreateShapePoints(string shapeName) {
            if (_cachedShapePoints.TryGetValue(shapeName, out var pts)) return pts;

            string key = shapeName switch {
                "Circle" => "GeomCircle",
                "Clover" or "Star" => "GeomStar",
                "Scallop" => "GeomScallop",
                "Arch" => "GeomArch",
                _ => "GeomSquircle"
            };

            Geometry? rawGeom = TryFindResource(key) as Geometry;
            if (rawGeom == null) {
                pts = GenerateRadialCirclePoints(MORPH_POINT_COUNT);
                _cachedShapePoints[shapeName] = pts;
                return pts;
            }

            pts = SampleRadialGeometryPoints(rawGeom, MORPH_POINT_COUNT);
            _cachedShapePoints[shapeName] = pts;
            return pts;
        }

        private static System.Windows.Point[] GenerateRadialCirclePoints(int count) {
            var pts = new System.Windows.Point[count];
            double cx = 12.0, cy = 12.0, r = 11.0;
            for (int i = 0; i < count; i++) {
                double theta = (2.0 * Math.PI * i) / count;
                pts[i] = new System.Windows.Point(cx + r * Math.Sin(theta), cy - r * Math.Cos(theta));
            }
            return pts;
        }

        private static System.Windows.Point[] SampleRadialGeometryPoints(Geometry geom, int count) {
            var flattened = geom.GetFlattenedPathGeometry(0.08, ToleranceType.Absolute);
            var rect = flattened.Bounds;
            double bw = rect.Width > 0 ? rect.Width : 22.0;
            double bh = rect.Height > 0 ? rect.Height : 22.0;

            var poly = new System.Collections.Generic.List<System.Windows.Point>();
            foreach (var fig in flattened.Figures) {
                poly.Add(NormalizePoint(fig.StartPoint, rect, bw, bh));
                foreach (var seg in fig.Segments) {
                    if (seg is PolyLineSegment pls) {
                        foreach (var p in pls.Points) poly.Add(NormalizePoint(p, rect, bw, bh));
                    } else if (seg is LineSegment ls) {
                        poly.Add(NormalizePoint(ls.Point, rect, bw, bh));
                    }
                }
            }

            if (poly.Count < 3) return GenerateRadialCirclePoints(count);

            var result = new System.Windows.Point[count];
            var center = new System.Windows.Point(12.0, 12.0);

            for (int i = 0; i < count; i++) {
                double theta = (2.0 * Math.PI * i) / count;
                double dx = Math.Sin(theta);
                double dy = -Math.Cos(theta);

                double bestDist = double.MaxValue;
                System.Windows.Point bestPt = new System.Windows.Point(12.0 + 11.0 * dx, 12.0 + 11.0 * dy);

                for (int j = 0; j < poly.Count; j++) {
                    var p1 = poly[j];
                    var p2 = poly[(j + 1) % poly.Count];
                    if (RaySegmentIntersect(center, dx, dy, p1, p2, out double dist, out var hitPt)) {
                        if (dist > 0.05 && dist < bestDist) {
                            bestDist = dist;
                            bestPt = hitPt;
                        }
                    }
                }
                result[i] = bestPt;
            }
            return result;
        }

        private static System.Windows.Point NormalizePoint(System.Windows.Point p, Rect bounds, double bw, double bh) {
            double u = (p.X - bounds.X) / bw;
            double v = (p.Y - bounds.Y) / bh;
            return new System.Windows.Point(1.0 + 22.0 * Math.Clamp(u, 0.0, 1.0), 1.0 + 22.0 * Math.Clamp(v, 0.0, 1.0));
        }

        private static bool RaySegmentIntersect(System.Windows.Point origin, double dx, double dy, System.Windows.Point a, System.Windows.Point b, out double dist, out System.Windows.Point hit) {
            dist = 0;
            hit = default;
            double vx = b.X - a.X;
            double vy = b.Y - a.Y;
            double det = dy * vx - dx * vy;
            if (Math.Abs(det) < 1e-7) return false;

            double rx = a.X - origin.X;
            double ry = a.Y - origin.Y;

            double r = (ry * vx - rx * vy) / det;
            double s = (dx * ry - dy * rx) / det;

            if (r > 0 && s >= 0.0 && s <= 1.0) {
                dist = r;
                hit = new System.Windows.Point(origin.X + r * dx, origin.Y + r * dy);
                return true;
            }
            return false;
        }

        private void MorphPreviewDesktopIconShape(string targetShape) {
            if (string.Equals(_currentDisplayedShape, targetShape, StringComparison.OrdinalIgnoreCase)) return;

            var targetPts = GetOrCreateShapePoints(targetShape);
            var startPts = _morphStartPoints != null ? _morphStartPoints : GetOrCreateShapePoints(_currentDisplayedShape);

            _morphStartPoints = startPts;
            _morphTargetPoints = targetPts;
            _shapeMorphStartTime = DateTime.UtcNow;
            _currentDisplayedShape = targetShape;

            if (_shapeMorphTimer == null) {
                _shapeMorphTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                _shapeMorphTimer.Tick += OnShapeMorphTick;
            }
            _shapeMorphTimer.Start();
        }

        private void OnShapeMorphTick(object? sender, EventArgs e) {
            if (_morphStartPoints == null || _morphTargetPoints == null) {
                _shapeMorphTimer?.Stop();
                return;
            }

            double elapsedMs = (DateTime.UtcNow - _shapeMorphStartTime).TotalMilliseconds;
            double progress = Math.Clamp(elapsedMs / 320.0, 0.0, 1.0);
            double eased = 1.0 - Math.Pow(1.0 - progress, 3.0);

            var currentPts = new System.Windows.Point[MORPH_POINT_COUNT];
            for (int i = 0; i < MORPH_POINT_COUNT; i++) {
                double x = (1.0 - eased) * _morphStartPoints[i].X + eased * _morphTargetPoints[i].X;
                double y = (1.0 - eased) * _morphStartPoints[i].Y + eased * _morphTargetPoints[i].Y;
                currentPts[i] = new System.Windows.Point(x, y);
            }

            var sg = new StreamGeometry();
            using (var ctx = sg.Open()) {
                ctx.BeginFigure(currentPts[0], isFilled: true, isClosed: true);
                for (int i = 1; i < MORPH_POINT_COUNT; i++) {
                    ctx.LineTo(currentPts[i], isStroked: true, isSmoothJoin: false);
                }
            }
            sg.Freeze();

            void SetPathData(System.Windows.Shapes.Path? p) {
                if (p != null) p.Data = sg;
            }

            SetPathData(Step1DesktopIconShapePath);
            SetPathData(Step1DesktopIcon2ShapePath);
            SetPathData(Step2DesktopIconShapePath);
            SetPathData(Step2DesktopIcon2ShapePath);
            SetPathData(Step3DesktopIconShapePath);
            SetPathData(Step3DesktopIcon2ShapePath);

            if (progress >= 1.0) {
                _shapeMorphTimer?.Stop();
                _morphStartPoints = _morphTargetPoints;

                string key = _currentDisplayedShape switch {
                    "Circle" => "GeomCircle",
                    "Clover" or "Star" => "GeomStar",
                    "Scallop" => "GeomScallop",
                    "Arch" => "GeomArch",
                    _ => "GeomSquircle"
                };
                if (TryFindResource(key) is Geometry finalGeom) {
                    void SetFinalData(System.Windows.Shapes.Path? p) {
                        if (p != null) p.Data = finalGeom;
                    }
                    SetFinalData(Step1DesktopIconShapePath);
                    SetFinalData(Step1DesktopIcon2ShapePath);
                    SetFinalData(Step2DesktopIconShapePath);
                    SetFinalData(Step2DesktopIcon2ShapePath);
                    SetFinalData(Step3DesktopIconShapePath);
                    SetFinalData(Step3DesktopIcon2ShapePath);
                }
            }
        }

        private void WizardColorSwatch_Checked(object sender, RoutedEventArgs e) {
            if (_isInitializing) return;
            if (sender is RadioButton rb && rb.Tag is string hex) {
                _selectedColor = hex;
                double hue = ShellHelper.GetHueFromHex(hex);
                ApplyMaterialYouPalette(hue, animate: true);
                _isInitializing = true;
                try {
                    Step1HueSlider.Value = hue;
                } finally {
                    _isInitializing = false;
                }
                UpdateLivePreview(animate: true);
            }
        }

        private long _lastStep1HueSliderApplyTicks = 0;
        private DispatcherTimer? _step1HueSliderThrottleTimer;
        private double _pendingStep1HueValue = -1;

        private void Step1HueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (_isInitializing) return;
            _pendingStep1HueValue = e.NewValue;
            long now = DateTime.UtcNow.Ticks;
            if ((now - _lastStep1HueSliderApplyTicks) >= TimeSpan.FromMilliseconds(15).Ticks) {
                _step1HueSliderThrottleTimer?.Stop();
                ApplyStep1HueSliderChange(e.NewValue);
            } else {
                if (_step1HueSliderThrottleTimer == null) {
                    _step1HueSliderThrottleTimer = new DispatcherTimer(DispatcherPriority.Render) {
                        Interval = TimeSpan.FromMilliseconds(15)
                    };
                    _step1HueSliderThrottleTimer.Tick += (s, args) => {
                        _step1HueSliderThrottleTimer.Stop();
                        if (_pendingStep1HueValue >= 0) {
                            ApplyStep1HueSliderChange(_pendingStep1HueValue);
                        }
                    };
                }
                if (!_step1HueSliderThrottleTimer.IsEnabled) {
                    _step1HueSliderThrottleTimer.Start();
                }
            }
        }

        private void ApplyStep1HueSliderChange(double hueValue) {
            _lastStep1HueSliderApplyTicks = DateTime.UtcNow.Ticks;
            _selectedColor = ShellHelper.ColorFromPastelHueHex(hueValue);
            ApplyMaterialYouPalette(hueValue);
            UpdateLivePreview();
        }

        private void Step1OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            _folderOpacity = e.NewValue / 100.0;
            if (Step1OpacityValueText != null) {
                Step1OpacityValueText.Text = $"{(int)e.NewValue}%";
            }
            if (!_isInitializing) UpdateLivePreview();
        }

        private void Step1BlurCheckBox_Click(object sender, RoutedEventArgs e) {
            _folderBlurEnabled = Step1BlurCheckBox.IsChecked ?? true;
            if (Step1BlurRadiusSlider != null) {
                Step1BlurRadiusSlider.IsEnabled = _folderBlurEnabled;
            }
            if (!_isInitializing) UpdateLivePreview();
        }

        private void Step1BlurRadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            _folderBlurRadius = e.NewValue;
            if (Step1RadiusValueText != null) {
                Step1RadiusValueText.Text = $"{(int)e.NewValue} px";
            }
            if (!_isInitializing) UpdateLivePreview();
        }

        // Event Handlers for Step 2
        private void Step2SliderModeCard_Click(object sender, MouseButtonEventArgs e) {
            _pagingMode = "Slider";
            UpdateStep2PagingModeCardsVisual();
            RebuildPreviewItems();
            UpdateLivePreview();
        }

        private void Step2ScrollModeCard_Click(object sender, MouseButtonEventArgs e) {
            _pagingMode = "Scrollbar";
            UpdateStep2PagingModeCardsVisual();
            RebuildPreviewItems();
            UpdateLivePreview();
        }

        private void UpdateStep2PagingModeCardsVisual() {
            if (Step2SliderModeCard == null || Step2ScrollModeCard == null) return;
            var primaryBrush = TryFindResource("M3PrimaryBrush") as Brush ?? Brushes.DarkOrange;

            Color baseCol = ShellHelper.ParseWpfColorFast(_selectedColor, Color.FromRgb(0xFC, 0xDD, 0xC5));
            // Dynamic tint matching the selected color tone
            var capsuleBg = new SolidColorBrush(Color.FromArgb(42, baseCol.R, baseCol.G, baseCol.B));
            capsuleBg.Freeze();

            var outlineBrush = TryFindResource("M3OutlineBrush") as Brush ?? Brushes.Gray;
            var cardBrush = TryFindResource("M3CardBrush") as Brush ?? Brushes.White;

            bool isSlider = string.Equals(_pagingMode, "Slider", StringComparison.OrdinalIgnoreCase);
            if (isSlider) {
                Step2SliderModeCard.BorderBrush = primaryBrush;
                Step2SliderModeCard.BorderThickness = new Thickness(2);
                Step2SliderModeCard.Background = capsuleBg;
                if (Step2SliderCheckBadge != null) Step2SliderCheckBadge.Visibility = Visibility.Visible;

                Step2ScrollModeCard.BorderBrush = outlineBrush;
                Step2ScrollModeCard.BorderThickness = new Thickness(1);
                Step2ScrollModeCard.Background = cardBrush;
                if (Step2ScrollCheckBadge != null) Step2ScrollCheckBadge.Visibility = Visibility.Collapsed;
            } else {
                Step2ScrollModeCard.BorderBrush = primaryBrush;
                Step2ScrollModeCard.BorderThickness = new Thickness(2);
                Step2ScrollModeCard.Background = capsuleBg;
                if (Step2ScrollCheckBadge != null) Step2ScrollCheckBadge.Visibility = Visibility.Visible;

                Step2SliderModeCard.BorderBrush = outlineBrush;
                Step2SliderModeCard.BorderThickness = new Thickness(1);
                Step2SliderModeCard.Background = cardBrush;
                if (Step2SliderCheckBadge != null) Step2SliderCheckBadge.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateFocusModeVisuals(bool animate = false) {
            bool isPopupOpen = _isPopup1Open || _isPopup2Open;
            bool shouldShowFocus = _focusMode && isPopupOpen;

            double targetDimming = 0.0;
            double targetBlur = 0.0;

            if (shouldShowFocus) {
                double dimPct = Step2DimmingSlider != null ? Step2DimmingSlider.Value : (_focusDimming * 100.0);
                targetDimming = Math.Clamp(dimPct / 100.0, 0.0, 1.0);

                bool blurOn = Step2FocusBlurCheckBox?.IsChecked ?? _focusBlurEnabled;
                if (blurOn) {
                    double blurRad = Step2FocusBlurSlider != null ? Step2FocusBlurSlider.Value : _focusBlurRadius;
                    targetBlur = Math.Clamp(blurRad, 0.0, 100.0);
                }
            }

            // Step 1 preview must NEVER show Focus Mode dimming or blur
            if (Step1FocusDimOverlay != null) {
                Step1FocusDimOverlay.BeginAnimation(OpacityProperty, null);
                Step1FocusDimOverlay.Opacity = 0.0;
            }
            if (Step1FocusWallpaperBlurFx != null) {
                Step1FocusWallpaperBlurFx.BeginAnimation(BlurEffect.RadiusProperty, null);
                Step1FocusWallpaperBlurFx.Radius = 0.0;
            }

            // Only Step 2 and Step 3 reflect Focus Mode
            Border?[] overlays = { Step2FocusDimOverlay, Step3FocusDimOverlay };
            BlurEffect?[] blurs = { Step2FocusWallpaperBlurFx, Step3FocusWallpaperBlurFx };

            if (animate) {
                var duration = TimeSpan.FromMilliseconds(220);
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                foreach (var overlay in overlays) {
                    if (overlay != null) {
                        var dimAnim = new DoubleAnimation(overlay.Opacity, targetDimming, duration) { EasingFunction = ease };
                        overlay.BeginAnimation(OpacityProperty, dimAnim);
                    }
                }
                foreach (var blur in blurs) {
                    if (blur != null) {
                        var blurAnim = new DoubleAnimation(blur.Radius, targetBlur, duration) { EasingFunction = ease };
                        blur.BeginAnimation(BlurEffect.RadiusProperty, blurAnim);
                    }
                }
            } else {
                foreach (var overlay in overlays) {
                    if (overlay != null) {
                        overlay.BeginAnimation(OpacityProperty, null);
                        overlay.Opacity = targetDimming;
                    }
                }
                foreach (var blur in blurs) {
                    if (blur != null) {
                        blur.BeginAnimation(BlurEffect.RadiusProperty, null);
                        blur.Radius = targetBlur;
                    }
                }
            }
        }

        private void Step2ColsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            _gridColumns = (int)e.NewValue;
            if (Step2ColsValueText != null) {
                Step2ColsValueText.Text = _gridColumns.ToString();
            }
            if (!_isInitializing) {
                RebuildPreviewItems();
                UpdateLivePreview();
            }
        }

        private void Step2RowsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            _gridRows = (int)e.NewValue;
            if (Step2RowsValueText != null) {
                Step2RowsValueText.Text = _gridRows.ToString();
            }
            if (!_isInitializing) {
                RebuildPreviewItems();
                UpdateLivePreview();
            }
        }

        private void Step2FocusCheckBox_Click(object sender, RoutedEventArgs e) {
            _focusMode = Step2FocusCheckBox.IsChecked ?? true;
            UpdateFocusModeVisuals(animate: true);
            UpdateLivePreview();
        }

        private void Step2FocusBlurCheckBox_Click(object sender, RoutedEventArgs e) {
            _focusBlurEnabled = Step2FocusBlurCheckBox.IsChecked ?? true;
            if (Step2FocusBlurSlider != null) {
                Step2FocusBlurSlider.IsEnabled = _focusBlurEnabled;
            }
            UpdateFocusModeVisuals(animate: true);
            UpdateLivePreview();
        }

        private void Step2DimmingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            _focusDimming = e.NewValue / 100.0;
            if (Step2DimmingValueText != null) {
                Step2DimmingValueText.Text = $"{(int)e.NewValue}%";
            }
            UpdateFocusModeVisuals(animate: false);
            if (!_isInitializing) UpdateLivePreview();
        }

        private void Step2FocusBlurSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            _focusBlurRadius = e.NewValue;
            if (Step2FocusBlurValueText != null) {
                Step2FocusBlurValueText.Text = $"{(int)e.NewValue} px";
            }
            UpdateFocusModeVisuals(animate: false);
            if (!_isInitializing) UpdateLivePreview();
        }

        private void Step2ArrowsCheckBox_Click(object sender, RoutedEventArgs e) {
            _sliderArrows = Step2ArrowsCheckBox.IsChecked ?? true;
            RebuildPreviewItems();
            UpdateLivePreview();
        }

        private void Step2DragCheckBox_Click(object sender, RoutedEventArgs e) {
            _sliderDrag = Step2DragCheckBox.IsChecked ?? true;
        }

        // Navigation Footer Buttons
        private void BackBtn_Click(object sender, RoutedEventArgs e) {
            GoToStep(_currentStep - 1);
        }

        private void SkipBtn_Click(object sender, RoutedEventArgs e) {
            GoToStep(2);
        }

        private void NextBtn_Click(object sender, RoutedEventArgs e) {
            if (_currentStep < 2) {
                GoToStep(_currentStep + 1);
            } else {
                SaveConfiguredSettings();
                try {
                    ((App)Application.Current)?.ShowSettings();
                } catch { }
                Close();
            }
        }

        private void CloseWizardBtn_Click(object sender, RoutedEventArgs e) {
            SaveConfiguredSettings();
            Close();
        }

        private void PreviewMoreBtn_Click(object sender, RoutedEventArgs e) {
            Step1PreviewMoreBtn_Click(sender, e);
        }

        private void PreviewMoreBtn2_Click(object sender, RoutedEventArgs e) {
            Step1PreviewMoreBtn_Click(sender, e);
        }

        private void StepDot_MouseDown(object sender, MouseButtonEventArgs e) {
            if (sender is FrameworkElement el && el.Tag is string tag && int.TryParse(tag, out int idx)) {
                GoToStep(idx);
            }
        }

        private void CrowdinBtn_Click(object sender, RoutedEventArgs e) {
            try {
                Process.Start(new ProcessStartInfo("https://crowdin.com/project/panak") { UseShellExecute = true });
            } catch { }
        }

        private void KofiBtn_Click(object sender, RoutedEventArgs e) {
            try {
                Process.Start(new ProcessStartInfo("https://ko-fi.com/hranthakobian/") { UseShellExecute = true });
            } catch { }
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e) {
            SaveConfiguredSettings();
            try {
                if (Application.Current is App app) {
                    app.ShowSettings();
                }
            } catch { }
            Close();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) {
            SaveConfiguredSettings();
            Close();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                DragMove();
                UpdateWallpaperCrop();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                SaveConfiguredSettings();
                Close();
            }
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);
            if (_liveWallpaperTimer != null) {
                _liveWallpaperTimer.Stop();
                _liveWallpaperTimer = null;
            }
            if (_shapeMorphTimer != null) {
                _shapeMorphTimer.Stop();
                _shapeMorphTimer = null;
            }
        }

        private System.Diagnostics.Stopwatch _dynamicIconThrottleStopwatch = System.Diagnostics.Stopwatch.StartNew();
        private DispatcherTimer? _dynamicIconDebounceTimer;
        private double _pendingIconHue;
        private bool _pendingIconIsDark;
        private IntPtr _lastCustomHIcon = IntPtr.Zero;
        private const int DynamicIconThrottleMs = 35;

        private void QueueDynamicIconUpdate(double hue, bool isDark) {
            _pendingIconHue = hue;
            _pendingIconIsDark = isDark;

            try {
                this.Icon = IconGenerator.RenderPanakLogoImageSource(32, hue, isDark);
            } catch { }

            void ApplyDynamicIcon() {
                try {
                    TrayManager.Instance?.UpdateTrayIconTheme(_pendingIconHue, _pendingIconIsDark);
                    var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    if (hwnd != IntPtr.Zero) {
                        using var bmp = IconGenerator.RenderPanakLogoBitmap(32, _pendingIconHue, _pendingIconIsDark);
                        IntPtr newHIcon = bmp.GetHicon();
                        ShellHelper.SendMessage(hwnd, 0x0080 /* WM_SETICON */, (IntPtr)1 /* ICON_BIG */, newHIcon);
                        ShellHelper.SendMessage(hwnd, 0x0080 /* WM_SETICON */, (IntPtr)0 /* ICON_SMALL */, newHIcon);
                        if (_lastCustomHIcon != IntPtr.Zero) {
                            ShellHelper.DestroyIcon(_lastCustomHIcon);
                        }
                        _lastCustomHIcon = newHIcon;
                    }
                } catch { }
            }

            if (_dynamicIconDebounceTimer == null) {
                _dynamicIconDebounceTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(DynamicIconThrottleMs) };
                _dynamicIconDebounceTimer.Tick += (s, e) => {
                    _dynamicIconDebounceTimer.Stop();
                    ApplyDynamicIcon();
                };
            }

            if (_dynamicIconThrottleStopwatch.ElapsedMilliseconds >= DynamicIconThrottleMs) {
                _dynamicIconThrottleStopwatch.Restart();
                _dynamicIconDebounceTimer.Stop();
                ApplyDynamicIcon();
            } else {
                _dynamicIconDebounceTimer.Stop();
                _dynamicIconDebounceTimer.Start();
            }
        }
    }
}

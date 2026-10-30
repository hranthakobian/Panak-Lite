using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    public partial class SettingsWindow : Window {
        private bool _isInitializing = true;
        private string _selectedShape = "Squircle";
        private string _selectedColor = ShellHelper.DefaultPastelColorHex;
        private string _selectedThemeMode = "Auto";
        private string _initialColor = ShellHelper.DefaultPastelColorHex;
        private string _initialThemeMode = "Auto";
        private string _currentQuickLookHotkey = "Space";
        private string _currentClosePopupHotkey = "Escape";
        private string? _recordingTarget = null;
        private bool _isAppliedOrSaved = false;
        private bool _isSliderMode = true;
        private RadioButton? SettingsAccentSwatch = null;
        private static readonly (string Hex, string Name)[] DefaultPresetColors = new[] {
            (ShellHelper.DefaultPastelColorHex, "Peach / Default"),
            ("#C5FCC5", "Mint"),
            ("#C5FCFC", "Cyan"),
            ("#C5DDFC", "Sky Blue"),
            ("#DDC5FC", "Lavender"),
            ("#FCC5FC", "Lilac"),
            ("#FCC5DD", "Rose"),
            ("#FCFCC5", "Butter Yellow")
        };

        public SettingsWindow() {
            InitializeComponent();
            LoadLogoAndIcon();
            LoadCurrentSettings();
            ApplyLocalization();
            StartWallpaperCapture();
            UpdateLivePreview();
            CheckSettingsLockState();
            _isInitializing = false;

            Loaded += (s, e) => {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero) {
                    bool isDark = _selectedThemeMode == "Dark" || (_selectedThemeMode == "Auto" && ShellHelper.IsWindowsDarkTheme());
                    ShellHelper.OptimizeWindowForOs(hwnd, isDark, roundCorners: false);
                }
                double currentHue = ShellHelper.GetHueFromHex(_selectedColor);
                ApplyMaterialYouPalette(currentHue);
                UpdateLivePreview();
                UpdateWallpaperCrop();
                int tIdx = _selectedThemeMode == "Light" ? 0 : (_selectedThemeMode == "Dark" ? 1 : 2);
                AnimateThemePill(tIdx, immediate: true);
                int sIdx = GetShapeIndex(_selectedShape);
                AnimateSettingsShapePill(sIdx, immediate: true);
                AnimatePreviewShapePill(sIdx, immediate: true);
            };
            LocationChanged += (s, e) => UpdateWallpaperCrop();

            FocusModeCheckBox.Click += (s, e) => { UpdateFocusModeControlsEnabled(animate: true); UpdateLivePreview(); };
            FocusBlurCheckBox.Click += (s, e) => { UpdateFocusModeControlsEnabled(animate: true); UpdateLivePreview(); };
            FolderBlurCheckBox.Click += (s, e) => { UpdateFolderBlurControlsEnabled(animate: true); UpdateLivePreview(); };

            GitHubUpdateService.Instance.StatusChanged += OnUpdateStatusChanged;
            GitHubUpdateService.Instance.DownloadProgressChanged += OnDownloadProgressChanged;
            Closed += (s, e) => {
                GitHubUpdateService.Instance.StatusChanged -= OnUpdateStatusChanged;
                GitHubUpdateService.Instance.DownloadProgressChanged -= OnDownloadProgressChanged;
            };
        }

        private void LoadLogoAndIcon() {
            BitmapImage? bi = null;
            try {
                bi = new BitmapImage(new Uri("pack://application:,,,/Logo.png", UriKind.Absolute));
            } catch {
                try {
                    bi = new BitmapImage(new Uri("pack://application:,,,/SettingsLogo.png", UriKind.Absolute));
                } catch { }
            }

            if (bi == null) {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = {
                    Path.Combine(baseDir, "src", "Logo.png"),
                    Path.Combine(baseDir, "Logo.png"),
                    Path.Combine(baseDir, "src", "SettingsLogo.png"),
                    Path.Combine(baseDir, "SettingsLogo.png"),
                    Path.Combine(baseDir, "PanakLogo.png"),
                    Path.Combine(baseDir, "Panak logo.png"),
                    Path.Combine(baseDir, "Panak.ico")
                };
                foreach (var file in candidates) {
                    if (File.Exists(file)) {
                        bi = new BitmapImage(new Uri(file, UriKind.Absolute));
                        break;
                    }
                }
            }

            if (bi != null) {
                Icon = bi;
            }
        }

        private bool? _lastAppliedWindowOsDark;

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

                static System.Windows.Media.Color toWpf(System.Drawing.Color c) =>
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
                    wpfSurface = toWpf(ShellHelper.ColorFromHsl(hue, 0.16, 0.11)); // Deep dark surface #16161D
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

                var animDuration = TimeSpan.FromMilliseconds(260);

                // Mutate SolidColorBrush in-place to avoid full visual-tree DynamicResource re-evaluation on every slider tick
                void SetBrush(string key, System.Windows.Media.Color col) {
                    if (animate) {
                        ShellHelper.AnimateResourceBrushColor(Resources, key, col, animDuration);
                    } else {
                        ShellHelper.SetResourceBrushColor(Resources, key, col);
                    }
                }

                SetBrush("M3SurfaceBrush", wpfSurface);
                SetBrush("M3SurfaceContainerBrush", wpfContainer);
                SetBrush("M3CardBrush", wpfCard);
                SetBrush("M3PrimaryBrush", wpfPrimary);
                SetBrush("M3OnPrimaryBrush", wpfOnPrimary);
                SetBrush("M3OnSurfaceBrush", wpfOnSurface);
                SetBrush("M3OnSurfaceVariantBrush", wpfOnSurfaceVariant);
                SetBrush("M3TrackInactiveBrush", wpfTrackInactive);

                var wpfErrorContainer = isDark
                    ? toWpf(ShellHelper.ColorFromHsl((hue + 350.0) % 360.0, 0.45, 0.25))
                    : toWpf(ShellHelper.ColorFromHsl((hue + 350.0) % 360.0, 0.65, 0.88));
                var wpfOnErrorContainer = isDark
                    ? toWpf(ShellHelper.ColorFromHsl((hue + 350.0) % 360.0, 0.80, 0.90))
                    : toWpf(ShellHelper.ColorFromHsl((hue + 350.0) % 360.0, 0.80, 0.22));
                SetBrush("M3ErrorContainerBrush", wpfErrorContainer);
                SetBrush("M3OnErrorContainerBrush", wpfOnErrorContainer);

                // Dynamically update the 5 vector logo colors based on active theme hue & luminance
                var (cContainer, cTriangle, cCloud, cShield, cEllipse) = IconGenerator.GetLogoDynamicColors(hue, isDark);
                SetBrush("LogoContainerBrush", toWpf(cContainer));
                SetBrush("LogoTriangleBrush", toWpf(cTriangle));
                SetBrush("LogoCloudBrush", toWpf(cCloud));
                SetBrush("LogoShieldBrush", toWpf(cShield));
                SetBrush("LogoEllipseBrush", toWpf(cEllipse));

                // Update Tray and Window & Taskbar Icon dynamically without blocking UI
                QueueDynamicIconUpdate(hue, isDark);

                System.Windows.Media.Color selColor = ShellHelper.ParseWpfColorFast(_selectedColor, wpfPrimary);
                SetBrush("SettingsHueThumbBrush", selColor);

                if (MainWindowBorder != null) {
                    MainWindowBorder.SetResourceReference(Border.BackgroundProperty, "M3SurfaceBrush");
                }
                if (SettingsColorIndicator != null) {
                    if (animate) {
                        SettingsColorIndicator.Background = ShellHelper.AnimateOrCreateBrush(SettingsColorIndicator.Background, selColor, animDuration);
                    } else {
                        SettingsColorIndicator.Background = ShellHelper.UpdateOrCreateBrush(SettingsColorIndicator.Background, selColor);
                    }
                }
                if (LanguageComboBox != null) {
                    if (animate) {
                        LanguageComboBox.Foreground = ShellHelper.AnimateOrCreateBrush(LanguageComboBox.Foreground, wpfOnSurface, animDuration);
                    } else {
                        LanguageComboBox.Foreground = ShellHelper.UpdateOrCreateBrush(LanguageComboBox.Foreground, wpfOnSurface);
                    }
                }
                UpdatePagingModeCardsVisual();
            } catch { }
        }

        private void LoadCurrentSettings() {
            var s = SettingsService.Instance.Current;
            _selectedShape = s.IconShape;
            _selectedColor = string.IsNullOrEmpty(s.ThemeColor) ? ShellHelper.GetDefaultPastelColorHex() : s.ThemeColor;

            // Theme Mode
            _selectedThemeMode = string.IsNullOrEmpty(s.ThemeMode) ? "Auto" : s.ThemeMode;
            _initialColor = _selectedColor;
            _initialThemeMode = _selectedThemeMode;
            if (string.Equals(_selectedThemeMode, "Light", StringComparison.OrdinalIgnoreCase)) {
                SettingsThemeLight.IsChecked = true;
            } else if (string.Equals(_selectedThemeMode, "Dark", StringComparison.OrdinalIgnoreCase)) {
                SettingsThemeDark.IsChecked = true;
            } else {
                SettingsThemeAuto.IsChecked = true;
            }
            int initialThemeIdx = string.Equals(_selectedThemeMode, "Light", StringComparison.OrdinalIgnoreCase) ? 0 :
                                  (string.Equals(_selectedThemeMode, "Dark", StringComparison.OrdinalIgnoreCase) ? 1 : 2);
            AnimateThemePill(initialThemeIdx, immediate: true);

            // Apply Material You real-time palette for the current color/hue
            double initialHue = ShellHelper.GetHueFromHex(_selectedColor);
            ApplyMaterialYouPalette(initialHue);

            // Language
            foreach (ComboBoxItem item in LanguageComboBox.Items) {
                if (item.Tag?.ToString() == s.Language) {
                    item.IsSelected = true;
                    break;
                }
            }

            // Shape
            if (string.Equals(s.IconShape, "Circle", StringComparison.OrdinalIgnoreCase)) {
                SettingsShapeCircle.IsChecked = true;
            } else if (string.Equals(s.IconShape, "Clover", StringComparison.OrdinalIgnoreCase)) {
                SettingsShapeClover.IsChecked = true;
            } else if (string.Equals(s.IconShape, "Scallop", StringComparison.OrdinalIgnoreCase)) {
                SettingsShapeScallop.IsChecked = true;
            } else if (string.Equals(s.IconShape, "Arch", StringComparison.OrdinalIgnoreCase) || string.Equals(s.IconShape, "Teardrop", StringComparison.OrdinalIgnoreCase)) {
                SettingsShapeArch.IsChecked = true;
            } else {
                SettingsShapeSquircle.IsChecked = true;
            }
            int initialShapeIdx = GetShapeIndex(_selectedShape);
            AnimateSettingsShapePill(initialShapeIdx, immediate: true);
            AnimatePreviewShapePill(initialShapeIdx, immediate: true);

            // Pastel Theme Color Setup
            RebuildPresetSwatchesUI();
            RebuildCustomSwatchesUI();

            bool foundColorSwatch = false;
            foreach (var rb in GetAllColorSwatchRadioButtons()) {
                if (rb.Tag is string tag) {
                    bool isMatch = string.Equals(tag, _selectedColor, StringComparison.OrdinalIgnoreCase);
                    rb.IsChecked = isMatch;
                    if (isMatch) foundColorSwatch = true;
                }
            }
            if (!foundColorSwatch) {
                double selHue = ShellHelper.GetHueFromHex(_selectedColor);
                foreach (var rb in GetAllColorSwatchRadioButtons()) {
                    if (rb.Tag is string tag) {
                        double swHue = ShellHelper.GetHueFromHex(tag);
                        double diff = Math.Abs(swHue - selHue);
                        if (diff > 180) diff = 360 - diff;
                        if (diff < 4.0) {
                            rb.IsChecked = true;
                            foundColorSwatch = true;
                            break;
                        }
                    }
                }
            }

            SettingsHueSlider.Value = initialHue;

            // General folder opacity
            GeneralFolderOpacitySlider.Value = Math.Round(s.FolderOpacity * 100);
            GeneralFolderOpacityValueText.Text = $"{(int)GeneralFolderOpacitySlider.Value}%";

            // Folder blur
            FolderBlurCheckBox.IsChecked = s.FolderBlurEnabled;
            FolderBlurSlider.Value = s.FolderBlurRadius;
            FolderBlurValueText.Text = $"{(int)s.FolderBlurRadius} px";
            UpdateFolderBlurControlsEnabled(animate: false);

            // Paging mode & rows
            _isSliderMode = !string.Equals(s.PagingMode, "Scrollbar", StringComparison.OrdinalIgnoreCase);
            UpdatePagingModeCardsVisual();
            GridRowsSlider.Value = s.GridRows;
            GridRowsValueText.Text = s.GridRows.ToString();
            SliderArrowsCheckBox.IsChecked = s.SliderArrowsEnabled;
            SliderDragCheckBox.IsChecked = s.SliderDragEnabled;
            UpdatePagingControlsVisibility();

            // Grid columns
            GridColumnsSlider.Value = s.GridColumns;
            GridColumnsValueText.Text = s.GridColumns.ToString();

            // Focus mode
            FocusModeCheckBox.IsChecked = s.FocusModeEnabled;
            FocusDimmingSlider.Value = s.FocusDimmingOpacity * 100;
            FocusDimmingValueText.Text = $"{(int)(s.FocusDimmingOpacity * 100)}%";
            FocusBlurCheckBox.IsChecked = s.FocusBlurEnabled;
            FocusBlurSlider.Value = s.FocusBlurRadius;
            FocusBlurValueText.Text = $"{(int)s.FocusBlurRadius} px";
            UpdateFocusModeControlsEnabled(animate: false);

            // Lock & Hooks
            LockFoldersCheckBox.IsChecked = s.IsLocked;
            if (LockPopupPositionCheckBox != null) LockPopupPositionCheckBox.IsChecked = s.LockPopupPosition;
            CenterPopupCheckBox.IsChecked = s.OpenInCenterOnly;
            RightDoubleClickCheckBox.IsChecked = s.RightDoubleClickEnabled;
            SpaceQuickLookCheckBox.IsChecked = s.SpaceQuickLookEnabled;
            CombineFilesCheckBox.IsChecked = s.CombineFilesOnDrop;
            AutoStartCheckBox.IsChecked = InstallerService.IsAutoStartEnabled() || s.AutoStart;
            AutoCheckUpdatesCheckBox.IsChecked = s.AutoCheckUpdates;

            if (RandomShapeCheckBox != null) RandomShapeCheckBox.IsChecked = s.RandomShapeOnCreate;
            if (RandomColorCheckBox != null) RandomColorCheckBox.IsChecked = s.RandomColorOnCreate;
            if (RandomOpacityCheckBox != null) RandomOpacityCheckBox.IsChecked = s.RandomOpacityOnCreate;

            RebuildCustomSwatchesUI();

            // Context Menu status
            bool registered = ContextMenuRegistry.IsRegistered();
            var loc = LocalizationService.Instance;
            ContextMenuStatusText.Text = registered ? loc["ContextMenuInstalled"] : "Not registered in Explorer";

            _currentQuickLookHotkey = s.QuickLookHotkey ?? "Space";
            _currentClosePopupHotkey = s.ClosePopupHotkey ?? "Escape";
            UpdateKeyBadgesUI();

            UpdateUpdateUI();
            UpdateSettingsLockUI();
        }

        public void ApplyLocalization() {
            var loc = LocalizationService.Instance;

            TitleText.Text = loc["Settings"];
            SupportTitleText.Text = loc["Support"];
            SupportDescText.Text = loc["SupportDesc"];
            KofiButton.Content = loc["DonateOnKofi"];
            if (CrowdinButton != null) CrowdinButton.ToolTip = loc["TranslateOnCrowdin"];
            LanguageLabel.Text = loc["Language"];
            LanguageAiBadge.Text = loc["LanguageAiNote"];
            IconShapeLabel.Text = loc["IconShape"];
            SettingsShapeCircle.ToolTip = loc["Circle"];
            SettingsShapeSquircle.ToolTip = loc["Squircle"];
            SettingsShapeClover.ToolTip = loc["Clover"];
            SettingsShapeScallop.ToolTip = loc["Scallop"];
            SettingsShapeArch.ToolTip = loc["Arch"];
            ThemeSectionLabel.Text = loc["Theme"];
            ThemeSectionDesc.Text = loc["ThemeDesc"];
            SettingsThemeLight.Content = loc["ThemeLight"];
            SettingsThemeDark.Content = loc["ThemeDark"];
            SettingsThemeAuto.Content = loc["ThemeAuto"];
            DefaultColorLabel.Text = loc["DefaultFolderColor"];
            GeneralFolderOpacityLabel.Text = loc["GeneralFolderOpacity"];
            GeneralFolderOpacityDescText.Text = loc["GeneralFolderOpacityDesc"];
            FolderBlurLabel.Text = loc["FolderBlur"];
            if (FolderBlurWarningBadge != null) FolderBlurWarningBadge.Text = loc["SlowsDownSystemWarning"];
            FolderBlurDescText.Text = loc["FolderBlurDesc"];
            FolderBlurRadiusLabel.Text = loc["FolderBlurRadius"];
            PagingModeLabel.Text = loc["PagingMode"];
            PagingModeDescText.Text = loc["PagingModeDesc"];
            if (PagingSliderLabel != null) PagingSliderLabel.Text = loc["PagingModeSlider"];
            if (PagingScrollbarLabel != null) PagingScrollbarLabel.Text = loc["PagingModeScrollbar"];
            GridRowsLabel.Text = loc["GridRows"];
            SliderArrowsLabel.Text = loc["SliderArrows"];
            SliderDragLabel.Text = loc["SliderDrag"];
            GridColumnsLabel.Text = loc["ItemsPerRow"];
            FocusModeLabel.Text = loc["FocusMode"];
            FocusModeDescText.Text = loc["FocusModeDesc"];
            FocusDimmingLabel.Text = loc["FocusDimming"];
            FocusBlurLabel.Text = loc["FocusBlur"];
            if (FocusBlurWarningBadge != null) FocusBlurWarningBadge.Text = loc["SlowsDownSystemWarning"];
            FocusBlurDescText.Text = loc["FocusBlurDesc"];
            FocusBlurRadiusLabel.Text = loc["FocusBlurRadius"];
            CenterPopupLabel.Text = loc["CenterPopupOnly"];
            CenterPopupDescText.Text = loc["CenterPopupOnlyDesc"];
            LockFoldersLabel.Text = loc["LockFolders"];
            LockFoldersDescText.Text = loc["LockFoldersDesc"];
            if (LockPopupPositionLabel != null) LockPopupPositionLabel.Text = loc["LockPopupPosition"];
            if (LockPopupPositionDescText != null) LockPopupPositionDescText.Text = loc["LockPopupPositionDesc"];
            RightDoubleClickLabel.Text = loc["RightDoubleClick"];
            RightDoubleClickDescText.Text = loc["RightDoubleClickDesc"];
            SpaceQuickLookLabel.Text = loc["SpaceQuickLook"];
            SpaceQuickLookDescText.Text = loc["SpaceQuickLookDesc"];
            if (KeyRebindingLabel != null) KeyRebindingLabel.Text = loc["KeyRebinding"];
            if (KeyRebindingDescText != null) KeyRebindingDescText.Text = loc["KeyRebindingDesc"];
            if (ResetKeysBtn != null) ResetKeysBtn.Content = loc["ResetKeys"];
            if (QuickLookKeyRowLabel != null) QuickLookKeyRowLabel.Text = loc["QuickLookKeyRow"];
            if (QuickLookKeyRowDesc != null) QuickLookKeyRowDesc.Text = loc["QuickLookKeyRowDesc"];
            if (ClosePopupKeyRowLabel != null) ClosePopupKeyRowLabel.Text = loc["ClosePopupKeyRow"];
            if (ClosePopupKeyRowDesc != null) ClosePopupKeyRowDesc.Text = loc["ClosePopupKeyRowDesc"];
            UpdateKeyBadgesUI();
            CombineFilesLabel.Text = loc["CombineFilesOnDrop"];
            CombineFilesDescText.Text = loc["CombineFilesOnDropDesc"];
            AutoStartLabel.Text = loc["AutoStart"];
            UpdatesSectionTitle.Text = loc["Updates"];
            CheckUpdatesBtn.Content = loc["CheckForUpdates"];
            AutoCheckUpdatesLabel.Text = loc["AutoCheckUpdates"];
            AutoCheckUpdatesDesc.Text = loc["AutoCheckUpdatesDesc"];
            ReleaseNotesHeader.Text = loc["ReleaseNotes"] + ":";
            DownloadInstallBtn.Content = loc["DownloadAndInstall"];
            SaveBtn.Content = loc["Save"];
            ApplyBtn.Content = loc["Apply"];
            CancelBtn.Content = loc["Cancel"];
            UninstallAppBtn.Content = loc["Uninstall"];
            InstallerSectionTitle.Text = loc["Uninstaller"];

            if (RandomShapeCheckBox != null) RandomShapeCheckBox.ToolTip = loc["RandomShape"] ?? "Random shape";
            if (RandomColorCheckBox != null) RandomColorCheckBox.ToolTip = loc["RandomColor"] ?? "Random color";
            if (RandomOpacityCheckBox != null) RandomOpacityCheckBox.ToolTip = loc["RandomOpacity"] ?? "Random opacity";
            if (RandomShapeLabel != null) RandomShapeLabel.Text = loc["RandomByChance"] ?? "Պատահականությամբ";
            if (RandomColorLabel != null) RandomColorLabel.Text = loc["RandomByChance"] ?? "Պատահականությամբ";
            if (RandomOpacityLabel != null) RandomOpacityLabel.Text = loc["RandomByChance"] ?? "Պատահականությամբ";
            if (RestorePresetsMenuItem != null) RestorePresetsMenuItem.Header = loc["RestoreDefaultColors"] ?? "Վերականգնել լռելյայն գույները";
            if (ExportSettingsBtn != null) ExportSettingsBtn.Content = loc["ExportSettings"] ?? "Export";
            if (ImportSettingsBtn != null) ImportSettingsBtn.Content = loc["ImportSettings"] ?? "Import";
            if (ExportImportSectionTitle != null) ExportImportSectionTitle.Text = loc["BackupAndRestore"] ?? "Backup & Restore";
            if (ExportImportSectionDesc != null) ExportImportSectionDesc.Text = loc["ExportImportDesc"] ?? "Export or import configuration via JSON file";

            if (PreviewMoreButton != null) PreviewMoreButton.ToolTip = loc["MoreActions"];
            if (PreviewMenuEditText != null) PreviewMenuEditText.Text = loc["Rename"];
            if (PreviewMenuExplorerText != null) PreviewMenuExplorerText.Text = loc["OpenInExplorer"];
            if (PreviewMenuPinText != null) PreviewMenuPinText.Text = _isPreviewPinned ? loc["Unpin"] : loc["Pin"];

            bool registered = ContextMenuRegistry.IsRegistered();
            ContextMenuStatusText.Text = registered ? loc["ContextMenuInstalled"] : "Explorer context menu not registered";
            if (NavTabAppearance != null) NavTabAppearance.Content = loc["AppearanceTab"] ?? "🎨 Արտաքին";
            if (NavTabLayout != null) NavTabLayout.Content = loc["LayoutTab"] ?? "📐 Դիրք";
            if (NavTabSecurity != null) NavTabSecurity.Content = loc["SecurityTab"] ?? "🔒 Անվտանգություն";
            if (NavTabSystem != null) NavTabSystem.Content = loc["SystemTab"] ?? "⚙️ Համակարգ";

            if (SectionAppearanceTitle != null) SectionAppearanceTitle.Text = loc["AppearanceTab"] ?? "🎨 Արտաքին";
            if (SectionLayoutTitle != null) SectionLayoutTitle.Text = loc["LayoutTab"] ?? "📐 Դիրք";
            if (SectionSecurityTitle != null) SectionSecurityTitle.Text = loc["SecurityTab"] ?? "🔒 Անվտանգություն";
            if (SectionSystemTitle != null) SectionSystemTitle.Text = loc["SystemTab"] ?? "⚙️ Համակարգ";

            if (CheckOtaBtn != null) CheckOtaBtn.Content = "🔄 " + (loc["CheckOtaUpdates"] ?? "Ստուգել OTA");
            if (ExportLangJsonBtn != null) ExportLangJsonBtn.Content = "💾 " + (loc["ExportLanguageJson"] ?? "Արտահանել JSON");
            if (LockSettingsLabel != null) LockSettingsLabel.Text = loc["LockSettings"] ?? "Կողպել Կայանքները";
            if (LockSettingsDescText != null) LockSettingsDescText.Text = loc["LockSettingsDesc"] ?? "Պահանջել գաղտնաբառ/PIN/նախշ/կենսաչափություն կայանքները բացելու կամ փոփոխելու համար";
            if (ConfigureSettingsLockBtn != null) ConfigureSettingsLockBtn.Content = loc["ConfigureLock"] ?? "Կարգավորել կողպումը";
            if (TogglePreviewBtnText != null) TogglePreviewBtnText.Text = (_isPreviewVisible ? (loc["HidePreview"] ?? "Նախադիտում") : (loc["ShowPreview"] ?? "Նախադիտում"));
            if (PreviewLiveBadgeText != null) PreviewLiveBadgeText.Text = loc["LivePreview"] ?? "Live Preview";
            if (PreviewEditNameLabel != null) PreviewEditNameLabel.Text = loc["FolderTitle"] ?? "Անուն";
            if (PreviewColorThemeLabel != null) PreviewColorThemeLabel.Text = loc["ColorTheme"] ?? "Գույն և ձև";
            if (PreviewFolderLockHeaderLabel != null) PreviewFolderLockHeaderLabel.Text = loc["LockFolder"] ?? "Կողպել պանակը";
            if (PreviewFolderLockBtnText != null) PreviewFolderLockBtnText.Text = loc["ConfigureLock"] ?? "Կարգավորել կողպումը";
            if (PreviewAlwaysPanakCheckBox != null) PreviewAlwaysPanakCheckBox.Content = loc["AlwaysOpenWithPanak"] ?? "Միշտ բացել որպես Panak";
            if (PreviewUnpackFolderBtnText != null) PreviewUnpackFolderBtnText.Text = loc["UnpackPanakFolder"] ?? "Ապահավաքել Panak-ը";
            UpdateUpdateUI();
            PopulatePreviewItems();
        }

        private void SelectThemeColor(string hex, bool animate = true) {
            if (_isInitializing) return;
            _selectedColor = hex;
            double hue = ShellHelper.GetHueFromHex(hex);
            ApplyMaterialYouPalette(hue, animate: animate);

            _isInitializing = true;
            try {
                if (SettingsHueSlider != null) {
                    SettingsHueSlider.Value = hue;
                }
                if (PreviewHueSlider != null) {
                    PreviewHueSlider.Value = hue;
                }
            } finally {
                _isInitializing = false;
            }

            foreach (var rb in GetAllColorSwatchRadioButtons()) {
                if (rb.Tag is string tag) {
                    rb.IsChecked = string.Equals(tag, hex, StringComparison.OrdinalIgnoreCase);
                }
            }

            if (PreviewColorSwatchesPanel != null) {
                foreach (var child in PreviewColorSwatchesPanel.Children) {
                    if (child is RadioButton prb && prb.Tag is string ptag) {
                        prb.IsChecked = string.Equals(ptag, hex, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            if (PanakManager.Instance.IsPopupOpen) {
                PanakManager.Instance.UpdateOpenPopupThemeColor(_selectedColor);
            }

            UpdateLivePreview(animate: animate);
        }

        private void SettingsColorSwatch_Checked(object sender, RoutedEventArgs e) {
            if (_isInitializing) return;
            if (sender is RadioButton rb && rb.Tag is string hex) {
                SelectThemeColor(hex, animate: true);
            }
        }

        private bool _isSyncingHueSliders = false;
        private long _lastHueSliderApplyTick = 0;
        private double _pendingHueSliderValue = 0;
        private DispatcherTimer? _hueSliderThrottleTimer;
        private (RadioButton Rb, double Hue)[]? _cachedColorSwatchHues;

        private (RadioButton Rb, double Hue)[] GetCachedColorSwatchHues() {
            if (_cachedColorSwatchHues != null && _cachedColorSwatchHues.Length > 0) {
                return _cachedColorSwatchHues;
            }
            var list = new List<(RadioButton, double)>();
            foreach (var rb in GetAllColorSwatchRadioButtons()) {
                if (rb != null && rb.Tag is string tag) {
                    list.Add((rb, ShellHelper.GetHueFromHex(tag)));
                }
            }
            if (list.Count > 0) {
                _cachedColorSwatchHues = list.ToArray();
                return _cachedColorSwatchHues;
            }
            return Array.Empty<(RadioButton, double)>();
        }

        private void ApplyHueSliderChangeCore(double hueVal) {
            _lastHueSliderApplyTick = Environment.TickCount64;
            string pastelHex = ShellHelper.ColorFromPastelHueHex(hueVal);
            _selectedColor = pastelHex;
            ApplyMaterialYouPalette(hueVal);

            if (PanakManager.Instance.IsPopupOpen) {
                PanakManager.Instance.UpdateOpenPopupThemeColor(_selectedColor);
            }
            UpdateLivePreview();

            foreach (var (rb, swatchHue) in GetCachedColorSwatchHues()) {
                double diff = Math.Abs(swatchHue - hueVal);
                if (diff > 180) diff = 360 - diff;
                bool isMatch = diff < 4.0;
                if (rb.IsChecked != isMatch) {
                    rb.IsChecked = isMatch;
                }
            }
        }

        private void SettingsHueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (_isInitializing || _isSyncingHueSliders) return;
            _pendingHueSliderValue = e.NewValue;

            long now = Environment.TickCount64;
            if (now - _lastHueSliderApplyTick >= 15) {
                _hueSliderThrottleTimer?.Stop();
                ApplyHueSliderChangeCore(e.NewValue);
                return;
            }

            if (_hueSliderThrottleTimer == null) {
                _hueSliderThrottleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
                _hueSliderThrottleTimer.Tick += (s, ev) => {
                    _hueSliderThrottleTimer.Stop();
                    if (!_isInitializing) {
                        ApplyHueSliderChangeCore(_pendingHueSliderValue);
                    }
                };
            }
            if (!_hueSliderThrottleTimer.IsEnabled) {
                _hueSliderThrottleTimer.Start();
            }
        }

        private void SettingsShape_Click(object sender, RoutedEventArgs e) {
            if (_isInitializing) {
                return;
            }
            if (sender == SettingsShapeCircle) _selectedShape = "Circle";
            else if (sender == SettingsShapeSquircle) _selectedShape = "Squircle";
            else if (sender == SettingsShapeClover) _selectedShape = "Clover";
            else if (sender == SettingsShapeScallop) _selectedShape = "Scallop";
            else if (sender == SettingsShapeArch) _selectedShape = "Arch";

            int sIdx = GetShapeIndex(_selectedShape);
            AnimateSettingsShapePill(sIdx, immediate: false);
            AnimatePreviewShapePill(sIdx, immediate: false);
            UpdateLivePreview();
        }

        private void SettingsTheme_Click(object sender, RoutedEventArgs e) {
            if (_isInitializing) {
                return;
            }
            if (sender is RadioButton rb && rb.Tag is string mode) {
                _selectedThemeMode = mode;
                int tIdx = mode == "Light" ? 0 : (mode == "Dark" ? 1 : 2);
                AnimateThemePill(tIdx, immediate: false);

                double hue = ShellHelper.GetHueFromHex(_selectedColor);
                ApplyMaterialYouPalette(hue, animate: true);

                if (PanakManager.Instance.IsPopupOpen) {
                    PanakManager.Instance.UpdateOpenPopupThemeMode(_selectedThemeMode);
                }
                UpdateLivePreview(animate: true);
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (_isInitializing) {
                return;
            }

            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string langCode) {
                LocalizationService.Instance.CurrentLanguage = langCode;
                ApplyLocalization();

                try {
                    SettingsService.Instance.Current.Language = langCode;
                    SettingsService.Instance.SaveSettings();

                    string exe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                    if (!string.IsNullOrEmpty(exe) && ContextMenuRegistry.IsRegistered()) {
                        ContextMenuRegistry.RegisterAll(exe, langCode);
                    }
                } catch { }
            }
        }

        private void GridColumnsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (GridColumnsValueText != null) {
                GridColumnsValueText.Text = ((int)e.NewValue).ToString();
            }
            UpdateLivePreview();
        }

        private void FocusDimmingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (FocusDimmingValueText != null) {
                FocusDimmingValueText.Text = $"{(int)e.NewValue}%";
            }
            UpdateFocusModePreviewVisuals(animate: false);
        }

        private void FocusBlurSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (FocusBlurValueText != null) {
                FocusBlurValueText.Text = $"{(int)e.NewValue} px";
            }
            UpdateFocusModePreviewVisuals(animate: false);
        }

        private void UpdateFocusModePreviewVisuals(bool animate = false) {
            if (PreviewFocusDimOverlay == null || PreviewFocusWallpaperBlurFx == null) return;

            bool focusOn = FocusModeCheckBox?.IsChecked ?? true;
            bool shouldShowFocus = focusOn && _isPreviewPopupOpen;

            double targetDimming = 0.0;
            double targetBlur = 0.0;

            if (shouldShowFocus) {
                double dimPct = FocusDimmingSlider != null ? FocusDimmingSlider.Value : 45.0;
                targetDimming = Math.Clamp(dimPct / 100.0, 0.0, 1.0);

                bool blurOn = FocusBlurCheckBox?.IsChecked ?? true;
                if (blurOn) {
                    double blurRad = FocusBlurSlider != null ? FocusBlurSlider.Value : 28.0;
                    targetBlur = Math.Clamp(blurRad, 0.0, 100.0);
                }
            }

            if (animate) {
                var duration = TimeSpan.FromMilliseconds(220);
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                var dimAnim = new DoubleAnimation(PreviewFocusDimOverlay.Opacity, targetDimming, duration) { EasingFunction = ease };
                var blurAnim = new DoubleAnimation(PreviewFocusWallpaperBlurFx.Radius, targetBlur, duration) { EasingFunction = ease };
                PreviewFocusDimOverlay.BeginAnimation(OpacityProperty, dimAnim);
                PreviewFocusWallpaperBlurFx.BeginAnimation(BlurEffect.RadiusProperty, blurAnim);
            } else {
                PreviewFocusDimOverlay.BeginAnimation(OpacityProperty, null);
                PreviewFocusWallpaperBlurFx.BeginAnimation(BlurEffect.RadiusProperty, null);
                PreviewFocusDimOverlay.Opacity = targetDimming;
                PreviewFocusWallpaperBlurFx.Radius = targetBlur;
            }
        }

        private void UpdateFocusModeControlsEnabled(bool animate = false) {
            bool focusOn = FocusModeCheckBox?.IsChecked ?? true;
            bool blurOn = FocusBlurCheckBox?.IsChecked ?? true;

            if (FocusDimmingSlider != null) FocusDimmingSlider.IsEnabled = focusOn;
            if (FocusBlurCheckBox != null) FocusBlurCheckBox.IsEnabled = focusOn;

            SetFocusDimmingSliderVisible(focusOn, animate);
            SetFocusBlurSliderVisible(focusOn && blurOn, animate);
        }

        private void SetFocusDimmingSliderVisible(bool visible, bool animate) {
            if (FocusDimmingContainer == null) return;

            if (!animate) {
                FocusDimmingContainer.BeginAnimation(UIElement.OpacityProperty, null);
                FocusDimmingContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, null);

                if (visible) {
                    FocusDimmingContainer.Visibility = Visibility.Visible;
                    FocusDimmingContainer.Opacity = 1.0;
                    FocusDimmingContainer.MaxHeight = double.PositiveInfinity;
                    if (FocusDimmingSlider != null) FocusDimmingSlider.IsEnabled = true;
                } else {
                    FocusDimmingContainer.Visibility = Visibility.Collapsed;
                    FocusDimmingContainer.Opacity = 0.0;
                    FocusDimmingContainer.MaxHeight = 0;
                    if (FocusDimmingSlider != null) FocusDimmingSlider.IsEnabled = false;
                }
                return;
            }

            var ease = new System.Windows.Media.Animation.CubicEase {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut
            };

            if (visible) {
                FocusDimmingContainer.Visibility = Visibility.Visible;
                if (FocusDimmingSlider != null) FocusDimmingSlider.IsEnabled = true;

                FocusDimmingContainer.Measure(new Size(
                    FocusDimmingContainer.ActualWidth > 0 ? FocusDimmingContainer.ActualWidth : 350,
                    double.PositiveInfinity));
                double targetHeight = FocusDimmingContainer.DesiredSize.Height > 0
                    ? FocusDimmingContainer.DesiredSize.Height
                    : 65.0;

                double fromHeight = FocusDimmingContainer.ActualHeight;
                if (fromHeight <= 0 || double.IsNaN(fromHeight)) {
                    fromHeight = 0;
                }

                var heightAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = fromHeight,
                    To = targetHeight,
                    Duration = TimeSpan.FromMilliseconds(240),
                    EasingFunction = ease
                };

                var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = FocusDimmingContainer.Opacity,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(240),
                    EasingFunction = ease
                };

                heightAnim.Completed += (s, e) => {
                    FocusDimmingContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
                    FocusDimmingContainer.MaxHeight = double.PositiveInfinity;
                };

                FocusDimmingContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, heightAnim);
                FocusDimmingContainer.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            } else {
                if (FocusDimmingSlider != null) FocusDimmingSlider.IsEnabled = false;

                double fromHeight = FocusDimmingContainer.ActualHeight;
                if (fromHeight <= 0 || double.IsNaN(fromHeight)) {
                    FocusDimmingContainer.Measure(new Size(
                        FocusDimmingContainer.ActualWidth > 0 ? FocusDimmingContainer.ActualWidth : 350,
                        double.PositiveInfinity));
                    fromHeight = FocusDimmingContainer.DesiredSize.Height > 0
                        ? FocusDimmingContainer.DesiredSize.Height
                        : 65.0;
                }

                var heightAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = fromHeight,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = ease
                };

                var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = FocusDimmingContainer.Opacity,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = ease
                };

                heightAnim.Completed += (s, e) => {
                    bool focusOn = FocusModeCheckBox?.IsChecked ?? true;
                    if (!focusOn) {
                        FocusDimmingContainer.Visibility = Visibility.Collapsed;
                        FocusDimmingContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
                        FocusDimmingContainer.MaxHeight = 0;
                    }
                };

                FocusDimmingContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, heightAnim);
                FocusDimmingContainer.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            }
        }

        private void SetFocusBlurSliderVisible(bool visible, bool animate) {
            if (FocusBlurSliderContainer == null) return;

            if (!animate) {
                FocusBlurSliderContainer.BeginAnimation(UIElement.OpacityProperty, null);
                FocusBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, null);

                if (visible) {
                    FocusBlurSliderContainer.Visibility = Visibility.Visible;
                    FocusBlurSliderContainer.Opacity = 1.0;
                    FocusBlurSliderContainer.MaxHeight = double.PositiveInfinity;
                    if (FocusBlurSlider != null) FocusBlurSlider.IsEnabled = true;
                } else {
                    FocusBlurSliderContainer.Visibility = Visibility.Collapsed;
                    FocusBlurSliderContainer.Opacity = 0.0;
                    FocusBlurSliderContainer.MaxHeight = 0;
                    if (FocusBlurSlider != null) FocusBlurSlider.IsEnabled = false;
                }
                return;
            }

            var ease = new System.Windows.Media.Animation.CubicEase {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut
            };

            if (visible) {
                FocusBlurSliderContainer.Visibility = Visibility.Visible;
                if (FocusBlurSlider != null) FocusBlurSlider.IsEnabled = true;

                FocusBlurSliderContainer.Measure(new Size(
                    FocusBlurSliderContainer.ActualWidth > 0 ? FocusBlurSliderContainer.ActualWidth : 350,
                    double.PositiveInfinity));
                double targetHeight = FocusBlurSliderContainer.DesiredSize.Height > 0
                    ? FocusBlurSliderContainer.DesiredSize.Height
                    : 65.0;

                double fromHeight = FocusBlurSliderContainer.ActualHeight;
                if (fromHeight <= 0 || double.IsNaN(fromHeight)) {
                    fromHeight = 0;
                }

                var heightAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = fromHeight,
                    To = targetHeight,
                    Duration = TimeSpan.FromMilliseconds(240),
                    EasingFunction = ease
                };

                var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = FocusBlurSliderContainer.Opacity,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(240),
                    EasingFunction = ease
                };

                heightAnim.Completed += (s, e) => {
                    FocusBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
                    FocusBlurSliderContainer.MaxHeight = double.PositiveInfinity;
                };

                FocusBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, heightAnim);
                FocusBlurSliderContainer.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            } else {
                if (FocusBlurSlider != null) FocusBlurSlider.IsEnabled = false;

                double fromHeight = FocusBlurSliderContainer.ActualHeight;
                if (fromHeight <= 0 || double.IsNaN(fromHeight)) {
                    FocusBlurSliderContainer.Measure(new Size(
                        FocusBlurSliderContainer.ActualWidth > 0 ? FocusBlurSliderContainer.ActualWidth : 350,
                        double.PositiveInfinity));
                    fromHeight = FocusBlurSliderContainer.DesiredSize.Height > 0
                        ? FocusBlurSliderContainer.DesiredSize.Height
                        : 65.0;
                }

                var heightAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = fromHeight,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = ease
                };

                var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = FocusBlurSliderContainer.Opacity,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = ease
                };

                heightAnim.Completed += (s, e) => {
                    bool focusOn = FocusModeCheckBox?.IsChecked ?? true;
                    bool blurOn = FocusBlurCheckBox?.IsChecked ?? true;
                    if (!focusOn || !blurOn) {
                        FocusBlurSliderContainer.Visibility = Visibility.Collapsed;
                        FocusBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
                        FocusBlurSliderContainer.MaxHeight = 0;
                    }
                };

                FocusBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, heightAnim);
                FocusBlurSliderContainer.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            }
        }

        private void FolderBlurSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (FolderBlurValueText != null) {
                FolderBlurValueText.Text = $"{(int)e.NewValue} px";
            }
            UpdateLivePreview();
        }

        private void GridRowsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (GridRowsValueText != null) {
                GridRowsValueText.Text = ((int)e.NewValue).ToString();
            }
            UpdateLivePreview();
        }

        public void SetPagingMode(bool isSlider) {
            _isSliderMode = isSlider;
            UpdatePagingModeCardsVisual();
            UpdatePagingControlsVisibility();
            UpdateLivePreview();
        }

        private void SliderModeCard_Click(object sender, MouseButtonEventArgs e) {
            SetPagingMode(true);
        }

        private void ScrollModeCard_Click(object sender, MouseButtonEventArgs e) {
            SetPagingMode(false);
        }

        private void UpdatePagingModeCardsVisual() {
            if (SliderModeCard == null || ScrollModeCard == null) return;

            var primaryBrush = TryFindResource("M3PrimaryBrush") as Brush ?? Brushes.DarkOrange;
            var capsuleBg = TryFindResource("ShapeCapsuleBgBrush") as Brush ?? Brushes.PeachPuff;
            var outlineBrush = TryFindResource("M3OutlineBrush") as Brush ?? Brushes.Gray;
            var cardBrush = TryFindResource("M3CardBrush") as Brush ?? Brushes.White;

            if (_isSliderMode) {
                SliderModeCard.BorderBrush = primaryBrush;
                SliderModeCard.BorderThickness = new Thickness(2);
                SliderModeCard.Background = capsuleBg;
                if (SliderCheckBadge != null) SliderCheckBadge.Visibility = Visibility.Visible;

                ScrollModeCard.BorderBrush = outlineBrush;
                ScrollModeCard.BorderThickness = new Thickness(1);
                ScrollModeCard.Background = cardBrush;
                if (ScrollCheckBadge != null) ScrollCheckBadge.Visibility = Visibility.Collapsed;
            } else {
                ScrollModeCard.BorderBrush = primaryBrush;
                ScrollModeCard.BorderThickness = new Thickness(2);
                ScrollModeCard.Background = capsuleBg;
                if (ScrollCheckBadge != null) ScrollCheckBadge.Visibility = Visibility.Visible;

                SliderModeCard.BorderBrush = outlineBrush;
                SliderModeCard.BorderThickness = new Thickness(1);
                SliderModeCard.Background = cardBrush;
                if (SliderCheckBadge != null) SliderCheckBadge.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdatePagingControlsVisibility() {
            bool isSlider = _isSliderMode;
            if (GridRowsContainer != null) GridRowsContainer.Visibility = isSlider ? Visibility.Visible : Visibility.Collapsed;
            if (SliderInteractionsContainer != null) SliderInteractionsContainer.Visibility = isSlider ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateFolderBlurControlsEnabled(bool animate = false) {
            bool blurOn = FolderBlurCheckBox?.IsChecked ?? true;
            SetFolderBlurSliderVisible(blurOn, animate);
        }

        private void SetFolderBlurSliderVisible(bool visible, bool animate) {
            if (FolderBlurSliderContainer == null) return;

            if (!animate) {
                FolderBlurSliderContainer.BeginAnimation(UIElement.OpacityProperty, null);
                FolderBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, null);

                if (visible) {
                    FolderBlurSliderContainer.Visibility = Visibility.Visible;
                    FolderBlurSliderContainer.Opacity = 1.0;
                    FolderBlurSliderContainer.MaxHeight = double.PositiveInfinity;
                    if (FolderBlurSlider != null) FolderBlurSlider.IsEnabled = true;
                } else {
                    FolderBlurSliderContainer.Visibility = Visibility.Collapsed;
                    FolderBlurSliderContainer.Opacity = 0.0;
                    FolderBlurSliderContainer.MaxHeight = 0;
                    if (FolderBlurSlider != null) FolderBlurSlider.IsEnabled = false;
                }
                return;
            }

            var ease = new System.Windows.Media.Animation.CubicEase {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut
            };

            if (visible) {
                FolderBlurSliderContainer.Visibility = Visibility.Visible;
                if (FolderBlurSlider != null) FolderBlurSlider.IsEnabled = true;

                FolderBlurSliderContainer.Measure(new Size(
                    FolderBlurSliderContainer.ActualWidth > 0 ? FolderBlurSliderContainer.ActualWidth : 350,
                    double.PositiveInfinity));
                double targetHeight = FolderBlurSliderContainer.DesiredSize.Height > 0
                    ? FolderBlurSliderContainer.DesiredSize.Height
                    : 65.0;

                double fromHeight = FolderBlurSliderContainer.ActualHeight;
                if (fromHeight <= 0 || double.IsNaN(fromHeight)) {
                    fromHeight = 0;
                }

                var heightAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = fromHeight,
                    To = targetHeight,
                    Duration = TimeSpan.FromMilliseconds(240),
                    EasingFunction = ease
                };

                var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = FolderBlurSliderContainer.Opacity,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(240),
                    EasingFunction = ease
                };

                heightAnim.Completed += (s, e) => {
                    FolderBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
                    FolderBlurSliderContainer.MaxHeight = double.PositiveInfinity;
                };

                FolderBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, heightAnim);
                FolderBlurSliderContainer.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            } else {
                if (FolderBlurSlider != null) FolderBlurSlider.IsEnabled = false;

                double fromHeight = FolderBlurSliderContainer.ActualHeight;
                if (fromHeight <= 0 || double.IsNaN(fromHeight)) {
                    FolderBlurSliderContainer.Measure(new Size(
                        FolderBlurSliderContainer.ActualWidth > 0 ? FolderBlurSliderContainer.ActualWidth : 350,
                        double.PositiveInfinity));
                    fromHeight = FolderBlurSliderContainer.DesiredSize.Height > 0
                        ? FolderBlurSliderContainer.DesiredSize.Height
                        : 65.0;
                }

                var heightAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = fromHeight,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = ease
                };

                var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation {
                    From = FolderBlurSliderContainer.Opacity,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = ease
                };

                heightAnim.Completed += (s, e) => {
                    bool blurOn = FolderBlurCheckBox?.IsChecked ?? true;
                    if (!blurOn) {
                        FolderBlurSliderContainer.Visibility = Visibility.Collapsed;
                        FolderBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, null);
                        FolderBlurSliderContainer.MaxHeight = 0;
                    }
                };

                FolderBlurSliderContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, heightAnim);
                FolderBlurSliderContainer.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            }
        }

        private void GeneralFolderOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (GeneralFolderOpacityValueText != null) {
                GeneralFolderOpacityValueText.Text = $"{(int)e.NewValue}%";
            }
            if (!_isInitializing && PanakManager.Instance.IsPopupOpen) {
                PanakManager.Instance.UpdateOpenPopupOpacity(e.NewValue / 100.0);
            }
            UpdateLivePreview();
        }

        public void ApplyCurrentSettings() {
            var s = SettingsService.Instance.Current;

            if (LanguageComboBox.SelectedItem is ComboBoxItem langItem && langItem.Tag is string lang) {
                s.Language = lang;
            }

            s.IconShape = _selectedShape;
            s.ThemeColor = _selectedColor;
            s.ThemeMode = _selectedThemeMode;
            s.FolderOpacity = GeneralFolderOpacitySlider.Value / 100.0;

            s.GridColumns = (int)GridColumnsSlider.Value;
            s.GridRows = (int)GridRowsSlider.Value;
            s.FolderBlurEnabled = FolderBlurCheckBox.IsChecked ?? true;
            s.FolderBlurRadius = FolderBlurSlider.Value;
            s.PagingMode = _isSliderMode ? "Slider" : "Scrollbar";
            s.SliderArrowsEnabled = SliderArrowsCheckBox.IsChecked ?? true;
            s.SliderDragEnabled = SliderDragCheckBox.IsChecked ?? true;
            s.FocusModeEnabled = FocusModeCheckBox.IsChecked ?? true;
            s.FocusDimmingOpacity = FocusDimmingSlider.Value / 100.0;
            s.FocusBlurEnabled = FocusBlurCheckBox.IsChecked ?? true;
            s.FocusBlurRadius = FocusBlurSlider.Value;
            s.IsLocked = LockFoldersCheckBox.IsChecked ?? false;
            if (LockPopupPositionCheckBox != null) s.LockPopupPosition = LockPopupPositionCheckBox.IsChecked ?? false;
            s.OpenInCenterOnly = CenterPopupCheckBox.IsChecked ?? false;
            s.RightDoubleClickEnabled = RightDoubleClickCheckBox.IsChecked ?? true;
            s.SpaceQuickLookEnabled = SpaceQuickLookCheckBox.IsChecked ?? true;
            s.QuickLookHotkey = _currentQuickLookHotkey;
            s.ClosePopupHotkey = _currentClosePopupHotkey;
            s.CombineFilesOnDrop = CombineFilesCheckBox.IsChecked ?? true;
            s.AutoStart = AutoStartCheckBox.IsChecked ?? false;
            s.AutoCheckUpdates = AutoCheckUpdatesCheckBox.IsChecked ?? true;
            s.GitHubRepo = GitHubUpdateService.TargetRepository;

            if (RandomShapeCheckBox != null) s.RandomShapeOnCreate = RandomShapeCheckBox.IsChecked ?? false;
            if (RandomColorCheckBox != null) s.RandomColorOnCreate = RandomColorCheckBox.IsChecked ?? false;
            if (RandomOpacityCheckBox != null) s.RandomOpacityOnCreate = RandomOpacityCheckBox.IsChecked ?? false;

            // Handle auto-start setting
            string exe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            InstallerService.SetAutoStart(exe, s.AutoStart);

            SettingsService.Instance.SaveSettings(s);
            PanakManager.Instance.UpdateOpenPopupThemeColor(s.ThemeColor);
            PanakManager.Instance.UpdateOpenPopupThemeMode(s.ThemeMode);
            PanakManager.Instance.UpdateOpenPopupOpacity(s.FolderOpacity);
            PanakManager.Instance.UpdateOpenPopupSettings();
            if (PanakManager.Instance.IsPopupOpen) {
                if (s.FocusModeEnabled) {
                    PanakManager.Instance.ShowFocusBackdrops();
                } else {
                    PanakManager.Instance.HideFocusBackdrops();
                }
            }

            try {
                if (!string.IsNullOrEmpty(exe) && ContextMenuRegistry.IsRegistered()) {
                    ContextMenuRegistry.RegisterAll(exe, s.Language);
                }
            } catch { }

            _initialColor = _selectedColor;
            _initialThemeMode = _selectedThemeMode;
        }

        private void ApplyBtn_Click(object sender, RoutedEventArgs e) {
            ApplyCurrentSettings();
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e) {
            ApplyCurrentSettings();
            Close();
        }

        private void RandomShapeCheckBox_Click(object sender, RoutedEventArgs e) {
            var s = SettingsService.Instance.Current;
            s.RandomShapeOnCreate = RandomShapeCheckBox.IsChecked ?? false;
            SettingsService.Instance.SaveSettings(s);
        }

        private void RandomColorCheckBox_Click(object sender, RoutedEventArgs e) {
            var s = SettingsService.Instance.Current;
            s.RandomColorOnCreate = RandomColorCheckBox.IsChecked ?? false;
            SettingsService.Instance.SaveSettings(s);
        }

        private void RandomOpacityCheckBox_Click(object sender, RoutedEventArgs e) {
            var s = SettingsService.Instance.Current;
            s.RandomOpacityOnCreate = RandomOpacityCheckBox.IsChecked ?? false;
            SettingsService.Instance.SaveSettings(s);
        }

        private void RandomShapeLabel_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                RandomShapeCheckBox.IsChecked = !(RandomShapeCheckBox.IsChecked ?? false);
                RandomShapeCheckBox_Click(RandomShapeCheckBox, new RoutedEventArgs());
            }
        }

        private void RandomColorLabel_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                RandomColorCheckBox.IsChecked = !(RandomColorCheckBox.IsChecked ?? false);
                RandomColorCheckBox_Click(RandomColorCheckBox, new RoutedEventArgs());
            }
        }

        private void RandomOpacityLabel_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                RandomOpacityCheckBox.IsChecked = !(RandomOpacityCheckBox.IsChecked ?? false);
                RandomOpacityCheckBox_Click(RandomOpacityCheckBox, new RoutedEventArgs());
            }
        }

        private void SettingsColorIndicator_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                var s = SettingsService.Instance.Current;
                if (!s.CustomPaletteColors.Contains(_selectedColor, StringComparer.OrdinalIgnoreCase)) {
                    s.CustomPaletteColors.Add(_selectedColor);
                    SettingsService.Instance.SaveSettings(s);
                    RebuildCustomSwatchesUI();
                }
            }
        }

        private void RebuildPresetSwatchesUI() {
            if (PresetSwatchesWrapPanel == null) return;
            PresetSwatchesWrapPanel.Children.Clear();
            var s = SettingsService.Instance.Current;
            string defaultPastel = ShellHelper.DefaultPastelColorHex;

            for (int i = 0; i < DefaultPresetColors.Length; i++) {
                var (hex, name) = DefaultPresetColors[i];
                if (i == 0) hex = defaultPastel;

                if (s.HiddenPresetColors != null && s.HiddenPresetColors.Contains(hex, StringComparer.OrdinalIgnoreCase)) {
                    continue;
                }

                var container = CreateSwatchContainer(hex, name, isCustom: false);
                if (i == 0 && container.Children.Count > 0 && container.Children[0] is RadioButton rb) {
                    SettingsAccentSwatch = rb;
                }
                PresetSwatchesWrapPanel.Children.Add(container);
            }
        }

        private void RebuildCustomSwatchesUI() {
            if (CustomSwatchesWrapPanel == null) return;
            CustomSwatchesWrapPanel.Children.Clear();
            var s = SettingsService.Instance.Current;
            if (s.CustomPaletteColors == null || s.CustomPaletteColors.Count == 0) return;

            foreach (var hex in s.CustomPaletteColors) {
                string colHex = hex;
                var container = CreateSwatchContainer(colHex, colHex, isCustom: true);
                CustomSwatchesWrapPanel.Children.Add(container);
            }
        }

        private Grid CreateSwatchContainer(string colHex, string tooltip, bool isCustom) {
            var grid = new Grid {
                Width = 28,
                Height = 28,
                Margin = new Thickness(3, 0, 3, 0),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand
            };

            var rb = new RadioButton {
                GroupName = "SettingsColorGroup",
                Style = (Style)FindResource("ColorSwatchStyle"),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colHex)),
                Tag = colHex,
                ToolTip = tooltip,
                Margin = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (string.Equals(_selectedColor, colHex, StringComparison.OrdinalIgnoreCase)) {
                rb.IsChecked = true;
            }

            void OnSwatchSelected() {
                if (_isInitializing) return;
                SelectThemeColor(colHex, animate: true);
            }

            rb.Checked += (snd, ev) => OnSwatchSelected();
            rb.Click += (snd, ev) => OnSwatchSelected();

            grid.Children.Add(rb);

            // Delete badge in top-right corner
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

            grid.MouseEnter += (s, e) => {
                deleteBadge.Visibility = Visibility.Visible;
            };
            grid.MouseLeave += (s, e) => {
                deleteBadge.Visibility = Visibility.Collapsed;
            };

            deleteBadge.MouseEnter += (s, e) => {
                deleteBadge.Background = new SolidColorBrush(Color.FromRgb(229, 57, 53));
            };
            deleteBadge.MouseLeave += (s, e) => {
                deleteBadge.Background = new SolidColorBrush(Color.FromArgb(210, 30, 30, 30));
            };

            deleteBadge.PreviewMouseLeftButtonDown += (s, e) => {
                e.Handled = true; // Prevent checking the radio button
                var curSettings = SettingsService.Instance.Current;
                if (isCustom) {
                    curSettings.CustomPaletteColors.Remove(colHex);
                    SettingsService.Instance.SaveSettings(curSettings);
                    RebuildCustomSwatchesUI();
                } else {
                    if (!curSettings.HiddenPresetColors.Contains(colHex, StringComparer.OrdinalIgnoreCase)) {
                        curSettings.HiddenPresetColors.Add(colHex);
                        SettingsService.Instance.SaveSettings(curSettings);
                    }
                    grid.Visibility = Visibility.Collapsed;
                }

                if (string.Equals(_selectedColor, colHex, StringComparison.OrdinalIgnoreCase)) {
                    string defaultPastel = ShellHelper.DefaultPastelColorHex;
                    SelectThemeColor(defaultPastel, animate: true);
                    var firstVisible = GetAllColorSwatchRadioButtons().FirstOrDefault();
                    if (firstVisible != null) firstVisible.IsChecked = true;
                }
            };

            grid.Children.Add(deleteBadge);
            return grid;
        }

        private void RestorePresetsMenuItem_Click(object sender, RoutedEventArgs e) {
            var s = SettingsService.Instance.Current;
            s.HiddenPresetColors.Clear();
            SettingsService.Instance.SaveSettings(s);
            RebuildPresetSwatchesUI();
        }

        private IEnumerable<RadioButton> GetAllColorSwatchRadioButtons() {
            if (SettingsColorSwatchesPanel == null) yield break;
            foreach (var child in SettingsColorSwatchesPanel.Children) {
                if (child is RadioButton rb) {
                    yield return rb;
                } else if (child is Panel p) {
                    foreach (var pChild in p.Children) {
                        if (pChild is RadioButton prb) {
                            yield return prb;
                        } else if (pChild is Grid pg && pg.Children.Count > 0 && pg.Children[0] is RadioButton pgrb) {
                            if (pg.Visibility == Visibility.Visible) yield return pgrb;
                        }
                    }
                }
            }
        }

        private void ExportSettingsBtn_Click(object sender, RoutedEventArgs e) {
            var loc = LocalizationService.Instance;
            var dlg = new Microsoft.Win32.SaveFileDialog {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                FileName = "panak_settings.json",
                DefaultExt = ".json"
            };
            if (dlg.ShowDialog() == true) {
                if (SettingsService.Instance.ExportSettings(dlg.FileName)) {
                    MessageBox.Show(loc["SettingsExported"] ?? "Settings exported successfully.", "Panak", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void ImportSettingsBtn_Click(object sender, RoutedEventArgs e) {
            var loc = LocalizationService.Instance;
            var dlg = new Microsoft.Win32.OpenFileDialog {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = ".json"
            };
            if (dlg.ShowDialog() == true) {
                if (SettingsService.Instance.ImportSettings(dlg.FileName)) {
                    MessageBox.Show(loc["SettingsImported"] ?? "Settings imported successfully.", "Panak", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadCurrentSettings();
                    ApplyLocalization();
                    PanakManager.Instance.UpdateOpenPopupSettings();
                } else {
                    MessageBox.Show(loc["InvalidSettingsFile"] ?? "Invalid settings file.", "Panak", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateUpdateUI() {
            var updater = GitHubUpdateService.Instance;
            var loc = LocalizationService.Instance;

            UpdateStatusTitleText.Text = $"{loc["CurrentVersion"]}: v{updater.CurrentVersionString}";

            switch (updater.Status) {
                case UpdateStatus.Checking:
                    UpdateStatusDetailText.Text = loc["CheckingForUpdates"];
                    UpdateStatusBadge.Text = "⏳";
                    CheckUpdatesBtn.IsEnabled = false;
                    UpdateAvailableCard.Visibility = Visibility.Collapsed;
                    break;
                case UpdateStatus.UpdateAvailable:
                    UpdateStatusDetailText.Text = $"{loc["UpdateAvailable"]}: v{updater.LatestVersionString}";
                    UpdateStatusBadge.Text = "★ " + loc["UpdateAvailable"];
                    CheckUpdatesBtn.IsEnabled = true;
                    UpdateAvailableCard.Visibility = Visibility.Visible;
                    NewVersionAvailableText.Text = $"v{updater.LatestVersionString} - {updater.ReleaseTitle ?? "Release"}";
                    NewVersionDateText.Text = updater.PublishedAt.HasValue ? updater.PublishedAt.Value.ToLocalTime().ToString("d MMM yyyy") : "";
                    ReleaseNotesContentText.Text = string.IsNullOrWhiteSpace(updater.ReleaseNotes) ? "(No release notes)" : updater.ReleaseNotes;
                    DownloadInstallBtn.IsEnabled = true;
                    DownloadInstallBtn.Content = loc["DownloadAndInstall"];
                    break;
                case UpdateStatus.Downloading:
                    UpdateStatusDetailText.Text = loc["DownloadingUpdate"];
                    UpdateStatusBadge.Text = $"{updater.DownloadProgress:0}%";
                    CheckUpdatesBtn.IsEnabled = false;
                    UpdateAvailableCard.Visibility = Visibility.Visible;
                    UpdateProgressBar.Visibility = Visibility.Visible;
                    UpdateProgressBar.Value = updater.DownloadProgress;
                    DownloadInstallBtn.IsEnabled = false;
                    DownloadInstallBtn.Content = $"{loc["DownloadingUpdate"]} {updater.DownloadProgress:0}%";
                    break;
                case UpdateStatus.ReadyToInstall:
                    UpdateStatusDetailText.Text = loc["ReadyToInstall"] ?? "Ready to install";
                    UpdateStatusBadge.Text = "✓ " + (loc["ReadyToInstall"] ?? "Ready");
                    CheckUpdatesBtn.IsEnabled = true;
                    UpdateAvailableCard.Visibility = Visibility.Visible;
                    UpdateProgressBar.Visibility = Visibility.Visible;
                    UpdateProgressBar.Value = 100;
                    DownloadInstallBtn.IsEnabled = true;
                    DownloadInstallBtn.Content = loc["InstallNow"] ?? loc["Install"] ?? "Install Now";
                    break;
                case UpdateStatus.Error:
                    UpdateStatusDetailText.Text = updater.ErrorMessage ?? loc["UpdateError"];
                    UpdateStatusBadge.Text = "⚠ " + loc["UpdateError"];
                    CheckUpdatesBtn.IsEnabled = true;
                    UpdateProgressBar.Visibility = Visibility.Collapsed;
                    DownloadInstallBtn.IsEnabled = true;
                    DownloadInstallBtn.Content = loc["DownloadAndInstall"];
                    break;
                case UpdateStatus.UpToDate:
                default:
                    UpdateStatusDetailText.Text = loc["UpToDate"];
                    UpdateStatusBadge.Text = "✓ " + loc["LatestVersion"];
                    CheckUpdatesBtn.IsEnabled = true;
                    UpdateAvailableCard.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void OnUpdateStatusChanged(UpdateStatus status, string? msg) {
            Dispatcher.Invoke(UpdateUpdateUI);
        }

        private void OnDownloadProgressChanged(double progress) {
            Dispatcher.Invoke(() => {
                UpdateProgressBar.Visibility = Visibility.Visible;
                UpdateProgressBar.Value = progress;
                DownloadInstallBtn.Content = $"{LocalizationService.Instance["DownloadingUpdate"]} {progress:0}%";
            });
        }

        private async void CheckUpdatesBtn_Click(object sender, RoutedEventArgs e) {
            SettingsService.Instance.Current.GitHubRepo = GitHubUpdateService.TargetRepository;
            SettingsService.Instance.Save();

            CheckUpdatesBtn.IsEnabled = false;
            UpdateStatusDetailText.Text = LocalizationService.Instance["CheckingForUpdates"];

            // Concurrently refresh OTA translations
            _ = Task.Run(async () => {
                try {
                    bool otaUpdated = await LocalizationService.Instance.RefreshOtaTranslationsAsync();
                    if (otaUpdated) {
                        Dispatcher.Invoke(() => ApplyLocalization());
                    }
                } catch { }
            });

            await GitHubUpdateService.Instance.CheckForUpdatesAsync(manual: true);
            UpdateUpdateUI();
        }

        private async void DownloadInstallBtn_Click(object sender, RoutedEventArgs e) {
            var updater = GitHubUpdateService.Instance;

            // If update is already downloaded and ready to install, DO NOT re-download! Apply it immediately!
            if (updater.Status == UpdateStatus.ReadyToInstall && !string.IsNullOrEmpty(updater.DownloadedFilePath) && System.IO.File.Exists(updater.DownloadedFilePath)) {
                DownloadInstallBtn.IsEnabled = false;
                DownloadInstallBtn.Content = LocalizationService.Instance["Installing"] ?? "Installing...";
                bool launched = updater.ApplyDownloadedUpdate();
                if (!launched) {
                    DownloadInstallBtn.IsEnabled = true;
                    UpdateUpdateUI();
                }
                return;
            }

            // Otherwise, start download
            DownloadInstallBtn.IsEnabled = false;
            UpdateProgressBar.Visibility = Visibility.Visible;
            UpdateProgressBar.Value = 0;

            bool downloadSuccess = await updater.DownloadUpdateAsync(p => {
                Dispatcher.Invoke(() => UpdateProgressBar.Value = p);
            });

            if (downloadSuccess) {
                // Immediately apply downloaded update
                bool launched = updater.ApplyDownloadedUpdate();
                if (!launched) {
                    DownloadInstallBtn.IsEnabled = true;
                    UpdateUpdateUI();
                }
            } else {
                DownloadInstallBtn.IsEnabled = true;
                UpdateUpdateUI();
            }
        }

        private void KofiButton_Click(object sender, RoutedEventArgs e) {
            try {
                Process.Start(new ProcessStartInfo("https://ko-fi.com/hranthakobian/") { UseShellExecute = true });
            } catch { }
        }

        private void CrowdinButton_Click(object sender, RoutedEventArgs e) {
            try {
                Process.Start(new ProcessStartInfo("https://crowdin.com/project/panak") { UseShellExecute = true });
            } catch { }
        }

        private void CrowdinLink_MouseDown(object sender, MouseButtonEventArgs e) {
            try {
                Process.Start(new ProcessStartInfo("https://crowdin.com/project/panak") { UseShellExecute = true });
            } catch { }
        }

        private void EmailLink_MouseDown(object sender, MouseButtonEventArgs e) {
            try {
                Process.Start(new ProcessStartInfo("mailto:dev@hakobian.am") { UseShellExecute = true });
            } catch { }
        }

        private void WebsiteLink_MouseDown(object sender, MouseButtonEventArgs e) {
            try {
                Process.Start(new ProcessStartInfo("https://hakobian.am/contact") { UseShellExecute = true });
            } catch { }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e) {
            if (PanakManager.Instance.IsPopupOpen) {
                PanakManager.Instance.UpdateOpenPopupThemeColor(_initialColor);
                PanakManager.Instance.UpdateOpenPopupThemeMode(_initialThemeMode);
            }
            try {
                double initialHue = ShellHelper.GetHueFromHex(_initialColor);
                bool initialDark = _initialThemeMode == "Dark" || (_initialThemeMode == "Auto" && ShellHelper.IsWindowsDarkTheme());
                TrayManager.Instance?.UpdateTrayIconTheme(initialHue, initialDark);
            } catch { }
            _isAppliedOrSaved = true;
            Close();
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);
            try {
                if (_liveWallpaperTimer != null) {
                    _liveWallpaperTimer.Stop();
                    _liveWallpaperTimer = null;
                }
                if (_shapeMorphTimer != null) {
                    _shapeMorphTimer.Stop();
                    _shapeMorphTimer = null;
                }
            } catch { }
            if (!_isAppliedOrSaved) {
                try {
                    double initialHue = ShellHelper.GetHueFromHex(_initialColor);
                    bool initialDark = _initialThemeMode == "Dark" || (_initialThemeMode == "Auto" && ShellHelper.IsWindowsDarkTheme());
                    TrayManager.Instance?.UpdateTrayIconTheme(initialHue, initialDark);
                } catch { }
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                DragMove();
                UpdateWallpaperCrop();
            }
        }

        private void ReRegisterContextBtn_Click(object sender, RoutedEventArgs e) {
            string exe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            ContextMenuRegistry.RegisterAll(exe, LocalizationService.Instance.CurrentLanguage);
            ApplyLocalization();
            MessageBox.Show(LocalizationService.Instance["ContextMenuInstalled"], "Panak", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UninstallAppBtn_Click(object sender, RoutedEventArgs e) {
            try {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string localAppDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
                    "Panak");

                string[] candidates = {
                    Path.Combine(baseDir, "Uninstall.exe"),
                    Path.Combine(localAppDir, "Uninstall.exe"),
                    Path.Combine(baseDir, "..", "..", "..", "..", "Uninstall", "bin", "Debug", "net8.0-windows10.0.19041.0", "Uninstall.exe"),
                    Path.Combine(baseDir, "..", "..", "..", "..", "publish", "Uninstall.exe"),
                    Path.Combine(baseDir, "Setup.exe"),
                    Path.Combine(localAppDir, "Setup.exe")
                };

                foreach (var candidate in candidates) {
                    if (File.Exists(candidate)) {
                        Process.Start(new ProcessStartInfo {
                            FileName = Path.GetFullPath(candidate),
                            Arguments = candidate.EndsWith("Setup.exe", StringComparison.OrdinalIgnoreCase) ? "--uninstall" : "",
                            UseShellExecute = true
                        });
                        Close();
                        return;
                    }
                }

                new InstallerWindow(isUninstallMode: true).Show();
                Close();
            } catch (Exception ex) {
                MessageBox.Show($"Could not launch uninstaller: {ex.Message}", "Panak", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RenderKeyBadges(StackPanel container, string hotkey, string target) {
            container.Children.Clear();

            bool isRecording = (_recordingTarget == target);
            if (isRecording) {
                var recBtn = new Button {
                    Style = (Style)FindResource("KeycapBadgeButtonStyle"),
                    Content = LocalizationService.Instance["PressAnyKey"],
                    Tag = target,
                    BorderThickness = new Thickness(2),
                    BorderBrush = (Brush)FindResource("M3PrimaryBrush"),
                    Background = (Brush)FindResource("M3SurfaceContainerBrush")
                };
                recBtn.Click += KeyBadgeButton_Click;
                container.Children.Add(recBtn);
                return;
            }

            var parts = hotkey.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) parts = new[] { "None" };

            for (int i = 0; i < parts.Length; i++) {
                if (i > 0) {
                    var plusText = new TextBlock {
                        Text = "+",
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Foreground = (Brush)FindResource("M3OnSurfaceVariantBrush"),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0, 4, 0)
                    };
                    container.Children.Add(plusText);
                }

                string keyName = parts[i];
                if (keyName.Equals("Escape", StringComparison.OrdinalIgnoreCase)) keyName = "Esc";
                else if (keyName.Equals("Return", StringComparison.OrdinalIgnoreCase)) keyName = "Enter";

                var btn = new Button {
                    Style = (Style)FindResource("KeycapBadgeButtonStyle"),
                    Content = keyName,
                    Tag = target
                };
                btn.Click += KeyBadgeButton_Click;
                container.Children.Add(btn);
            }
        }

        private void KeyBadgeButton_Click(object sender, RoutedEventArgs e) {
            if (sender is Button btn && btn.Tag is string target) {
                if (_recordingTarget == target) {
                    _recordingTarget = null;
                } else {
                    _recordingTarget = target;
                }
                UpdateKeyBadgesUI();
            }
        }

        private void ResetKeysBtn_Click(object sender, RoutedEventArgs e) {
            _recordingTarget = null;
            _currentQuickLookHotkey = "Space";
            _currentClosePopupHotkey = "Escape";
            UpdateKeyBadgesUI();
        }

        private void UpdateKeyBadgesUI() {
            if (QuickLookKeysContainer != null) {
                RenderKeyBadges(QuickLookKeysContainer, _currentQuickLookHotkey, "QuickLook");
            }
            if (ClosePopupKeysContainer != null) {
                RenderKeyBadges(ClosePopupKeysContainer, _currentClosePopupHotkey, "ClosePopup");
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (SettingsLockCardContainer != null && SettingsLockCardContainer.Visibility == Visibility.Visible &&
                SettingsLockOverlay != null && SettingsLockOverlay.Visibility == Visibility.Visible) {
                if (SettingsLockOverlay.HandlePreviewKeyDown(e)) {
                    return;
                }
            }

            if (_recordingTarget == null) {
                if (e.Key == Key.Escape) {
                    CancelBtn_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                return;
            }

            Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin) {
                return;
            }

            var parts = new List<string>();
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) parts.Add("Ctrl");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) parts.Add("Alt");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) parts.Add("Shift");

            string keyStr = key.ToString();
            if (key == Key.Escape) keyStr = "Escape";
            else if (key == Key.Space) keyStr = "Space";

            parts.Add(keyStr);
            string finalHotkey = string.Join(" + ", parts);

            if (_recordingTarget == "QuickLook") {
                _currentQuickLookHotkey = finalHotkey;
            } else if (_recordingTarget == "ClosePopup") {
                _currentClosePopupHotkey = finalHotkey;
            }

            _recordingTarget = null;
            UpdateKeyBadgesUI();
            e.Handled = true;
        }

        private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e) {
            if (SettingsLockCardContainer != null && SettingsLockCardContainer.Visibility == Visibility.Visible &&
                SettingsLockOverlay != null && SettingsLockOverlay.Visibility == Visibility.Visible) {
                if (SettingsLockOverlay.HandleTextInput(e.Text)) {
                    e.Handled = true;
                }
            }
        }

        private static bool IsDescendantOf(DependencyObject? child, DependencyObject? parent) {
            if (child == null || parent == null) return false;
            while (child != null) {
                if (child == parent) return true;
                child = VisualTreeHelper.GetParent(child);
            }
            return false;
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
            if (_isPreviewMoreMenuOpen && PreviewMoreActionsMenuBorder != null) {
                var src = e.OriginalSource as DependencyObject;
                bool isInsideMenu = IsDescendantOf(src, PreviewMoreActionsMenuBorder);
                bool isMoreBtn = IsDescendantOf(src, PreviewMoreButton);
                if (!isInsideMenu && !isMoreBtn) {
                    ClosePreviewMoreActionsMenuAnimated();
                }
            }

            if (_recordingTarget != null) {
                var source = e.OriginalSource as DependencyObject;
                bool isInside = false;
                while (source != null) {
                    if (source is Button b && (string)b.Tag == _recordingTarget) {
                        isInside = true;
                        break;
                    }
                    source = VisualTreeHelper.GetParent(source);
                }
                if (!isInside) {
                    _recordingTarget = null;
                    UpdateKeyBadgesUI();
                }
            }
        }

        // --- Navigation Tabs & Smooth Scrolling ---
        private bool _isProgrammaticScrolling = false;
        private bool _isPreviewDirty = false;

        private void NavTab_Click(object sender, RoutedEventArgs e) {
            if (sender is RadioButton tab) {
                FrameworkElement? target = tab.Name switch {
                    "NavTabAppearance" => SectionAppearance,
                    "NavTabLayout" => SectionLayout,
                    "NavTabSecurity" => SectionSecurity,
                    "NavTabSystem" => SectionSystem,
                    _ => null
                };
                if (target != null && SettingsScrollViewer != null) {
                    try {
                        if (tab.Name == "NavTabAppearance") {
                            AnimateScrollViewer(0);
                            return;
                        }
                        var transform = target.TransformToVisual(SettingsScrollViewer);
                        var position = transform.Transform(new Point(0, 0));
                        double targetOffset = SettingsScrollViewer.VerticalOffset + position.Y - 10;
                        if (targetOffset < 0) targetOffset = 0;
                        if (targetOffset > SettingsScrollViewer.ScrollableHeight) targetOffset = SettingsScrollViewer.ScrollableHeight;
                        AnimateScrollViewer(targetOffset);
                    } catch { }
                }
            }
        }

        private void AnimateScrollViewer(double toOffset) {
            if (SettingsScrollViewer == null) return;
            _isProgrammaticScrolling = true;
            double fromOffset = SettingsScrollViewer.VerticalOffset;
            int steps = 15;
            int currentStep = 0;
            var timer = new System.Windows.Threading.DispatcherTimer {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            timer.Tick += (s, e) => {
                currentStep++;
                double progress = (double)currentStep / steps;
                double eased = 1.0 - Math.Pow(1.0 - progress, 3);
                double newOffset = fromOffset + (toOffset - fromOffset) * eased;
                SettingsScrollViewer.ScrollToVerticalOffset(newOffset);
                if (currentStep >= steps) {
                    SettingsScrollViewer.ScrollToVerticalOffset(toOffset);
                    timer.Stop();
                    _isProgrammaticScrolling = false;
                }
            };
            timer.Start();
        }

        private void SettingsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) {
            if (SettingsScrollViewer == null) return;

            if (_isProgrammaticScrolling) return;

            try {
                // Calculate relative positions of each section header to determine active tab
                double viewTop = 60.0; // trigger line below header
                FrameworkElement[] sections = { SectionSystem, SectionSecurity, SectionLayout, SectionAppearance };
                RadioButton[] tabs = { NavTabSystem, NavTabSecurity, NavTabLayout, NavTabAppearance };

                for (int i = 0; i < sections.Length; i++) {
                    var sec = sections[i];
                    var tab = tabs[i];
                    if (sec != null && tab != null) {
                        var transform = sec.TransformToVisual(SettingsScrollViewer);
                        var pos = transform.Transform(new Point(0, 0));
                        if (pos.Y <= viewTop) {
                            if (tab.IsChecked != true) {
                                tab.IsChecked = true;
                            }
                            break;
                        }
                    }
                }
            } catch { }
        }

        // --- Preview Toggle & Resize ---
        private bool _isPreviewVisible = true;

        private void TogglePreviewBtn_Click(object sender, RoutedEventArgs e) {
            _isPreviewVisible = !_isPreviewVisible;
            var loc = LocalizationService.Instance;
            if (_isPreviewVisible) {
                if (PreviewCardBorder != null) PreviewCardBorder.Visibility = Visibility.Visible;
                if (PreviewZoomControlsGroup != null) PreviewZoomControlsGroup.Visibility = Visibility.Visible;
                if (SplitterColumn != null) SplitterColumn.Width = GridLength.Auto;
                if (PreviewSplitter != null) PreviewSplitter.Visibility = Visibility.Visible;
                if (PreviewColumn != null) {
                    PreviewColumn.MinWidth = 240;
                    PreviewColumn.MaxWidth = 460;
                    PreviewColumn.Width = new GridLength(340);
                }
                if (TogglePreviewBtnText != null) TogglePreviewBtnText.Text = loc["HidePreview"] ?? "Նախադիտում";
                AnimateWindowWidth(880);
                if (_isPreviewDirty) {
                    UpdateLivePreview();
                }
                UpdateWallpaperCrop();
            } else {
                if (PreviewCardBorder != null) PreviewCardBorder.Visibility = Visibility.Collapsed;
                if (PreviewZoomControlsGroup != null) PreviewZoomControlsGroup.Visibility = Visibility.Collapsed;
                if (SplitterColumn != null) SplitterColumn.Width = new GridLength(0);
                if (PreviewSplitter != null) PreviewSplitter.Visibility = Visibility.Collapsed;
                if (PreviewColumn != null) {
                    PreviewColumn.MinWidth = 0;
                    PreviewColumn.MaxWidth = 0;
                    PreviewColumn.Width = new GridLength(0);
                }
                if (TogglePreviewBtnText != null) TogglePreviewBtnText.Text = loc["ShowPreview"] ?? "Նախադիտում";
                AnimateWindowWidth(520);
            }
        }

        private void AnimateWindowWidth(double toWidth) {
            try {
                var anim = new System.Windows.Media.Animation.DoubleAnimation {
                    To = toWidth,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };
                BeginAnimation(WidthProperty, anim);
            } catch {
                Width = toWidth;
            }
        }

        private int _previewCurrentPage = 0;
        private int _previewTotalPages = 1;
        private double _previewPageWidth = 234.0;
        private bool _isPreviewPinned = false;
        private bool _isPreviewEditOpen = false;
        private bool _isPreviewPopupOpen = true;
        private bool _isDraggingPreviewSlider = false;
        private Point _previewDragStartPos;

        // Desktop Preview Zoom & Icon Dragging
        private double _desktopZoomScale = 1.0;
        private Point _desktopIconPos = new Point(-1, -1);
        private bool _isDraggingDesktopIcon = false;
        private bool _hasDraggedDesktopIcon = false;
        private Point _dragDesktopIconStartMouse;
        private Point _dragDesktopIconStartPos;

        private void PreviewCardBorder_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (PreviewClipRect != null) {
                PreviewClipRect.Rect = new Rect(0, 0, e.NewSize.Width, e.NewSize.Height);
            }
            ApplySettingsDesktopZoom();
            int cols = GridColumnsSlider != null ? (int)Math.Clamp(GridColumnsSlider.Value, 3, 8) : 3;
            int rows = GridRowsSlider != null ? (int)Math.Clamp(GridRowsSlider.Value, 1, 5) : 3;
            bool isSlider = _isSliderMode;
            UpdatePreviewCardDimensions(cols, rows, isSlider);
            UpdateWallpaperCrop();
        }

        // --- Live Desktop Preview System ---
        private BitmapSource? _fullWallpaper = null;
        private DispatcherTimer? _liveWallpaperTimer;

        private void StartWallpaperCapture() {
            try {
                _fullWallpaper = ShellHelper.GetDesktopWallpaperBitmap();
                UpdateWallpaperCrop();
            } catch { }

            this.LocationChanged -= OnSettingsWindowLocationChanged;
            this.LocationChanged += OnSettingsWindowLocationChanged;

            try {
                if (!ShellHelper.IsLiveWallpaperEngineRunning()) {
                    return;
                }
                _liveWallpaperTimer = new DispatcherTimer(DispatcherPriority.Background) {
                    Interval = TimeSpan.FromMilliseconds(250)
                };
                _liveWallpaperTimer.Tick += (s, e) => {
                    try {
                        if (!IsLoaded || !_isPreviewVisible) return;
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
            } catch { }
        }

        private void OnSettingsWindowLocationChanged(object? sender, EventArgs e) {
            if (IsLoaded && _isPreviewVisible) {
                UpdateWallpaperCrop();
            }
        }

        // --- Segmented Switchers Animated Pills ---
        private void ThemeSegmentGrid_SizeChanged(object sender, SizeChangedEventArgs e) {
            int idx = _selectedThemeMode == "Light" ? 0 : (_selectedThemeMode == "Dark" ? 1 : 2);
            AnimateThemePill(idx, immediate: true);
        }

        private void AnimateThemePill(int index, bool immediate = false) {
            if (ThemeIndicatorPill == null || ThemePillTranslate == null || ThemeSegmentGrid == null) return;
            double totalW = ThemeSegmentGrid.ActualWidth;
            if (totalW <= 0) totalW = 210;
            double pillW = totalW / 3.0;
            ThemeIndicatorPill.Width = pillW;
            double targetX = index * pillW;

            if (immediate) {
                ThemePillTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                ThemePillTranslate.X = targetX;
            } else {
                var anim = new DoubleAnimation(ThemePillTranslate.X, targetX, TimeSpan.FromMilliseconds(260)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                ThemePillTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
            }
        }

        private void AnimateSettingsShapePill(int index, bool immediate = false) {
            if (SettingsShapePill == null || SettingsShapePillTranslate == null) return;
            double targetX = index * 40.0 + 2.0;
            if (immediate) {
                SettingsShapePillTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                SettingsShapePillTranslate.X = targetX;
            } else {
                var anim = new DoubleAnimation(SettingsShapePillTranslate.X, targetX, TimeSpan.FromMilliseconds(260)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                SettingsShapePillTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
            }
        }

        private void PreviewShapeCapsuleGrid_SizeChanged(object sender, SizeChangedEventArgs e) {
            int idx = GetShapeIndex(_selectedShape);
            AnimatePreviewShapePill(idx, immediate: true);
        }

        private void AnimatePreviewShapePill(int index, bool immediate = false) {
            if (PreviewShapePill == null || PreviewShapePillTranslate == null || PreviewShapeCapsuleGrid == null) return;
            double totalW = PreviewShapeCapsuleGrid.ActualWidth;
            if (totalW <= 0) totalW = 180;
            double colW = totalW / 5.0;
            PreviewShapePill.Width = Math.Max(28, colW);
            double targetX = index * colW;
            if (immediate) {
                PreviewShapePillTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                PreviewShapePillTranslate.X = targetX;
            } else {
                var anim = new DoubleAnimation(PreviewShapePillTranslate.X, targetX, TimeSpan.FromMilliseconds(260)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                PreviewShapePillTranslate.BeginAnimation(TranslateTransform.XProperty, anim);
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
        private const int MORPH_POINT_COUNT = 64;
        private static readonly Dictionary<string, System.Windows.Point[]> _cachedShapePoints = new(StringComparer.OrdinalIgnoreCase);
        private DispatcherTimer? _shapeMorphTimer;
        private DateTime _shapeMorphStartTime;
        private System.Windows.Point[]? _morphStartPoints;
        private System.Windows.Point[]? _morphTargetPoints;
        private string _currentDisplayedShape = "Squircle";

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

            var poly = new List<System.Windows.Point>();
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
            if (PreviewDesktopIconShapePath == null) return;
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
            if (_morphStartPoints == null || _morphTargetPoints == null || PreviewDesktopIconShapePath == null) {
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
            PreviewDesktopIconShapePath.Data = sg;

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
                    PreviewDesktopIconShapePath.Data = finalGeom;
                }
            }
        }

        private BitmapSource? _lastCroppedWallpaperSource;
        private Int32Rect _lastOuterCropRect;
        private Int32Rect _lastInnerCropRect;

        public void UpdateWallpaperCrop() {
            try {
                if (_fullWallpaper == null) {
                    _fullWallpaper = ShellHelper.GetDesktopWallpaperBitmap();
                    if (_fullWallpaper == null) return;
                }

                if (PreviewCardBorder == null || !IsLoaded || !_isPreviewVisible) {
                    if (PreviewWallpaperImage != null && PreviewWallpaperImage.Source != _fullWallpaper) {
                        PreviewWallpaperImage.Source = _fullWallpaper;
                    }
                    if (PreviewPopupBlurImage != null && PreviewPopupBlurImage.Source != _fullWallpaper) {
                        PreviewPopupBlurImage.Source = _fullWallpaper;
                    }
                    return;
                }

                double dpiScaleX = 1.0;
                double dpiScaleY = 1.0;
                try {
                    var dpi = VisualTreeHelper.GetDpi(this);
                    dpiScaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
                    dpiScaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;
                } catch { }

                Point screenPt = PreviewCardBorder.PointToScreen(new Point(0, 0));
                double pxW = (PreviewCardBorder.ActualWidth > 0 ? PreviewCardBorder.ActualWidth : 340) * dpiScaleX;
                double pxH = (PreviewCardBorder.ActualHeight > 0 ? PreviewCardBorder.ActualHeight : 600) * dpiScaleY;

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
                    double cardW = (PreviewCardBorderInner != null && PreviewCardBorderInner.Width > 20) ? PreviewCardBorderInner.Width : 258.0;
                    double cardH = (PreviewCardBorderInner != null && PreviewCardBorderInner.Height > 20) ? PreviewCardBorderInner.Height : 300.0;
                    double cardXInBorder = 12.0;
                    double cardYInBorder = Math.Max(0, (PreviewCardBorder.ActualHeight > 0 ? PreviewCardBorder.ActualHeight : 600) - 74.0 - cardH);

                    if (PreviewPopupContainer != null && PreviewCardBorder.IsVisible && PreviewPopupContainer.IsVisible) {
                        try {
                            var pt = PreviewPopupContainer.TranslatePoint(new Point(0, 0), PreviewCardBorder);
                            cardXInBorder = pt.X;
                            cardYInBorder = pt.Y;
                            cardW *= _desktopZoomScale;
                            cardH *= _desktopZoomScale;
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

                    var outerRect = new Int32Rect(cropX, cropY, cropW, cropH);
                    var innerRect = new Int32Rect(subX, subY, subW, subH);

                    if (ReferenceEquals(_lastCroppedWallpaperSource, _fullWallpaper) &&
                        _lastOuterCropRect == outerRect &&
                        _lastInnerCropRect == innerRect &&
                        PreviewWallpaperImage?.Source != null &&
                        PreviewPopupBlurImage?.Source != null) {
                        return;
                    }

                    _lastCroppedWallpaperSource = _fullWallpaper;
                    _lastOuterCropRect = outerRect;
                    _lastInnerCropRect = innerRect;

                    var cropped = new CroppedBitmap(_fullWallpaper, outerRect);
                    cropped.Freeze();
                    if (PreviewWallpaperImage != null) PreviewWallpaperImage.Source = cropped;

                    try {
                        if (subW > 20 && subH > 20) {
                            var cardCropped = new CroppedBitmap(_fullWallpaper, innerRect);
                            cardCropped.Freeze();
                            if (PreviewPopupBlurImage != null) PreviewPopupBlurImage.Source = cardCropped;
                        } else {
                            if (PreviewPopupBlurImage != null) PreviewPopupBlurImage.Source = cropped;
                        }
                    } catch {
                        if (PreviewPopupBlurImage != null) PreviewPopupBlurImage.Source = cropped;
                    }
                } else {
                    if (PreviewWallpaperImage != null) PreviewWallpaperImage.Source = _fullWallpaper;
                    if (PreviewPopupBlurImage != null) PreviewPopupBlurImage.Source = _fullWallpaper;
                }
            } catch {
                if (_fullWallpaper != null) {
                    if (PreviewWallpaperImage != null) PreviewWallpaperImage.Source = _fullWallpaper;
                    if (PreviewPopupBlurImage != null) PreviewPopupBlurImage.Source = _fullWallpaper;
                }
            }
        }

        private static ImageSource? _cachedFolderMockIcon = null;
        private static readonly Dictionary<string, ImageSource> _cachedMockIconSources = new(StringComparer.OrdinalIgnoreCase);

        private static ImageSource? GetPreviewMockIcon(bool isFolder, string ext) {
            try {
                if (isFolder) {
                    if (_cachedFolderMockIcon != null) return _cachedFolderMockIcon;
                    _cachedFolderMockIcon = ShellHelper.GetFolderIconSource(isLarge: true);
                    return _cachedFolderMockIcon;
                }
                if (!string.IsNullOrEmpty(ext)) {
                    if (_cachedMockIconSources.TryGetValue(ext, out var cached)) return cached;
                    var src = ShellHelper.GetFileIconSource("test" + ext, isLarge: true);
                    if (src != null) {
                        _cachedMockIconSources[ext] = src;
                        return src;
                    }
                }
            } catch { }
            return null;
        }

        // Realistic localized sample items matching real desktop folders (Image 1 parity)
        private static readonly (string Name, string Ext, bool IsFolder)[] SamplePreviewItemsArm = new[] {
            ("data", "", true),
            ("dist", "", true),
            ("node_modules", "", true),
            ("public", "", true),
            ("src", "", true),
            ("stitch_modern", "", true),
            ("test", "", true),
            ("views", "", true),
            (".env", ".env", false),
            ("Զեկույց.pdf", ".pdf", false),
            ("Աղյուսակ.xlsx", ".xlsx", false),
            ("Սլայդներ.pptx", ".pptx", false),
            ("Լոգո.png", ".png", false)
        };

        private static readonly (string Name, string Ext, bool IsFolder)[] SamplePreviewItemsEn = new[] {
            ("data", "", true),
            ("dist", "", true),
            ("node_modules", "", true),
            ("public", "", true),
            ("src", "", true),
            ("stitch_modern", "", true),
            ("test", "", true),
            ("views", "", true),
            (".env", ".env", false),
            ("Report.pdf", ".pdf", false),
            ("Spreadsheet.xlsx", ".xlsx", false),
            ("Slides.pptx", ".pptx", false),
            ("Logo.png", ".png", false)
        };

        private static readonly (string Name, string Ext, bool IsFolder)[] SamplePreviewItemsRu = new[] {
            ("data", "", true),
            ("dist", "", true),
            ("node_modules", "", true),
            ("public", "", true),
            ("src", "", true),
            ("stitch_modern", "", true),
            ("test", "", true),
            ("views", "", true),
            (".env", ".env", false),
            ("Отчет.pdf", ".pdf", false),
            ("Таблица.xlsx", ".xlsx", false),
            ("Презентация.pptx", ".pptx", false),
            ("Логотип.png", ".png", false)
        };

        private (string Name, string Ext, bool IsFolder)[] GetCurrentSampleItems() {
            string lang = LocalizationService.Instance.CurrentLanguage;
            if (string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase)) return SamplePreviewItemsEn;
            if (string.Equals(lang, "ru", StringComparison.OrdinalIgnoreCase)) return SamplePreviewItemsRu;
            return SamplePreviewItemsArm;
        }

        private int _lastPreviewCols = -1;
        private int _lastPreviewRows = -1;
        private bool? _lastPreviewIsSlider = null;
        private string? _lastPreviewLang = null;

        private void UpdateLivePreview(bool animate = false) {
            try {
                bool isDark = _selectedThemeMode == "Dark" || (_selectedThemeMode == "Auto" && ShellHelper.IsWindowsDarkTheme());
                System.Windows.Media.Color baseCol = ShellHelper.ParseWpfColorFast(_selectedColor, System.Windows.Media.Color.FromRgb(252, 221, 197));
                var animDuration = TimeSpan.FromMilliseconds(260);

                // Material 3 Dynamic shape capsule styling (used on both left panel and preview)
                double hue = ShellHelper.GetHueFromHex(_selectedColor);
                var (_, containerColorSys, primaryColorSys) = ShellHelper.GetMaterialYouTonalPalette(hue);
                var primaryColor = System.Windows.Media.Color.FromArgb(primaryColorSys.A, primaryColorSys.R, primaryColorSys.G, primaryColorSys.B);
                var containerColor = System.Windows.Media.Color.FromArgb(containerColorSys.A, containerColorSys.R, containerColorSys.G, containerColorSys.B);

                var capsuleBg = System.Windows.Media.Color.FromArgb(45, primaryColor.R, primaryColor.G, primaryColor.B);
                var unselectedBg = System.Windows.Media.Color.FromArgb(50, containerColor.R, containerColor.G, containerColor.B);

                void SetPreviewResBrush(string key, System.Windows.Media.Color col) {
                    if (animate) {
                        ShellHelper.AnimateResourceBrushColor(Resources, key, col, animDuration);
                    } else {
                        ShellHelper.SetResourceBrushColor(Resources, key, col);
                    }
                }

                SetPreviewResBrush("ShapeCapsuleBgBrush", capsuleBg);
                SetPreviewResBrush("ShapeUnselectedBgBrush", unselectedBg);
                SetPreviewResBrush("ShapeSelectedBgBrush", primaryColor);
                SetPreviewResBrush("ShapeUnselectedPathBrush", isDark ? System.Windows.Media.Colors.White : primaryColor);
                SetPreviewResBrush("ShapeSelectedPathBrush", System.Windows.Media.Colors.White);
                SetPreviewResBrush("CurrentHueThumbBrush", baseCol);

                if (!_isPreviewVisible) {
                    _isPreviewDirty = true;
                    return;
                }
                _isPreviewDirty = false;

                var textColor = isDark ? System.Windows.Media.Color.FromRgb(255, 255, 255) : System.Windows.Media.Color.FromRgb(0, 0, 0);
                var subtextColor = isDark ? System.Windows.Media.Color.FromRgb(255, 255, 255) : System.Windows.Media.Color.FromRgb(0, 0, 0);

                SetPreviewResBrush("PopupTextBrush", textColor);
                SetPreviewResBrush("PopupSubtextBrush", subtextColor);

                var borderCol = isDark ? System.Windows.Media.Color.FromArgb(56, 255, 255, 255) : System.Windows.Media.Color.FromArgb(60, 0, 0, 0);
                var bgCol = isDark ? System.Windows.Media.Color.FromArgb(1, 0, 0, 0) : System.Windows.Media.Color.FromArgb(1, 255, 255, 255);
                var fgBrushCol = isDark ? System.Windows.Media.Colors.White : System.Windows.Media.Colors.Black;

                if (PreviewCardBorderInner != null) {
                    if (animate) {
                        PreviewCardBorderInner.BorderBrush = ShellHelper.AnimateOrCreateBrush(PreviewCardBorderInner.BorderBrush, borderCol, animDuration);
                        PreviewCardBorderInner.Background = ShellHelper.AnimateOrCreateBrush(PreviewCardBorderInner.Background, bgCol, animDuration);
                    } else {
                        PreviewCardBorderInner.BorderBrush = ShellHelper.UpdateOrCreateBrush(PreviewCardBorderInner.BorderBrush, borderCol);
                        PreviewCardBorderInner.Background = ShellHelper.UpdateOrCreateBrush(PreviewCardBorderInner.Background, bgCol);
                    }
                }

                void UpdateLabelFg(TextBlock? tb) {
                    if (tb != null) {
                        if (animate) {
                            tb.Foreground = ShellHelper.AnimateOrCreateBrush(tb.Foreground, fgBrushCol, animDuration);
                        } else {
                            tb.Foreground = ShellHelper.UpdateOrCreateBrush(tb.Foreground, fgBrushCol);
                        }
                    }
                }
                UpdateLabelFg(PreviewFolderTitleText);
                UpdateLabelFg(PreviewEditNameLabel);
                UpdateLabelFg(PreviewColorThemeLabel);
                UpdateLabelFg(PreviewFolderLockHeaderLabel);
                if (PreviewAlwaysPanakCheckBox != null) {
                    if (animate) {
                        PreviewAlwaysPanakCheckBox.Foreground = ShellHelper.AnimateOrCreateBrush(PreviewAlwaysPanakCheckBox.Foreground, fgBrushCol, animDuration);
                    } else {
                        PreviewAlwaysPanakCheckBox.Foreground = ShellHelper.UpdateOrCreateBrush(PreviewAlwaysPanakCheckBox.Foreground, fgBrushCol);
                    }
                }

                if (isDark) {
                    SetPreviewResBrush("PopupHeaderBtnBg", System.Windows.Media.Color.FromArgb(37, 255, 255, 255));
                    SetPreviewResBrush("PopupHeaderBtnFg", System.Windows.Media.Colors.White);
                    SetPreviewResBrush("PopupItemBadgeBg", System.Windows.Media.Color.FromArgb(37, 255, 255, 255));
                    SetPreviewResBrush("PopupItemBadgeFg", System.Windows.Media.Colors.White);
                    SetPreviewResBrush("PopupItemHoverBrush", System.Windows.Media.Color.FromArgb(45, 255, 255, 255));
                } else {
                    SetPreviewResBrush("PopupHeaderBtnBg", System.Windows.Media.Color.FromArgb(25, 0, 0, 0));
                    SetPreviewResBrush("PopupHeaderBtnFg", System.Windows.Media.Colors.Black);
                    SetPreviewResBrush("PopupItemBadgeBg", System.Windows.Media.Color.FromArgb(20, 0, 0, 0));
                    SetPreviewResBrush("PopupItemBadgeFg", System.Windows.Media.Colors.Black);
                    SetPreviewResBrush("PopupItemHoverBrush", System.Windows.Media.Color.FromArgb(20, 0, 0, 0));
                }

                // Frosted Tint Layer (dark/light)
                bool hasBlur = FolderBlurCheckBox?.IsChecked ?? true;
                if (PreviewFrostedTintLayer != null) {
                    var frostedCol = isDark
                        ? (hasBlur ? System.Windows.Media.Color.FromArgb(194, 22, 22, 29) : System.Windows.Media.Color.FromArgb(235, 24, 24, 30))
                        : (hasBlur ? System.Windows.Media.Color.FromArgb(220, 248, 249, 250) : System.Windows.Media.Color.FromArgb(245, 248, 249, 250));
                    if (animate) {
                        PreviewFrostedTintLayer.Background = ShellHelper.AnimateOrCreateBrush(PreviewFrostedTintLayer.Background, frostedCol, animDuration);
                    } else {
                        PreviewFrostedTintLayer.Background = ShellHelper.UpdateOrCreateBrush(PreviewFrostedTintLayer.Background, frostedCol);
                    }
                }

                // Accent Tint Layer
                if (PreviewTintLayer != null) {
                    var tintColor = System.Windows.Media.Color.FromArgb(95, baseCol.R, baseCol.G, baseCol.B);
                    if (animate) {
                        PreviewTintLayer.Background = ShellHelper.AnimateOrCreateBrush(PreviewTintLayer.Background, tintColor, animDuration);
                    } else {
                        PreviewTintLayer.Background = ShellHelper.UpdateOrCreateBrush(PreviewTintLayer.Background, tintColor);
                    }
                }

                // Opacity syncing
                double opacityVal = GeneralFolderOpacitySlider != null ? GeneralFolderOpacitySlider.Value : 85.0;
                double normOpacity = Math.Clamp(opacityVal / 100.0, 0.20, 1.0);
                if (PreviewFrostedTintLayer != null) PreviewFrostedTintLayer.Opacity = normOpacity;
                if (PreviewTintLayer != null) PreviewTintLayer.Opacity = Math.Min(1.0, normOpacity * 1.1);

                // Blur effect
                if (PreviewPopupBlurEffect != null && FolderBlurSlider != null) {
                    double targetRadius = Math.Max(2, FolderBlurSlider.Value);
                    if (Math.Abs(PreviewPopupBlurEffect.Radius - targetRadius) > 0.1) {
                        PreviewPopupBlurEffect.Radius = targetRadius;
                    }
                }
                if (PreviewPopupBlurImage != null) {
                    var targetVis = hasBlur ? Visibility.Visible : Visibility.Collapsed;
                    if (PreviewPopupBlurImage.Visibility != targetVis) {
                        PreviewPopupBlurImage.Visibility = targetVis;
                    }
                }
                if (_isPreviewMoreMenuOpen) {
                    UpdatePreviewMoreActionsMenuTheme();
                }

                // Scrollbar thumb dynamic theme color
                var thumbColor = System.Windows.Media.Color.FromArgb(150, baseCol.R, baseCol.G, baseCol.B);
                var hoverColor = System.Windows.Media.Color.FromArgb(220, baseCol.R, baseCol.G, baseCol.B);
                ShellHelper.SetResourceBrushColor(Resources, "PanakScrollBarThumbBrush", thumbColor);
                ShellHelper.SetResourceBrushColor(Resources, "PanakScrollBarThumbHoverBrush", hoverColor);

                if (PreviewHueSlider != null && Math.Abs(PreviewHueSlider.Value - hue) > 1.0) {
                    _isSyncingHueSliders = true;
                    try {
                        PreviewHueSlider.Value = hue;
                    } finally {
                        _isSyncingHueSliders = false;
                    }
                }
                if (PreviewOpacitySlider != null && Math.Abs(PreviewOpacitySlider.Value - normOpacity) > 0.02) {
                    PreviewOpacitySlider.Value = normOpacity;
                }

                // Desktop folder icon at bottom left
                if (PreviewDesktopIconShapePath != null) {
                    if (string.Equals(_currentDisplayedShape, _selectedShape, StringComparison.OrdinalIgnoreCase)) {
                        string key = _selectedShape switch {
                            "Circle" => "GeomCircle",
                            "Clover" or "Star" => "GeomStar",
                            "Scallop" => "GeomScallop",
                            "Arch" or "Teardrop" => "GeomArch",
                            _ => "GeomSquircle"
                        };
                        if (TryFindResource(key) is Geometry geom && !ReferenceEquals(PreviewDesktopIconShapePath.Data, geom)) {
                            PreviewDesktopIconShapePath.Data = geom;
                        }
                    } else {
                        MorphPreviewDesktopIconShape(_selectedShape);
                    }

                    byte iconAlpha = (byte)Math.Clamp((int)Math.Round(normOpacity * 255.0), 30, 255);
                    var iconColor = System.Windows.Media.Color.FromArgb(iconAlpha, baseCol.R, baseCol.G, baseCol.B);
                    if (animate) {
                        PreviewDesktopIconShapePath.Fill = ShellHelper.AnimateOrCreateBrush(PreviewDesktopIconShapePath.Fill, iconColor, animDuration);
                    } else {
                        PreviewDesktopIconShapePath.Fill = ShellHelper.UpdateOrCreateBrush(PreviewDesktopIconShapePath.Fill, iconColor);
                    }
                    PreviewDesktopIconShapePath.Opacity = normOpacity;
                }

                UpdatePreviewPinVisual(updateDimensions: false);
                PopulatePreviewDesktopIcon();
                PopulatePreviewItems(force: false);
                UpdateFocusModePreviewVisuals(animate: false);
            } catch { }
        }

        private void PopulatePreviewDesktopIcon() {
            if (PreviewDesktopIconMiniGrid == null) return;
            if (PreviewDesktopIconMiniGrid.Children.Count == 0) {
                var samples = GetCurrentSampleItems();
                for (int i = 0; i < 4; i++) {
                    var item = samples[i % samples.Length];
                    var iconSrc = GetPreviewMockIcon(item.IsFolder, item.Ext);
                    var img = new Image {
                        Source = iconSrc,
                        Width = 12,
                        Height = 12,
                        Margin = new Thickness(1)
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    PreviewDesktopIconMiniGrid.Children.Add(img);
                }
            }
        }

        private Border CreatePreviewItemView((string Name, string Ext, bool IsFolder) sample) {
            var itemBorder = new Border {
                Width = 68,
                Height = 76,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Margin = new Thickness(5),
                Padding = new Thickness(2, 4, 2, 4),
                Cursor = Cursors.Hand
            };

            itemBorder.MouseEnter += (s, e) => {
                itemBorder.Background = TryFindResource("PopupItemHoverBrush") as Brush ?? new SolidColorBrush(System.Windows.Media.Color.FromArgb(45, 255, 255, 255));
            };
            itemBorder.MouseLeave += (s, e) => {
                itemBorder.Background = Brushes.Transparent;
            };

            var stack = new StackPanel {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 66
            };

            var iconGrid = new Grid {
                Width = 47,
                Height = 47,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var iconSrc = GetPreviewMockIcon(sample.IsFolder, sample.Ext);
            var img = new Image {
                Source = iconSrc,
                Width = 43,
                Height = 43,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            iconGrid.Children.Add(img);
            stack.Children.Add(iconGrid);

            var txt = new TextBlock {
                Text = sample.Name,
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 26,
                Margin = new Thickness(0, 4, 0, 0),
                ToolTip = sample.Name
            };
            txt.SetResourceReference(TextBlock.ForegroundProperty, "PopupTextBrush");
            stack.Children.Add(txt);
            itemBorder.Child = stack;
            return itemBorder;
        }

        private void PopulatePreviewItems(bool force = false) {
            var samples = GetCurrentSampleItems();
            int cols = GridColumnsSlider != null ? (int)Math.Clamp(GridColumnsSlider.Value, 3, 8) : 3;
            int rows = GridRowsSlider != null ? (int)Math.Clamp(GridRowsSlider.Value, 1, 5) : 3;
            bool isSlider = _isSliderMode;
            string currentLang = LocalizationService.Instance.CurrentLanguage;

            if (!force &&
                _lastPreviewCols == cols &&
                _lastPreviewRows == rows &&
                _lastPreviewIsSlider == isSlider &&
                string.Equals(_lastPreviewLang, currentLang, StringComparison.OrdinalIgnoreCase) &&
                PreviewSliderPagesPanel != null &&
                PreviewSliderPagesPanel.Children.Count > 0) {
                return;
            }

            _lastPreviewCols = cols;
            _lastPreviewRows = rows;
            _lastPreviewIsSlider = isSlider;
            _lastPreviewLang = currentLang;

            double itemW = 78.0;
            double itemH = 86.0;
            double pageW = cols * itemW;
            double pageH = rows * itemH;
            _previewPageWidth = pageW;

            int itemsPerPage = Math.Max(1, cols * rows);
            _previewTotalPages = (int)Math.Ceiling((double)samples.Length / itemsPerPage);
            if (_previewTotalPages < 1) _previewTotalPages = 1;
            if (_previewCurrentPage >= _previewTotalPages) _previewCurrentPage = _previewTotalPages - 1;
            if (_previewCurrentPage < 0) _previewCurrentPage = 0;

            // 1. Slider pages setup
            if (PreviewSliderPagesViewport != null && PreviewSliderPagesPanel != null) {
                PreviewSliderPagesViewport.Width = pageW;
                PreviewSliderPagesViewport.Height = pageH;
                PreviewSliderPagesPanel.Width = _previewTotalPages * pageW;
                PreviewSliderPagesPanel.Height = pageH;
                PreviewSliderPagesPanel.Children.Clear();

                for (int p = 0; p < _previewTotalPages; p++) {
                    var pageGrid = new UniformGrid {
                        Columns = cols,
                        Rows = rows,
                        Width = pageW,
                        Height = pageH,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Visibility = (p == _previewCurrentPage) ? Visibility.Visible : Visibility.Hidden
                    };
                    var pageItems = samples.Skip(p * itemsPerPage).Take(itemsPerPage);
                    foreach (var item in pageItems) {
                        pageGrid.Children.Add(CreatePreviewItemView(item));
                    }
                    PreviewSliderPagesPanel.Children.Add(pageGrid);
                }

                AnimatePreviewSliderPage(immediate: true);
            }

            // 2. ScrollViewer setup
            if (PreviewScrollGrid != null) {
                PreviewScrollGrid.Columns = cols;
                PreviewScrollGrid.Width = pageW;
                PreviewScrollGrid.Children.Clear();
                foreach (var item in samples) {
                    PreviewScrollGrid.Children.Add(CreatePreviewItemView(item));
                }
            }

            if (PreviewItemsScrollViewer != null) {
                PreviewItemsScrollViewer.MaxHeight = Math.Min(rows, 3) * itemH + 8;
            }

            // 3. Switch between Slider and Scrollbar mode
            if (PreviewSliderPagesViewport != null && PreviewItemsScrollViewer != null && PreviewFooterPagerGrid != null) {
                if (isSlider) {
                    PreviewSliderPagesViewport.Visibility = Visibility.Visible;
                    PreviewItemsScrollViewer.Visibility = Visibility.Collapsed;
                    PreviewFooterPagerGrid.Visibility = (_previewTotalPages > 1) ? Visibility.Visible : Visibility.Collapsed;
                    UpdatePreviewDots();
                } else {
                    PreviewSliderPagesViewport.Visibility = Visibility.Collapsed;
                    PreviewItemsScrollViewer.Visibility = Visibility.Visible;
                    PreviewFooterPagerGrid.Visibility = Visibility.Collapsed;
                }
            }

            // 4. Update count badge
            if (PreviewItemCountBadge != null) {
                string countFormatted = $"{samples.Length} {LocalizationService.Instance["FilesCount"]}";
                PreviewItemCountBadge.Text = countFormatted;
            }

            // 5. Update layout sizing and scaling
            UpdatePreviewCardDimensions(cols, rows, isSlider);
        }

        private void UpdatePreviewCardDimensions(int cols, int rows, bool isSlider) {
            if (PreviewCardBorderInner == null || PreviewInnerClippingBorder == null || PreviewCardClipGeometry == null) return;

            double naturalW = cols * 78.0 + 24.0;
            double naturalH = (rows * 86.0) + 36.0 /* header */ + (isSlider && _previewTotalPages > 1 ? 30.0 : 0.0) /* footer */ + 22.0 /* inner padding */;
            if (_isPreviewEditOpen && PreviewEditPanelContainer != null) {
                double editH = PreviewEditPanelContainer.ActualHeight > 0 ? PreviewEditPanelContainer.ActualHeight : 240.0;
                naturalH += editH;
            }

            PreviewCardBorderInner.Width = naturalW;
            PreviewCardBorderInner.Height = naturalH;
            PreviewInnerClippingBorder.Width = naturalW;
            PreviewInnerClippingBorder.Height = naturalH;
            PreviewCardClipGeometry.Rect = new Rect(0, 0, naturalW, naturalH);

            if (PreviewPopupBlurImage != null) {
                PreviewPopupBlurImage.Width = naturalW;
                PreviewPopupBlurImage.Height = naturalH;
            }

            if (PreviewPopupScale != null) {
                PreviewPopupScale.ScaleX = 1.0;
                PreviewPopupScale.ScaleY = 1.0;
            }

            PositionSettingsPreviewPopup();

            if (PreviewMoreButton != null && PreviewPinButton != null && PreviewEditButton != null && PreviewExplorerButton != null) {
                bool collapseToMore = (cols <= 3);
                if (collapseToMore) {
                    bool shouldShowPin = _isPreviewPinned;
                    if (shouldShowPin && PreviewPinButton.Visibility != Visibility.Visible) {
                        AnimatePreviewPinButtonAppearance();
                    } else if (!shouldShowPin && PreviewPinButton.Visibility == Visibility.Visible) {
                        AnimatePreviewPinButtonDisappearance();
                    } else if (!shouldShowPin) {
                        PreviewPinButton.Visibility = Visibility.Collapsed;
                    }
                    PreviewEditButton.Visibility = Visibility.Collapsed;
                    PreviewExplorerButton.Visibility = Visibility.Collapsed;
                    PreviewMoreButton.Visibility = Visibility.Visible;
                } else {
                    if (PreviewPinButton.Visibility != Visibility.Visible) {
                        AnimatePreviewPinButtonAppearance();
                    }
                    PreviewEditButton.Visibility = Visibility.Visible;
                    PreviewExplorerButton.Visibility = Visibility.Visible;
                    PreviewMoreButton.Visibility = Visibility.Collapsed;
                    ClosePreviewMoreActionsMenuImmediate();
                }
            }

            UpdateWallpaperCrop();
        }

        private void UpdatePreviewDots() {
            if (PreviewSliderDotsPanel == null) return;
            PreviewSliderDotsPanel.Children.Clear();
            if (_previewTotalPages <= 1) return;

            var textBrush = TryFindResource("PopupTextBrush") as Brush ?? Brushes.White;
            var subtextBrush = TryFindResource("PopupSubtextBrush") as Brush ?? Brushes.Gray;

            for (int i = 0; i < _previewTotalPages; i++) {
                int pageIndex = i;
                bool isActive = (i == _previewCurrentPage);
                var dot = new Border {
                    Width = isActive ? 20.0 : 7.0,
                    Height = 7.0,
                    CornerRadius = new CornerRadius(3.5),
                    Margin = new Thickness(2.5, 0, 2.5, 0),
                    Background = isActive ? textBrush : subtextBrush,
                    Opacity = isActive ? 0.95 : 0.45,
                    Cursor = Cursors.Hand,
                    Tag = pageIndex
                };
                dot.MouseLeftButtonDown += (s, e) => {
                    _previewCurrentPage = pageIndex;
                    AnimatePreviewSliderPage(immediate: false);
                    UpdatePreviewDots();
                };
                PreviewSliderDotsPanel.Children.Add(dot);
            }

            bool arrowsEnabled = SliderArrowsCheckBox?.IsChecked ?? true;
            if (PreviewSliderPrevBtn != null) {
                PreviewSliderPrevBtn.Visibility = arrowsEnabled ? Visibility.Visible : Visibility.Collapsed;
                PreviewSliderPrevBtn.IsEnabled = _previewCurrentPage > 0;
                PreviewSliderPrevBtn.Opacity = _previewCurrentPage > 0 ? 1.0 : 0.35;
            }
            if (PreviewSliderNextBtn != null) {
                PreviewSliderNextBtn.Visibility = arrowsEnabled ? Visibility.Visible : Visibility.Collapsed;
                PreviewSliderNextBtn.IsEnabled = _previewCurrentPage < _previewTotalPages - 1;
                PreviewSliderNextBtn.Opacity = _previewCurrentPage < _previewTotalPages - 1 ? 1.0 : 0.35;
            }
        }

        private void CullOffscreenPreviewSliderPages(bool showAllForTransition = false) {
            if (PreviewSliderPagesPanel == null) return;
            for (int i = 0; i < PreviewSliderPagesPanel.Children.Count; i++) {
                if (PreviewSliderPagesPanel.Children[i] is UIElement pageEl) {
                    var targetVis = (showAllForTransition || i == _previewCurrentPage) ? Visibility.Visible : Visibility.Hidden;
                    if (pageEl.Visibility != targetVis) {
                        pageEl.Visibility = targetVis;
                    }
                }
            }
        }

        private void AnimatePreviewSliderPage(bool immediate) {
            if (PreviewPagesTranslateX == null) return;
            double targetX = -_previewCurrentPage * _previewPageWidth;
            if (immediate) {
                PreviewPagesTranslateX.BeginAnimation(TranslateTransform.XProperty, null);
                PreviewPagesTranslateX.X = targetX;
                CullOffscreenPreviewSliderPages(showAllForTransition: false);
            } else {
                CullOffscreenPreviewSliderPages(showAllForTransition: true);
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                var anim = new DoubleAnimation {
                    To = targetX,
                    Duration = TimeSpan.FromMilliseconds(240),
                    EasingFunction = ease
                };
                anim.Completed += (s, e) => CullOffscreenPreviewSliderPages(showAllForTransition: false);
                PreviewPagesTranslateX.BeginAnimation(TranslateTransform.XProperty, anim);
            }
        }

        private void PreviewSliderPrevBtn_Click(object sender, RoutedEventArgs e) {
            if (_previewCurrentPage > 0) {
                _previewCurrentPage--;
                AnimatePreviewSliderPage(immediate: false);
                UpdatePreviewDots();
            }
        }

        private void PreviewSliderNextBtn_Click(object sender, RoutedEventArgs e) {
            if (_previewCurrentPage < _previewTotalPages - 1) {
                _previewCurrentPage++;
                AnimatePreviewSliderPage(immediate: false);
                UpdatePreviewDots();
            }
        }

        private void PreviewSlider_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                _isDraggingPreviewSlider = true;
                _previewDragStartPos = e.GetPosition(PreviewSliderPagesViewport);
                PreviewSliderPagesViewport?.CaptureMouse();
            }
        }

        private void PreviewSlider_MouseMove(object sender, MouseEventArgs e) {
            if (_isDraggingPreviewSlider && e.LeftButton == MouseButtonState.Pressed) {
                var currentPos = e.GetPosition(PreviewSliderPagesViewport);
                double deltaX = currentPos.X - _previewDragStartPos.X;
                if (Math.Abs(deltaX) > 40) {
                    _isDraggingPreviewSlider = false;
                    PreviewSliderPagesViewport?.ReleaseMouseCapture();
                    if (deltaX < 0 && _previewCurrentPage < _previewTotalPages - 1) {
                        _previewCurrentPage++;
                        AnimatePreviewSliderPage(immediate: false);
                        UpdatePreviewDots();
                    } else if (deltaX > 0 && _previewCurrentPage > 0) {
                        _previewCurrentPage--;
                        AnimatePreviewSliderPage(immediate: false);
                        UpdatePreviewDots();
                    }
                }
            }
        }

        private void PreviewSlider_MouseUp(object sender, MouseButtonEventArgs e) {
            _isDraggingPreviewSlider = false;
            PreviewSliderPagesViewport?.ReleaseMouseCapture();
        }

        private void PreviewSlider_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (_previewTotalPages <= 1) return;
            if (e.Delta < 0) {
                if (_previewCurrentPage < _previewTotalPages - 1) {
                    _previewCurrentPage++;
                    AnimatePreviewSliderPage(immediate: false);
                    UpdatePreviewDots();
                    e.Handled = true;
                }
            } else if (e.Delta > 0) {
                if (_previewCurrentPage > 0) {
                    _previewCurrentPage--;
                    AnimatePreviewSliderPage(immediate: false);
                    UpdatePreviewDots();
                    e.Handled = true;
                }
            }
        }

        private void PreviewScrollViewer_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (PreviewItemsScrollViewer != null) {
                if (e.Delta < 0) {
                    PreviewItemsScrollViewer.LineDown();
                } else {
                    PreviewItemsScrollViewer.LineUp();
                }
                e.Handled = true;
            }
        }

        private void AnimatePreviewPinButtonAppearance() {
            if (PreviewPinButton == null || PreviewPinButtonScale == null) return;
            PreviewPinButton.Visibility = Visibility.Visible;
            var ease = new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut };
            var opacityAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease };
            var scaleAnim = new DoubleAnimation(0.4, 1.0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease };
            PreviewPinButton.BeginAnimation(OpacityProperty, opacityAnim);
            PreviewPinButtonScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            PreviewPinButtonScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        private void AnimatePreviewPinButtonDisappearance() {
            if (PreviewPinButton == null || PreviewPinButtonScale == null) return;
            if (PreviewPinButton.Visibility != Visibility.Visible) return;
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            var opacityAnim = new DoubleAnimation(PreviewPinButton.Opacity, 0.0, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease };
            var scaleAnim = new DoubleAnimation(PreviewPinButtonScale.ScaleX, 0.4, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease };
            opacityAnim.Completed += (s, e) => {
                PreviewPinButton.Visibility = Visibility.Collapsed;
                PreviewPinButton.Opacity = 1.0;
                PreviewPinButtonScale.ScaleX = 1.0;
                PreviewPinButtonScale.ScaleY = 1.0;
            };
            PreviewPinButton.BeginAnimation(OpacityProperty, opacityAnim);
            PreviewPinButtonScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            PreviewPinButtonScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        private void PreviewPinButton_Click(object sender, RoutedEventArgs e) {
            _isPreviewPinned = !_isPreviewPinned;
            UpdatePreviewPinVisual(updateDimensions: true);
        }

        private void UpdatePreviewPinVisual(bool updateDimensions = true) {
            if (PreviewPinIconPath == null) return;
            var loc = LocalizationService.Instance;
            if (PreviewPinButton != null) {
                PreviewPinButton.ToolTip = _isPreviewPinned ? loc["Unpin"] : loc["Pin"];
            }
            if (PreviewMenuPinText != null) {
                PreviewMenuPinText.Text = _isPreviewPinned ? loc["Unpin"] : loc["Pin"];
            }
            if (_isPreviewPinned) {
                if (PreviewPinButton != null) {
                    PreviewPinButton.Background = ShellHelper.UpdateOrCreateBrush(PreviewPinButton.Background, System.Windows.Media.Color.FromArgb(90, 255, 255, 255));
                }
                PreviewPinIconPath.Fill = (Brush)FindResource("M3PrimaryBrush");
                if (PreviewMenuPinIconPath != null) {
                    PreviewMenuPinIconPath.Fill = (Brush)FindResource("M3PrimaryBrush");
                }
            } else {
                PreviewPinButton?.ClearValue(Button.BackgroundProperty);
                PreviewPinIconPath.Fill = (Brush)FindResource("PopupHeaderBtnFg");
                if (PreviewMenuPinIconPath != null) {
                    PreviewMenuPinIconPath.Fill = (Brush)FindResource("PopupHeaderBtnFg");
                }
            }
            if (updateDimensions) {
                int cols = GridColumnsSlider != null ? (int)Math.Clamp(GridColumnsSlider.Value, 3, 8) : 3;
                int rows = GridRowsSlider != null ? (int)Math.Clamp(GridRowsSlider.Value, 1, 5) : 3;
                UpdatePreviewCardDimensions(cols, rows, _isSliderMode);
            }
        }

        private bool _isPreviewMoreMenuOpen = false;

        private void OpenPreviewMoreActionsMenuAnimated() {
            if (PreviewMoreActionsMenuBorder == null) return;
            UpdatePreviewMoreActionsMenuTheme();
            _isPreviewMoreMenuOpen = true;
            PreviewMoreActionsMenuBorder.Visibility = Visibility.Visible;
            PreviewMoreActionsMenuBorder.IsHitTestVisible = true;

            if (PreviewMainContentGrid != null && (FolderBlurCheckBox?.IsChecked ?? true)) {
                var blur = new BlurEffect { Radius = 0, RenderingBias = RenderingBias.Performance };
                PreviewMainContentGrid.Effect = blur;
                var blurAnim = new DoubleAnimation(0.0, 10.0, TimeSpan.FromMilliseconds(200)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                blur.BeginAnimation(BlurEffect.RadiusProperty, blurAnim);
            }

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var opacityAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease };
            var scaleAnim = new DoubleAnimation(0.85, 1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease };

            PreviewMoreActionsMenuBorder.BeginAnimation(OpacityProperty, opacityAnim);
            PreviewMoreActionsMenuScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            PreviewMoreActionsMenuScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        private void ClosePreviewMoreActionsMenuAnimated() {
            if (PreviewMoreActionsMenuBorder == null || !_isPreviewMoreMenuOpen) {
                if (PreviewMoreActionsMenuBorder != null) {
                    PreviewMoreActionsMenuBorder.Visibility = Visibility.Collapsed;
                    PreviewMoreActionsMenuBorder.IsHitTestVisible = false;
                }
                _isPreviewMoreMenuOpen = false;
                if (PreviewMainContentGrid?.Effect != null) {
                    PreviewMainContentGrid.Effect = null;
                }
                return;
            }
            _isPreviewMoreMenuOpen = false;
            PreviewMoreActionsMenuBorder.IsHitTestVisible = false;

            if (PreviewMainContentGrid?.Effect is BlurEffect blur) {
                var blurAnim = new DoubleAnimation(blur.Radius, 0.0, TimeSpan.FromMilliseconds(160)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                blurAnim.Completed += (s, e) => {
                    if (!_isPreviewMoreMenuOpen) {
                        PreviewMainContentGrid.Effect = null;
                    }
                };
                blur.BeginAnimation(BlurEffect.RadiusProperty, blurAnim);
            }

            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            var opacityAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease };
            var scaleAnim = new DoubleAnimation(1.0, 0.85, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease };

            opacityAnim.Completed += (s, e) => {
                if (!_isPreviewMoreMenuOpen) {
                    PreviewMoreActionsMenuBorder.Visibility = Visibility.Collapsed;
                }
            };

            PreviewMoreActionsMenuBorder.BeginAnimation(OpacityProperty, opacityAnim);
            PreviewMoreActionsMenuScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            PreviewMoreActionsMenuScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        private void ClosePreviewMoreActionsMenuImmediate() {
            if (PreviewMoreActionsMenuBorder == null) return;
            _isPreviewMoreMenuOpen = false;
            if (PreviewMainContentGrid?.Effect != null) {
                PreviewMainContentGrid.Effect = null;
            }
            PreviewMoreActionsMenuBorder.BeginAnimation(OpacityProperty, null);
            PreviewMoreActionsMenuScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            PreviewMoreActionsMenuScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            PreviewMoreActionsMenuBorder.Opacity = 0;
            PreviewMoreActionsMenuScale.ScaleX = 0.85;
            PreviewMoreActionsMenuScale.ScaleY = 0.85;
            PreviewMoreActionsMenuBorder.Visibility = Visibility.Collapsed;
            PreviewMoreActionsMenuBorder.IsHitTestVisible = false;
        }

        private void PreviewMoreButton_Click(object sender, RoutedEventArgs e) {
            if (PreviewMoreActionsMenuBorder == null) return;
            if (_isPreviewMoreMenuOpen) {
                ClosePreviewMoreActionsMenuAnimated();
            } else {
                OpenPreviewMoreActionsMenuAnimated();
            }
            e.Handled = true;
        }

        private void PreviewMenuPin_Click(object sender, MouseButtonEventArgs e) {
            e.Handled = true;
            ClosePreviewMoreActionsMenuAnimated();
            PreviewPinButton_Click(sender, e);
        }

        private void PreviewMenuEdit_Click(object sender, MouseButtonEventArgs e) {
            e.Handled = true;
            ClosePreviewMoreActionsMenuAnimated();
            PreviewEditButton_Click(sender, e);
        }

        private void PreviewMenuExplorer_Click(object sender, MouseButtonEventArgs e) {
            e.Handled = true;
            ClosePreviewMoreActionsMenuAnimated();
            PreviewExplorerButton_Click(sender, e);
        }

        public void UpdatePreviewMoreActionsMenuTheme() {
            if (PreviewMoreActionsMenuBorder == null) return;
            try {
                bool isDark = _selectedThemeMode == "Dark" || (_selectedThemeMode == "Auto" && ShellHelper.IsWindowsDarkTheme());
                bool blurEnabled = FolderBlurCheckBox?.IsChecked ?? true;
                string hexColor = !string.IsNullOrEmpty(_selectedColor) ? _selectedColor : ShellHelper.DefaultPastelColorHex;
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
                double hue = ShellHelper.GetHueFromHex(hexColor);
                var (_, _, primaryHex) = ShellHelper.GetMaterialYouTonalPaletteHex(hue);
                var primaryColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(primaryHex);

                PreviewMoreActionsMenuBorder.BorderThickness = new Thickness(0);
                PreviewMoreActionsMenuBorder.BorderBrush = Brushes.Transparent;

                if (isDark) {
                    byte alpha = blurEnabled ? (byte)185 : (byte)235;
                    var menuBg = System.Windows.Media.Color.FromArgb(alpha, 
                        (byte)Math.Clamp(color.R * 0.22 + 34 * 0.78, 0, 255), 
                        (byte)Math.Clamp(color.G * 0.22 + 34 * 0.78, 0, 255), 
                        (byte)Math.Clamp(color.B * 0.22 + 42 * 0.78, 0, 255));
                    PreviewMoreActionsMenuBorder.Background = new SolidColorBrush(menuBg);
                    var lightFg = System.Windows.Media.Brushes.White;
                    if (PreviewMenuPinText != null) PreviewMenuPinText.Foreground = lightFg;
                    if (PreviewMenuEditText != null) PreviewMenuEditText.Foreground = lightFg;
                    if (PreviewMenuExplorerText != null) PreviewMenuExplorerText.Foreground = lightFg;
                    if (PreviewMenuPinIconPath != null) PreviewMenuPinIconPath.Fill = _isPreviewPinned ? (Brush)FindResource("M3PrimaryBrush") : lightFg;
                    if (PreviewMenuEditIconPath != null) PreviewMenuEditIconPath.Fill = lightFg;
                    if (PreviewMenuExplorerIconPath != null) PreviewMenuExplorerIconPath.Fill = lightFg;
                } else {
                    byte alpha = blurEnabled ? (byte)190 : (byte)245;
                    var menuBg = System.Windows.Media.Color.FromArgb(alpha, 
                        (byte)Math.Clamp(color.R * 0.18 + 248 * 0.82, 0, 255), 
                        (byte)Math.Clamp(color.G * 0.18 + 250 * 0.82, 0, 255), 
                        (byte)Math.Clamp(color.B * 0.18 + 252 * 0.82, 0, 255));
                    PreviewMoreActionsMenuBorder.Background = new SolidColorBrush(menuBg);
                    var darkFg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(28, 27, 31));
                    if (PreviewMenuPinText != null) PreviewMenuPinText.Foreground = darkFg;
                    if (PreviewMenuEditText != null) PreviewMenuEditText.Foreground = darkFg;
                    if (PreviewMenuExplorerText != null) PreviewMenuExplorerText.Foreground = darkFg;
                    if (PreviewMenuPinIconPath != null) PreviewMenuPinIconPath.Fill = _isPreviewPinned ? (Brush)FindResource("M3PrimaryBrush") : darkFg;
                    if (PreviewMenuEditIconPath != null) PreviewMenuEditIconPath.Fill = darkFg;
                    if (PreviewMenuExplorerIconPath != null) PreviewMenuExplorerIconPath.Fill = darkFg;
                }
            } catch { }
        }

        private void PreviewEditButton_Click(object sender, RoutedEventArgs e) {
            if (PreviewEditPanelContainer == null || PreviewEditPanel == null) return;

            _isPreviewEditOpen = !_isPreviewEditOpen;
            var duration = TimeSpan.FromMilliseconds(220);
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

            if (_isPreviewEditOpen) {
                PreviewEditPanelContainer.Visibility = Visibility.Visible;
                PreviewEditPanel.Measure(new Size(234, double.PositiveInfinity));
                double targetH = PreviewEditPanel.DesiredSize.Height > 0 ? PreviewEditPanel.DesiredSize.Height : 240.0;

                var hAnim = new DoubleAnimation(0.0, targetH, duration) { EasingFunction = ease };
                var opAnim = new DoubleAnimation(0.0, 1.0, duration) { EasingFunction = ease };
                var trAnim = new DoubleAnimation(-10.0, 0.0, duration) { EasingFunction = ease };

                PreviewEditPanelContainer.BeginAnimation(HeightProperty, hAnim);
                PreviewEditPanel.BeginAnimation(OpacityProperty, opAnim);
                PreviewEditPanelTranslate.BeginAnimation(TranslateTransform.YProperty, trAnim);
            } else {
                double startH = PreviewEditPanelContainer.ActualHeight > 0 ? PreviewEditPanelContainer.ActualHeight : PreviewEditPanelContainer.Height;
                var hAnim = new DoubleAnimation(startH, 0.0, duration) { EasingFunction = ease };
                var opAnim = new DoubleAnimation(1.0, 0.0, duration) { EasingFunction = ease };
                var trAnim = new DoubleAnimation(0.0, -10.0, duration) { EasingFunction = ease };

                hAnim.Completed += (s, ev) => {
                    if (!_isPreviewEditOpen) {
                        PreviewEditPanelContainer.Visibility = Visibility.Collapsed;
                    }
                };

                PreviewEditPanelContainer.BeginAnimation(HeightProperty, hAnim);
                PreviewEditPanel.BeginAnimation(OpacityProperty, opAnim);
                PreviewEditPanelTranslate.BeginAnimation(TranslateTransform.YProperty, trAnim);
            }

            int cols = GridColumnsSlider != null ? (int)Math.Clamp(GridColumnsSlider.Value, 3, 8) : 3;
            int rows = GridRowsSlider != null ? (int)Math.Clamp(GridRowsSlider.Value, 1, 5) : 3;
            bool isSlider = _isSliderMode;
            UpdatePreviewCardDimensions(cols, rows, isSlider);
        }

        private void PreviewExplorerButton_Click(object sender, RoutedEventArgs e) {
            try {
                var defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                ShellContextMenu.OpenFile(defaultPath);
            } catch { }
        }

        private void PreviewCloseButton_Click(object sender, RoutedEventArgs e) {
            if (PreviewPopupContainer == null || !_isPreviewPopupOpen) return;
            _isPreviewPopupOpen = false;
            UpdateFocusModePreviewVisuals(animate: true);

            var duration = TimeSpan.FromMilliseconds(220);
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

            var scaleXAnim = new DoubleAnimation(PreviewPopupScale.ScaleX, 0.0, duration) { EasingFunction = ease };
            var scaleYAnim = new DoubleAnimation(PreviewPopupScale.ScaleY, 0.0, duration) { EasingFunction = ease };
            var opAnim = new DoubleAnimation(1.0, 0.0, duration) { EasingFunction = ease };

            scaleXAnim.Completed += (s, ev) => {
                if (!_isPreviewPopupOpen) {
                    PreviewPopupContainer.Visibility = Visibility.Collapsed;
                    PreviewPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    PreviewPopupScale.ScaleX = 0.0;
                    PreviewPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    PreviewPopupScale.ScaleY = 0.0;
                    PreviewPopupContainer.BeginAnimation(OpacityProperty, null);
                    PreviewPopupContainer.Opacity = 0.0;
                }
            };

            PreviewPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            PreviewPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
            PreviewPopupContainer.BeginAnimation(OpacityProperty, opAnim);
        }

        private void ApplySettingsDesktopZoom() {
            if (PreviewDesktopCanvas == null || PreviewDesktopScaleTransform == null || PreviewCardBorder == null) return;
            double bW = PreviewCardBorder.ActualWidth > 0 ? PreviewCardBorder.ActualWidth : 340.0;
            double bH = PreviewCardBorder.ActualHeight > 0 ? PreviewCardBorder.ActualHeight : 600.0;
            double vW = bW / _desktopZoomScale;
            double vH = bH / _desktopZoomScale;

            PreviewDesktopCanvas.Width = vW;
            PreviewDesktopCanvas.Height = vH;
            PreviewDesktopScaleTransform.ScaleX = _desktopZoomScale;
            PreviewDesktopScaleTransform.ScaleY = _desktopZoomScale;

            PositionSettingsPreviewPopup();
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

        private void PositionSettingsPreviewPopup() {
            if (PreviewDesktopCanvas == null || PreviewDesktopIcon == null || PreviewPopupContainer == null) return;

            double canvasW = PreviewDesktopCanvas.Width > 0 ? PreviewDesktopCanvas.Width : (PreviewCardBorder?.ActualWidth > 0 ? PreviewCardBorder.ActualWidth : 340.0);
            double canvasH = PreviewDesktopCanvas.Height > 0 ? PreviewDesktopCanvas.Height : (PreviewCardBorder?.ActualHeight > 0 ? PreviewCardBorder.ActualHeight : 600.0);

            if (_desktopIconPos.X < 0) {
                _desktopIconPos = new Point(12.0, 12.0);
            }
            _desktopIconPos = SnapToDesktopGrid(_desktopIconPos.X, _desktopIconPos.Y, canvasW, canvasH);

            Canvas.SetLeft(PreviewDesktopIcon, _desktopIconPos.X);
            Canvas.SetTop(PreviewDesktopIcon, _desktopIconPos.Y);

            double popupW = (PreviewCardBorderInner != null && PreviewCardBorderInner.Width > 20) ? PreviewCardBorderInner.Width : 258.0;
            double popupH = (PreviewCardBorderInner != null && PreviewCardBorderInner.Height > 20) ? PreviewCardBorderInner.Height : 340.0;

            double iconLeft = _desktopIconPos.X;
            double iconTop = _desktopIconPos.Y;
            double iconRight = iconLeft + 52.0;
            double iconBottom = iconTop + 68.0;

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

            PreviewPopupContainer.RenderTransformOrigin = origin;
            Canvas.SetLeft(PreviewPopupContainer, targetLeft);
            Canvas.SetTop(PreviewPopupContainer, targetTop);
        }

        private void PreviewDesktop_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) {
                e.Handled = true;
                double step = e.Delta > 0 ? 0.05 : -0.05;
                _desktopZoomScale = Math.Clamp(_desktopZoomScale + step, 0.35, 1.50);
                ApplySettingsDesktopZoom();
                UpdateWallpaperCrop();
                UpdateZoomBadge();
            }
        }

        private void LivePreviewBadge_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 2 || e.ChangedButton == MouseButton.Middle) {
                _desktopZoomScale = 1.0;
                ApplySettingsDesktopZoom();
                UpdateWallpaperCrop();
                UpdateZoomBadge();
                e.Handled = true;
            }
        }

        private void PreviewZoomOutBtn_Click(object sender, RoutedEventArgs e) {
            _desktopZoomScale = Math.Clamp(_desktopZoomScale - 0.05, 0.35, 1.50);
            ApplySettingsDesktopZoom();
            UpdateWallpaperCrop();
            UpdateZoomBadge();
        }

        private void PreviewZoomInBtn_Click(object sender, RoutedEventArgs e) {
            _desktopZoomScale = Math.Clamp(_desktopZoomScale + 0.05, 0.35, 1.50);
            ApplySettingsDesktopZoom();
            UpdateWallpaperCrop();
            UpdateZoomBadge();
        }

        private void PreviewZoomResetBtn_Click(object sender, RoutedEventArgs e) {
            _desktopZoomScale = 1.0;
            ApplySettingsDesktopZoom();
            UpdateWallpaperCrop();
            UpdateZoomBadge();
        }

        private void UpdateZoomBadge() {
            if (PreviewZoomValueText != null) {
                PreviewZoomValueText.Text = $"{(int)Math.Round(_desktopZoomScale * 100)}%";
            }
        }

        private void PreviewDesktopCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (!_isPreviewPopupOpen) return;

            var source = e.OriginalSource as DependencyObject;
            if (source != null) {
                if (PreviewPopupContainer != null && (PreviewPopupContainer == source || PreviewPopupContainer.IsAncestorOf(source))) {
                    return;
                }
                if (PreviewDesktopIcon != null && (PreviewDesktopIcon == source || PreviewDesktopIcon.IsAncestorOf(source))) {
                    return;
                }
            }

            PreviewCloseButton_Click(this, new RoutedEventArgs());
        }

        private void PreviewDesktopIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (PreviewDesktopIcon == null || PreviewDesktopCanvas == null) return;
            _isDraggingDesktopIcon = true;
            _hasDraggedDesktopIcon = false;
            _dragDesktopIconStartMouse = e.GetPosition(PreviewDesktopCanvas);
            _dragDesktopIconStartPos = _desktopIconPos;
            PreviewDesktopIcon.CaptureMouse();
            e.Handled = true;
        }

        private void PreviewDesktopIcon_MouseMove(object sender, MouseEventArgs e) {
            if (_isDraggingDesktopIcon && PreviewDesktopIcon != null && PreviewDesktopCanvas != null) {
                var curMouse = e.GetPosition(PreviewDesktopCanvas);
                var delta = curMouse - _dragDesktopIconStartMouse;
                if (!_hasDraggedDesktopIcon && (Math.Abs(delta.X) > 3 || Math.Abs(delta.Y) > 3)) {
                    _hasDraggedDesktopIcon = true;
                }
                if (_hasDraggedDesktopIcon) {
                    double cW = PreviewDesktopCanvas.Width > 0 ? PreviewDesktopCanvas.Width : 340.0;
                    double cH = PreviewDesktopCanvas.Height > 0 ? PreviewDesktopCanvas.Height : 600.0;
                    _desktopIconPos = SnapToDesktopGrid(_dragDesktopIconStartPos.X + delta.X, _dragDesktopIconStartPos.Y + delta.Y, cW, cH);
                    PositionSettingsPreviewPopup();
                    UpdateWallpaperCrop();
                }
                e.Handled = true;
            }
        }

        private void PreviewDesktopIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (_isDraggingDesktopIcon && PreviewDesktopIcon != null) {
                _isDraggingDesktopIcon = false;
                PreviewDesktopIcon.ReleaseMouseCapture();
                if (!_hasDraggedDesktopIcon) {
                    ToggleSettingsPreviewPopup();
                }
                e.Handled = true;
            }
        }

        private void ToggleSettingsPreviewPopup() {
            if (PreviewPopupContainer == null) return;
            if (!_isPreviewPopupOpen) {
                OpenSettingsPreviewPopup();
            } else {
                PreviewCloseButton_Click(this, new RoutedEventArgs());
            }
        }

        private void OpenSettingsPreviewPopup() {
            if (PreviewPopupContainer == null) return;
            _isPreviewPopupOpen = true;
            UpdateFocusModePreviewVisuals(animate: true);
            PreviewPopupContainer.Visibility = Visibility.Visible;

            int cols = GridColumnsSlider != null ? (int)Math.Clamp(GridColumnsSlider.Value, 3, 8) : 3;
            int rows = GridRowsSlider != null ? (int)Math.Clamp(GridRowsSlider.Value, 1, 5) : 3;
            bool isSlider = _isSliderMode;
            UpdatePreviewCardDimensions(cols, rows, isSlider);
            PositionSettingsPreviewPopup();

            double targetScale = 1.0;

            PreviewPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            PreviewPopupScale.ScaleX = 0.0;
            PreviewPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            PreviewPopupScale.ScaleY = 0.0;
            PreviewPopupContainer.BeginAnimation(OpacityProperty, null);
            PreviewPopupContainer.Opacity = 0.0;

            UpdateWallpaperCrop();
            UpdateLivePreview();

            var duration = TimeSpan.FromMilliseconds(260);
            var ease = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut };

            var scaleXAnim = new DoubleAnimation(0.0, targetScale, duration) { EasingFunction = ease };
            var scaleYAnim = new DoubleAnimation(0.0, targetScale, duration) { EasingFunction = ease };
            var opAnim = new DoubleAnimation(0.0, 1.0, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };

            scaleXAnim.Completed += (s, ev) => {
                PreviewPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                PreviewPopupScale.ScaleX = targetScale;
                PreviewPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                PreviewPopupScale.ScaleY = targetScale;
                PreviewPopupContainer.BeginAnimation(OpacityProperty, null);
                PreviewPopupContainer.Opacity = 1.0;
                UpdateWallpaperCrop();
                UpdateLivePreview();
            };

            PreviewPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
            PreviewPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
            PreviewPopupContainer.BeginAnimation(OpacityProperty, opAnim);
        }

        private void PreviewSaveNameButton_Click(object sender, RoutedEventArgs e) {
            if (PreviewNameEditBox != null && PreviewFolderTitleText != null) {
                PreviewFolderTitleText.Text = PreviewNameEditBox.Text;
                if (PreviewDesktopIconLabel != null) {
                    PreviewDesktopIconLabel.Text = PreviewNameEditBox.Text;
                }
            }
        }

        private void PreviewNameEditBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                PreviewSaveNameButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void PreviewColorSwatch_Checked(object sender, RoutedEventArgs e) {
            if (_isInitializing) return;
            if (sender is RadioButton rb && rb.Tag is string hex) {
                SelectThemeColor(hex, animate: true);
            }
        }

        private void PreviewHueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (_isInitializing || _isSyncingHueSliders) return;
            if (SettingsHueSlider != null && Math.Abs(SettingsHueSlider.Value - e.NewValue) > 0.5) {
                _isSyncingHueSliders = true;
                try {
                    SettingsHueSlider.Value = e.NewValue;
                } finally {
                    _isSyncingHueSliders = false;
                }
            }
            SettingsHueSlider_ValueChanged(sender, e);
        }

        private void PreviewOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (GeneralFolderOpacitySlider != null && Math.Abs(GeneralFolderOpacitySlider.Value - (e.NewValue * 100.0)) > 1.0) {
                GeneralFolderOpacitySlider.Value = e.NewValue * 100.0;
            }
            UpdateLivePreview();
        }

        private void PreviewShapeRadio_Click(object sender, RoutedEventArgs e) {
            string chosenShape = "Squircle";
            if (sender == PreviewShapeRadioCircle) chosenShape = "Circle";
            else if (sender == PreviewShapeRadioSquircle) chosenShape = "Squircle";
            else if (sender == PreviewShapeRadioClover) chosenShape = "Clover";
            else if (sender == PreviewShapeRadioScallop) chosenShape = "Scallop";
            else if (sender == PreviewShapeRadioArch) chosenShape = "Arch";

            _selectedShape = chosenShape;
            if (chosenShape == "Circle" && SettingsShapeCircle != null) SettingsShapeCircle.IsChecked = true;
            else if (chosenShape == "Squircle" && SettingsShapeSquircle != null) SettingsShapeSquircle.IsChecked = true;
            else if (chosenShape == "Clover" && SettingsShapeClover != null) SettingsShapeClover.IsChecked = true;
            else if (chosenShape == "Scallop" && SettingsShapeScallop != null) SettingsShapeScallop.IsChecked = true;
            else if (chosenShape == "Arch" && SettingsShapeArch != null) SettingsShapeArch.IsChecked = true;

            int sIdx = GetShapeIndex(chosenShape);
            AnimateSettingsShapePill(sIdx, immediate: false);
            AnimatePreviewShapePill(sIdx, immediate: false);

            UpdateLivePreview();
        }

        private void PreviewAlwaysPanakCheckBox_Checked(object sender, RoutedEventArgs e) { }
        private void PreviewAlwaysPanakCheckBox_Unchecked(object sender, RoutedEventArgs e) { }
        private void PreviewUnpackFolderButton_Click(object sender, RoutedEventArgs e) { }
        private void PreviewFolderLockBtn_Click(object sender, RoutedEventArgs e) {
            var win = new FolderLockSetupWindow("__PREVIEW__");
            win.Owner = this;
            win.ShowDialog();
        }

        // --- Settings Lock & Biometrics Enrollment ---
        private void CheckSettingsLockState() {
            if (FolderLockService.Instance.IsSettingsLocked) {
                if (SettingsLockCardContainer != null && SettingsLockBackdrop != null) {
                    MainContentGrid.Effect = new BlurEffect { Radius = 24.0, RenderingBias = RenderingBias.Performance };
                    SettingsLockBackdrop.Visibility = Visibility.Visible;
                    SettingsLockBackdrop.Opacity = 1.0;
                    SettingsLockCardContainer.Visibility = Visibility.Visible;
                    SettingsLockCardContainer.Opacity = 1.0;
                    if (SettingsLockScale != null) {
                        SettingsLockScale.ScaleX = 1.0;
                        SettingsLockScale.ScaleY = 1.0;
                    }

                    SettingsLockOverlay.SetupForSettings();
                    SettingsLockOverlay.FocusInput();
                    SettingsLockOverlay.Unlocked -= AnimateUnlockSettings;
                    SettingsLockOverlay.Unlocked += AnimateUnlockSettings;
                }
            } else {
                if (SettingsLockCardContainer != null) SettingsLockCardContainer.Visibility = Visibility.Collapsed;
                if (SettingsLockBackdrop != null) SettingsLockBackdrop.Visibility = Visibility.Collapsed;
                MainContentGrid.Effect = null;
            }
        }

        private void AnimateUnlockSettings() {
            if (SettingsLockCardContainer == null || SettingsLockBackdrop == null) return;

            // 1. Scale up & fade out the lock modal card
            var scaleXAnim = new DoubleAnimation(1.0, 1.15, TimeSpan.FromMilliseconds(220)) {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var scaleYAnim = new DoubleAnimation(1.0, 1.15, TimeSpan.FromMilliseconds(220)) {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var cardFadeAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200)) {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            // 2. Smoothly fade out the dark background overlay
            var backdropFadeAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(200)) {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            // 3. Smoothly fade blur down to 0
            if (MainContentGrid.Effect is BlurEffect blur) {
                var blurAnim = new DoubleAnimation(blur.Radius, 0.0, TimeSpan.FromMilliseconds(200)) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                blur.BeginAnimation(BlurEffect.RadiusProperty, blurAnim);
            }

            cardFadeAnim.Completed += (s, e) => {
                SettingsLockCardContainer.Visibility = Visibility.Collapsed;
                SettingsLockBackdrop.Visibility = Visibility.Collapsed;
                MainContentGrid.Effect = null;
                if (SettingsLockScale != null) {
                    SettingsLockScale.ScaleX = 1.0;
                    SettingsLockScale.ScaleY = 1.0;
                }
                SettingsLockCardContainer.Opacity = 1.0;
                SettingsLockBackdrop.Opacity = 1.0;
            };

            if (SettingsLockScale != null) {
                SettingsLockScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);
                SettingsLockScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);
            }
            SettingsLockCardContainer.BeginAnimation(UIElement.OpacityProperty, cardFadeAnim);
            SettingsLockBackdrop.BeginAnimation(UIElement.OpacityProperty, backdropFadeAnim);
        }

        private void UpdateSettingsLockUI() {
            bool isLocked = FolderLockService.Instance.IsSettingsLocked;
            if (LockSettingsCheckBox != null) {
                LockSettingsCheckBox.IsChecked = isLocked;
            }
            if (SettingsLockStatusText != null) {
                if (isLocked) {
                    string typeArm = FolderLockService.Instance.SettingsLockType switch {
                        "PIN" => "PIN կոդ",
                        "Pattern" => "Նախշ",
                        _ => "Գաղտնաբառ"
                    };
                    SettingsLockStatusText.Text = $"🔒 Կողպված է ({typeArm})";
                    SettingsLockStatusText.Foreground = (Brush)FindResource("M3PrimaryBrush");
                } else {
                    SettingsLockStatusText.Text = "Կայանքները պաշտպանված չեն";
                    SettingsLockStatusText.Foreground = (Brush)FindResource("M3OnSurfaceVariantBrush");
                }
            }
        }

        private void LockSettingsCheckBox_Click(object sender, RoutedEventArgs e) {
            var win = new FolderLockSetupWindow("__SETTINGS__");
            win.Owner = this;
            win.ShowDialog();
            UpdateSettingsLockUI();
        }

        private void ConfigureSettingsLockBtn_Click(object sender, RoutedEventArgs e) {
            var win = new FolderLockSetupWindow("__SETTINGS__");
            win.Owner = this;
            win.ShowDialog();
            UpdateSettingsLockUI();
        
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

        private async void CheckOtaBtn_Click(object sender, RoutedEventArgs e) {
            if (CrowdinOtaStatusText == null || CheckOtaBtn == null) return;

            CheckOtaBtn.IsEnabled = false;
            string origContent = CheckOtaBtn.Content?.ToString() ?? "🔄 Ստուգել OTA";
            CheckOtaBtn.Content = "⏳ ...";
            CrowdinOtaStatusText.Text = "Ստուգվում են թարմ թարգմանությունները...";

            try {
                bool updated = await LocalizationService.Instance.RefreshOtaTranslationsAsync();
                if (updated) {
                    CrowdinOtaStatusText.Text = "✓ Թարգմանությունները հաջողությամբ թարմացվեցին!";
                    ApplyLocalization();
                } else {
                    CrowdinOtaStatusText.Text = "✓ Թարգմանությունները արդիական են";
                }
            } catch (Exception ex) {
                CrowdinOtaStatusText.Text = $"Սխալ: {ex.Message}";
            } finally {
                CheckOtaBtn.Content = origContent;
                CheckOtaBtn.IsEnabled = true;
            }
        }

        private void ExportLangJsonBtn_Click(object sender, RoutedEventArgs e) {
            try {
                string lang = LocalizationService.Instance.CurrentLanguage;
                var dict = LocalizationService.Instance.GetTranslations(lang);
                var sfd = new Microsoft.Win32.SaveFileDialog {
                    FileName = $"{lang}.json",
                    DefaultExt = ".json",
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    Title = "Արտահանել թարգմանությունները"
                };
                if (sfd.ShowDialog(this) == true) {
                    var options = new System.Text.Json.JsonSerializerOptions {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };
                    string json = System.Text.Json.JsonSerializer.Serialize(dict, options);
                    System.IO.File.WriteAllText(sfd.FileName, json, System.Text.Encoding.UTF8);
                    System.Windows.MessageBox.Show(this, $"Ֆայլը հաջողությամբ պահպանվեց՝\n{sfd.FileName}", "Panak", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            } catch (Exception ex) {
                System.Windows.MessageBox.Show(this, $"Չհաջողվեց արտահանել՝ {ex.Message}", "Panak", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

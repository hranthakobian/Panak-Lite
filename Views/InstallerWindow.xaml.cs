using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Panak.Services;

namespace Panak.Views {
    public partial class InstallerWindow : Window {
        private readonly bool _isUninstallMode;
        private bool _needsAutoDetect = false;
        private bool _isUpdatingLanguage = false;
        private bool _isInitialized = false;

        public InstallerWindow(bool isUninstallMode = false) {
            InitializeComponent();
            _isUninstallMode = isUninstallMode;
            LoadLogoAndIcon();

            var settings = SettingsService.Instance.Current;
            string lang = "en";
            if (!string.IsNullOrEmpty(settings.Language)) {
                lang = settings.Language;
                _needsAutoDetect = false;
            } else {
                _needsAutoDetect = true;
            }

            SetSelectedLanguage(lang);

            string color = string.IsNullOrEmpty(settings.ThemeColor)
                ? ShellHelper.GetDefaultPastelColorHex()
                : settings.ThemeColor;
            double hue = ShellHelper.GetHueFromHex(color);
            ApplyMaterialYouPalette(hue);

            ConfigureMode();
            _isInitialized = true;

            Loaded += InstallerWindow_Loaded;
        }

        private async void InstallerWindow_Loaded(object sender, RoutedEventArgs e) {
            if (_needsAutoDetect) {
                string detected = await LocalizationService.DetectOptimalLanguageAsync();
                SetSelectedLanguage(detected);
                ConfigureMode();
            }
        }

        private void SetSelectedLanguage(string lang) {
            _isUpdatingLanguage = true;
            try {
                foreach (System.Windows.Controls.ComboBoxItem item in LanguageComboBox.Items) {
                    if (string.Equals(item.Tag?.ToString(), lang, StringComparison.OrdinalIgnoreCase)) {
                        item.IsSelected = true;
                        break;
                    }
                }
                LocalizationService.Instance.CurrentLanguage = lang;
            } finally {
                _isUpdatingLanguage = false;
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
            if (!_isInitialized || _isUpdatingLanguage) return;
            if (LanguageComboBox?.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string lang) {
                LocalizationService.Instance.CurrentLanguage = lang;
                ConfigureMode();
            }
        }

        private void LoadLogoAndIcon() {
            try {
                var uri = new Uri("pack://application:,,,/Setup.png", UriKind.Absolute);
                var bi = new BitmapImage(uri);
                Icon = bi;
                return;
            } catch { }

            try {
                var uri = new Uri("pack://application:,,,/PanakSetupLogo.png", UriKind.Absolute);
                var bi = new BitmapImage(uri);
                Icon = bi;
                return;
            } catch { }

            try {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = {
                    Path.Combine(baseDir, "src", "Setup.png"),
                    Path.Combine(baseDir, "Setup.png"),
                    Path.Combine(baseDir, "PanakSetupLogo.png"),
                    Path.Combine(baseDir, "Panak setup logo.png"),
                    Path.Combine(baseDir, "PanakLogo.png"),
                    Path.Combine(baseDir, "Panak logo.png"),
                    Path.Combine(baseDir, "Panak.ico")
                };
                foreach (var file in candidates) {
                    if (File.Exists(file)) {
                        var bi = new BitmapImage(new Uri(file, UriKind.Absolute));
                        Icon = bi;
                        return;
                    }
                }
            } catch { }
        }

        public void ApplyMaterialYouPalette(double hue) {
            try {
                hue = (hue % 360.0 + 360.0) % 360.0;

                var (surfaceColor, containerColor, primaryColor) = ShellHelper.GetMaterialYouTonalPalette(hue);
                var cardColor = ShellHelper.ColorFromHsl(hue, 0.60, 0.94);
                var onSurfaceColor = ShellHelper.ColorFromHsl(hue, 0.40, 0.16);
                var onSurfaceVariantColor = ShellHelper.ColorFromHsl(hue, 0.28, 0.42);
                var trackInactiveColor = ShellHelper.ColorFromHsl(hue, 0.45, 0.84);

                System.Windows.Media.Color toWpf(System.Drawing.Color c) =>
                    System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);

                var wpfSurface = toWpf(surfaceColor);
                var wpfContainer = toWpf(containerColor);
                var wpfCard = toWpf(cardColor);
                var wpfPrimary = toWpf(primaryColor);
                var wpfOnSurface = toWpf(onSurfaceColor);
                var wpfOnSurfaceVariant = toWpf(onSurfaceVariantColor);
                var wpfTrackInactive = toWpf(trackInactiveColor);
                var wpfOnPrimary = System.Windows.Media.Color.FromRgb(0xFE, 0xF4, 0xEB);

                // Update dynamic brushes in Resources so styles and templates adapt in real time
                Resources["M3SurfaceBrush"] = new System.Windows.Media.SolidColorBrush(wpfSurface);
                Resources["M3SurfaceContainerBrush"] = new System.Windows.Media.SolidColorBrush(wpfContainer);
                Resources["M3CardBrush"] = new System.Windows.Media.SolidColorBrush(wpfCard);
                Resources["M3PrimaryBrush"] = new System.Windows.Media.SolidColorBrush(wpfPrimary);
                Resources["M3OnPrimaryBrush"] = new System.Windows.Media.SolidColorBrush(wpfOnPrimary);
                Resources["M3OnSurfaceBrush"] = new System.Windows.Media.SolidColorBrush(wpfOnSurface);
                Resources["M3OnSurfaceVariantBrush"] = new System.Windows.Media.SolidColorBrush(wpfOnSurfaceVariant);
                Resources["M3TrackInactiveBrush"] = new System.Windows.Media.SolidColorBrush(wpfTrackInactive);

                var (cContainer, cTriangle, cCloud, cShield, cEllipse) = ShellHelper.GetLogoDynamicColors(hue, false);
                Resources["LogoContainerBrush"] = new System.Windows.Media.SolidColorBrush(toWpf(cContainer));
                Resources["LogoArrowBrush"] = new System.Windows.Media.SolidColorBrush(toWpf(cTriangle));
                Resources["LogoTriangleBrush"] = new System.Windows.Media.SolidColorBrush(toWpf(cTriangle));
                Resources["LogoCloudBrush"] = new System.Windows.Media.SolidColorBrush(toWpf(cCloud));
                Resources["LogoShieldBrush"] = new System.Windows.Media.SolidColorBrush(toWpf(cShield));
                Resources["LogoEllipseBrush"] = new System.Windows.Media.SolidColorBrush(toWpf(cEllipse));

                if (MainWindowBorder != null) {
                    MainWindowBorder.Background = new System.Windows.Media.SolidColorBrush(wpfSurface);
                }
            } catch { }
        }

        private void ConfigureMode() {
            if (WizardTitleText == null) return;
            var loc = LocalizationService.Instance;
            if (_isUninstallMode) {
                Title = loc["UninstallerTitle"];
                WizardTitleText.Text = loc["UninstallerTitle"];
                WizardSubtitleText.Text = loc["UninstallDesc"];
                InstallPanel.Visibility = Visibility.Collapsed;
                UninstallPanel.Visibility = Visibility.Visible;
                MainActionBtn.Content = loc["Uninstall"];
                MainActionBtn.Style = (Style)FindResource("DangerButtonStyle");
            } else {
                Title = loc["InstallerTitle"];
                WizardTitleText.Text = loc["InstallerTitle"];
                WizardSubtitleText.Text = loc["InstallDesc"];
                InstallPanel.Visibility = Visibility.Visible;
                UninstallPanel.Visibility = Visibility.Collapsed;
                MainActionBtn.Content = loc["Install"];
                MainActionBtn.Style = (Style)FindResource("ActionButtonStyle");
            }

            CancelActionBtn.Content = loc["Cancel"];
            FinishBtn.Content = loc["Finish"];

            InstallIntroText.Text = loc["InstallIntroDesc"];
            DesktopShortcutTitleText.Text = loc["DesktopShortcut"];
            DesktopShortcutDescText.Text = loc["DesktopShortcutDesc"];
            StartMenuShortcutTitleText.Text = loc["StartMenuShortcut"];
            StartMenuShortcutDescText.Text = loc["StartMenuShortcutDesc"];
            AutoStartTitleText.Text = loc["AutoStart"];
            AutoStartDescText.Text = loc["AutoStartDesc"];

            UninstallIntroText.Text = loc["UninstallConfirm"];
            UninstallWillRemoveText.Text = loc["UninstallWillRemove"];
            UninstallItemContextMenuText.Text = "• " + loc["UninstallItemContextMenu"];
            UninstallItemShortcutsText.Text = "• " + loc["UninstallItemShortcuts"];
            UninstallItemStartupText.Text = "• " + loc["UninstallItemStartup"];
            UninstallItemFilesText.Text = "• " + loc["UninstallItemFiles"];
        }

        private async void MainActionBtn_Click(object sender, RoutedEventArgs e) {
            var loc = LocalizationService.Instance;

            InstallPanel.Visibility = Visibility.Collapsed;
            UninstallPanel.Visibility = Visibility.Collapsed;
            MainActionBtn.Visibility = Visibility.Collapsed;
            CancelActionBtn.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;

            ProgressTitleText.Text = _isUninstallMode ? loc["Uninstall"] : loc["Install"];
            InstallationProgressBar.IsIndeterminate = true;
            ProgressStatusText.Text = _isUninstallMode ? loc["PreparingUninstall"] : loc["PreparingInstall"];

            if (_isUninstallMode) {
                bool success = await System.Threading.Tasks.Task.Run(() => InstallerService.Uninstall());
                ShowCompleted(success, loc["UninstalledSuccess"], loc["UninstallFailed"]);
            } else {
                bool desktop = DesktopShortcutCheckBox.IsChecked ?? true;
                bool startMenu = StartMenuShortcutCheckBox.IsChecked ?? true;
                bool autoStart = AutoStartCheckBox.IsChecked ?? true;

                bool success = await System.Threading.Tasks.Task.Run(() => InstallerService.Install(desktop, startMenu, autoStart));
                if (success) {
                    SettingsService.Instance.Current.Language = LocalizationService.Instance.CurrentLanguage;
                    SettingsService.Instance.SaveSettings();
                }
                ShowCompleted(success, loc["InstalledSuccess"], loc["InstallFailed"]);
            }
        }

        private void ShowCompleted(bool success, string successMsg, string failMsg) {
            ProgressPanel.Visibility = Visibility.Collapsed;
            InstallPanel.Visibility = Visibility.Collapsed;
            UninstallPanel.Visibility = Visibility.Collapsed;
            MainActionBtn.Visibility = Visibility.Collapsed;
            CancelActionBtn.Visibility = Visibility.Collapsed;
            CompletedPanel.Visibility = Visibility.Visible;
            FinishBtn.Visibility = Visibility.Visible;

            var loc = LocalizationService.Instance;
            if (success) {
                StatusEmoji.Text = "🎉";
                StatusTitle.Text = successMsg;
                StatusSubtitle.Text = _isUninstallMode ? loc["UninstalledSuccess"] : loc["ReadyToUse"];
            } else {
                StatusEmoji.Text = "⚠️";
                StatusTitle.Text = failMsg;
                StatusSubtitle.Text = loc["InstallErrorEnsureUnlocked"];
            }
        }

        private void FinishBtn_Click(object sender, RoutedEventArgs e) {
            if (!_isUninstallMode) {
                // If installed, launch background app from install directory if available
                string targetDir = InstallerService.GetInstallDirectory();
                string exe = System.IO.Path.Combine(targetDir, "Panak.exe");
                if (System.IO.File.Exists(exe)) {
                    Process.Start(new ProcessStartInfo {
                        FileName = exe,
                        Arguments = "--welcome",
                        UseShellExecute = true
                    });
                }
            }
            Application.Current.Shutdown();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                DragMove();
            }
        }
    }
}

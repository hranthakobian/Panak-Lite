using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Panak.Services;

namespace Panak.Setup {
    public partial class SetupWindow : Window {
        private readonly bool _isUninstallMode;
        private readonly bool _isUpdateMode;
        private string _targetDirectory;
        private bool _isUpdatingLanguage = false;
        private bool _isInitialized = false;
        private bool _dotNetUserToggled = false;

        public SetupWindow(bool isUninstallMode = false, bool isUpdateMode = false, string? initialLanguage = null) {
            InitializeComponent();
            _isUninstallMode = isUninstallMode;
            _isUpdateMode = isUpdateMode;

            _targetDirectory = StandaloneInstallerService.GetInstalledDirectory();
            if (string.IsNullOrEmpty(_targetDirectory)) {
                _targetDirectory = StandaloneInstallerService.GetDefaultInstallDirectory();
            }

            if (DotNetRuntimeCheckBox != null) {
                DotNetRuntimeCheckBox.Click += (s, e) => _dotNetUserToggled = true;
            }

            LoadLogoAndIcon();
            LoadThemeAndLanguage(initialLanguage);
            ConfigureMode();
            _isInitialized = true;

            Loaded += SetupWindow_Loaded;
        }

        private void SetupWindow_Loaded(object sender, RoutedEventArgs e) {
            if (_isUpdateMode) {
                MainActionBtn_Click(this, new RoutedEventArgs());
            }
        }

        private void LoadLogoAndIcon() {
            try {
                var asm = typeof(SetupWindow).Assembly;
                string? resName = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("SetupEmbedded.png", StringComparison.OrdinalIgnoreCase) ||
                                         n.EndsWith("Setup.png", StringComparison.OrdinalIgnoreCase) ||
                                         n.EndsWith("PanakSetupLogoEmbedded.png", StringComparison.OrdinalIgnoreCase) ||
                                         n.EndsWith("PanakSetupLogo.png", StringComparison.OrdinalIgnoreCase) ||
                                         n.EndsWith("Panak setup logo.png", StringComparison.OrdinalIgnoreCase) ||
                                         n.EndsWith("PanakLogoEmbedded.png", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(resName)) {
                    using var stream = asm.GetManifestResourceStream(resName);
                    if (stream != null) {
                        var ms = new MemoryStream();
                        stream.CopyTo(ms);
                        ms.Position = 0;

                        var bi = new System.Windows.Media.Imaging.BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bi.StreamSource = ms;
                        bi.EndInit();
                        bi.Freeze();

                        Icon = bi;
                        return;
                    }
                }
            } catch { }

            try {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = {
                    Path.Combine(exeDir, "Setup.png"),
                    Path.Combine(exeDir, "PanakSetupLogo.png"),
                    Path.Combine(exeDir, "Panak setup logo.png"),
                    Path.Combine(exeDir, "PanakLogo.png"),
                    Path.Combine(exeDir, "Panak logo.png")
                };
                foreach (var localPng in candidates) {
                    if (File.Exists(localPng)) {
                        var bi = new System.Windows.Media.Imaging.BitmapImage(new Uri(localPng, UriKind.Absolute));
                        Icon = bi;
                        return;
                    }
                }
            } catch { }
        }

        private void LoadThemeAndLanguage(string? initialLanguage = null) {
            string language = !string.IsNullOrWhiteSpace(initialLanguage) ? initialLanguage : string.Empty;
            string colorHex = ShellHelper.GetDefaultPastelColorHex();

            try {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string settingsPath = Path.Combine(appData, "Panak", "settings.json");
                if (File.Exists(settingsPath)) {
                    string json = File.ReadAllText(settingsPath);
                    using var doc = JsonDocument.Parse(json);
                    if (string.IsNullOrWhiteSpace(language) && doc.RootElement.TryGetProperty("Language", out var langProp) && !string.IsNullOrEmpty(langProp.GetString())) {
                        language = langProp.GetString()!;
                    }
                    if (doc.RootElement.TryGetProperty("ThemeColor", out var colProp) && !string.IsNullOrEmpty(colProp.GetString())) {
                        colorHex = colProp.GetString()!;
                    }
                }
            } catch { }

            if (string.IsNullOrWhiteSpace(language)) {
                try {
                    string uiLang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
                    string[] supported = { "hy", "en", "ru", "fr", "es", "zh", "ar" };
                    language = supported.Contains(uiLang) ? uiLang : "hy";
                } catch {
                    language = "hy";
                }
            }

            SetSelectedLanguage(language);

            double hue = ShellHelper.GetHueFromHex(colorHex);
            ApplyMaterialYouPalette(hue);
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

        public void ApplyMaterialYouPalette(double hue) {
            try {
                hue = (hue % 360.0 + 360.0) % 360.0;

                var (surfaceColor, containerColor, primaryColor) = ShellHelper.GetMaterialYouTonalPalette(hue);
                var cardColor = ShellHelper.ColorFromHsl(hue, 0.60, 0.94);
                var onSurfaceColor = ShellHelper.ColorFromHsl(hue, 0.40, 0.16);
                var onSurfaceVariantColor = ShellHelper.ColorFromHsl(hue, 0.28, 0.42);
                var trackInactiveColor = ShellHelper.ColorFromHsl(hue, 0.45, 0.84);

                Color toWpf(System.Drawing.Color c) => Color.FromArgb(c.A, c.R, c.G, c.B);

                var wpfSurface = toWpf(surfaceColor);
                var wpfContainer = toWpf(containerColor);
                var wpfCard = toWpf(cardColor);
                var wpfPrimary = toWpf(primaryColor);
                var wpfOnSurface = toWpf(onSurfaceColor);
                var wpfOnSurfaceVariant = toWpf(onSurfaceVariantColor);
                var wpfTrackInactive = toWpf(trackInactiveColor);
                var wpfOnPrimary = Color.FromRgb(0xFE, 0xF4, 0xEB);

                Resources["M3SurfaceBrush"] = new SolidColorBrush(wpfSurface);
                Resources["M3SurfaceContainerBrush"] = new SolidColorBrush(wpfContainer);
                Resources["M3CardBrush"] = new SolidColorBrush(wpfCard);
                Resources["M3PrimaryBrush"] = new SolidColorBrush(wpfPrimary);
                Resources["M3OnPrimaryBrush"] = new SolidColorBrush(wpfOnPrimary);
                Resources["M3OnSurfaceBrush"] = new SolidColorBrush(wpfOnSurface);
                Resources["M3OnSurfaceVariantBrush"] = new SolidColorBrush(wpfOnSurfaceVariant);
                Resources["M3TrackInactiveBrush"] = new SolidColorBrush(wpfTrackInactive);

                var (cContainer, cTriangle, cCloud, cShield, cEllipse) = ShellHelper.GetLogoDynamicColors(hue, false);
                Resources["LogoContainerBrush"] = new SolidColorBrush(toWpf(cContainer));
                Resources["LogoArrowBrush"] = new SolidColorBrush(toWpf(cTriangle));
                Resources["LogoTriangleBrush"] = new SolidColorBrush(toWpf(cTriangle));
                Resources["LogoCloudBrush"] = new SolidColorBrush(toWpf(cCloud));
                Resources["LogoShieldBrush"] = new SolidColorBrush(toWpf(cShield));
                Resources["LogoEllipseBrush"] = new SolidColorBrush(toWpf(cEllipse));

                if (MainWindowBorder != null) {
                    MainWindowBorder.Background = new SolidColorBrush(wpfSurface);
                }
                if (LanguageComboBox != null) {
                    LanguageComboBox.Foreground = new SolidColorBrush(wpfOnSurface);
                }
            } catch { }
        }

        private void ConfigureMode() {
            if (InstallFolderDisplayPath == null || WizardTitleText == null) return;
            var loc = LocalizationService.Instance;

            InstallFolderDisplayPath.Text = _targetDirectory;

            if (_isUpdateMode) {
                Title = loc["UpdatePanak"];
                WizardTitleText.Text = loc["UpdatePanak"];
                WizardSubtitleText.Text = loc["UpdatingPanakDesc"];
                InstallPanel.Visibility = Visibility.Visible;
                UninstallPanel.Visibility = Visibility.Collapsed;
                MainActionBtn.Content = loc["InstallNow"];
                MainActionBtn.Style = (Style)FindResource("ActionButtonStyle");
            } else if (_isUninstallMode) {
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
            InstallFolderTitleText.Text = loc["InstallationFolder"];
            BrowseFolderBtn.Content = loc["Browse"];

            DesktopShortcutTitleText.Text = loc["DesktopShortcut"];
            DesktopShortcutDescText.Text = loc["DesktopShortcutDesc"];
            StartMenuShortcutTitleText.Text = loc["StartMenuShortcut"];
            StartMenuShortcutDescText.Text = loc["StartMenuShortcutDesc"];
            AutoStartTitleText.Text = loc["AutoStart"];
            AutoStartDescText.Text = loc["AutoStartDesc"];

            if (ProgressTitleText != null) {
                ProgressTitleText.Text = _isUpdateMode 
                    ? loc["UpdatingPanak"] 
                    : (_isUninstallMode ? loc["Uninstall"] : loc["Install"]);
            }
            if (ProgressStatusText != null) {
                ProgressStatusText.Text = _isUpdateMode 
                    ? loc["PreparingUpdate"] 
                    : (_isUninstallMode ? loc["PreparingUninstall"] : loc["PreparingInstall"]);
            }

            ConfigureDotNetRuntime();

            UninstallIntroText.Text = loc["UninstallConfirm"];
            UninstallWillRemoveText.Text = loc["UninstallWillRemove"];
            UninstallItemContextMenuText.Text = "• " + loc["UninstallItemContextMenu"];
            UninstallItemShortcutsText.Text = "• " + loc["UninstallItemShortcuts"];
            UninstallItemStartupText.Text = "• " + loc["UninstallItemStartup"];
            UninstallItemFilesText.Text = "• " + loc["UninstallItemFiles"];
        }

        private void ConfigureDotNetRuntime() {
            if (DotNetSectionCard != null) {
                DotNetSectionCard.Visibility = Visibility.Collapsed;
            }
            if (DotNetRuntimeCheckBox != null) {
                DotNetRuntimeCheckBox.IsChecked = false;
            }
        }

        private string GetLocalizedInstallFolderTitle() {
            return LocalizationService.Instance["InstallationFolder"];
        }

        private string GetLocalizedBrowseText() {
            return LocalizationService.Instance["Browse"];
        }

        private void BrowseFolderBtn_Click(object sender, RoutedEventArgs e) {
            try {
                var dialog = new Microsoft.Win32.OpenFolderDialog {
                    Title = GetLocalizedInstallFolderTitle(),
                    InitialDirectory = _targetDirectory
                };

                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName)) {
                    _targetDirectory = dialog.FolderName;
                    InstallFolderDisplayPath.Text = _targetDirectory;
                }
            } catch { }
        }

        private async void MainActionBtn_Click(object sender, RoutedEventArgs e) {
            var loc = LocalizationService.Instance;

            // Transition to progress panel
            InstallPanel.Visibility = Visibility.Collapsed;
            UninstallPanel.Visibility = Visibility.Collapsed;
            MainActionBtn.Visibility = Visibility.Collapsed;
            CancelActionBtn.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;

            ProgressTitleText.Text = _isUpdateMode ? loc["UpdatingPanak"] : (_isUninstallMode ? loc["Uninstall"] : loc["Install"]);
            InstallationProgressBar.Value = 10;
            ProgressStatusText.Text = _isUpdateMode ? loc["PreparingUpdate"] : (_isUninstallMode ? loc["PreparingUninstall"] : loc["PreparingInstall"]);

            var progress = new Progress<(double percent, string status)>(val => {
                InstallationProgressBar.Value = Math.Clamp(val.percent * 100.0, 0, 100);
                ProgressStatusText.Text = val.status;
            });

            if (_isUninstallMode) {
                var (success, error) = await StandaloneInstallerService.UninstallAsync(_targetDirectory, progress);
                ShowCompleted(success, loc["UninstalledSuccess"], !string.IsNullOrEmpty(error) ? error : loc["UninstallFailed"]);
            } else {
                bool desktop = DesktopShortcutCheckBox.IsChecked ?? true;
                bool startMenu = StartMenuShortcutCheckBox.IsChecked ?? true;
                bool autoStart = AutoStartCheckBox.IsChecked ?? true;
                bool installDotNet = DotNetRuntimeCheckBox.IsChecked ?? false;

                var (success, error) = await StandaloneInstallerService.InstallAsync(
                    _targetDirectory, desktop, startMenu, autoStart, installDotNet, progress, isUpdate: _isUpdateMode);
                if (success) {
                    SaveInstalledLanguage(LocalizationService.Instance.CurrentLanguage);
                }
                ShowCompleted(success, 
                    _isUpdateMode ? loc["UpdatedSuccess"] : loc["InstalledSuccess"], 
                    !string.IsNullOrEmpty(error) ? error : (_isUpdateMode ? loc["UpdateFailed"] : loc["InstallFailed"]));
            }
        }

        private void SaveInstalledLanguage(string language) {
            try {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "Panak");
                if (!Directory.Exists(dir)) {
                    Directory.CreateDirectory(dir);
                }
                string settingsPath = Path.Combine(dir, "settings.json");
                var dict = new Dictionary<string, object>();
                if (File.Exists(settingsPath)) {
                    try {
                        string existingJson = File.ReadAllText(settingsPath);
                        using var doc = JsonDocument.Parse(existingJson);
                        foreach (var prop in doc.RootElement.EnumerateObject()) {
                            switch (prop.Value.ValueKind) {
                                case JsonValueKind.String:
                                    dict[prop.Name] = prop.Value.GetString()!;
                                    break;
                                case JsonValueKind.Number:
                                    if (prop.Value.TryGetInt32(out int iVal)) dict[prop.Name] = iVal;
                                    else if (prop.Value.TryGetDouble(out double dVal)) dict[prop.Name] = dVal;
                                    break;
                                case JsonValueKind.True:
                                    dict[prop.Name] = true;
                                    break;
                                case JsonValueKind.False:
                                    dict[prop.Name] = false;
                                    break;
                            }
                        }
                    } catch { }
                }
                dict["Language"] = language;
                string updatedJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsPath, updatedJson);
            } catch { }
        }

        private void ShowCompleted(bool success, string successMsg, string failMsg) {
            ProgressPanel.Visibility = Visibility.Collapsed;
            CompletedPanel.Visibility = Visibility.Visible;
            FinishBtn.Visibility = Visibility.Visible;

            var loc = LocalizationService.Instance;
            if (success) {
                StatusEmoji.Text = "🎉";
                StatusTitle.Text = successMsg;
                StatusSubtitle.Text = _isUpdateMode ? loc["UpdateCompletedDesc"] : (_isUninstallMode ? loc["UninstalledSuccess"] : loc["ReadyToUse"]);

                if (_isUpdateMode) {
                    var timer = new System.Windows.Threading.DispatcherTimer {
                        Interval = TimeSpan.FromSeconds(1.8)
                    };
                    timer.Tick += (s, ev) => {
                        timer.Stop();
                        FinishBtn_Click(this, new RoutedEventArgs());
                    };
                    timer.Start();
                }
            } else {
                StatusEmoji.Text = "⚠️";
                StatusTitle.Text = failMsg;
                StatusSubtitle.Text = loc["InstallErrorEnsureUnlocked"];
            }
        }

        private void FinishBtn_Click(object sender, RoutedEventArgs e) {
            if (!_isUninstallMode) {
                string exe = Path.Combine(_targetDirectory, "Panak.exe");
                if (File.Exists(exe)) {
                    try {
                        Process.Start(new ProcessStartInfo {
                            FileName = exe,
                            Arguments = _isUpdateMode ? "--background" : "--welcome",
                            UseShellExecute = true
                        });
                    } catch { }
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

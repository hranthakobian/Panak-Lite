using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Panak.Services;

namespace Panak.Uninstall {
    public partial class UninstallerWindow : Window {
        private bool _isCompleted = false;
        private bool _isUpdatingLanguage = false;
        private bool _isInitialized = false;
        private string _selectedThemeMode = "Auto";
        private IntPtr _hIconBig = IntPtr.Zero;
        private IntPtr _hIconSmall = IntPtr.Zero;

        public UninstallerWindow() {
            InitializeComponent();
            LoadFallbackIcon();
            LoadThemeAndLanguage();
            CheckDotNetStatus();
            _isInitialized = true;
        }

        protected override void OnSourceInitialized(EventArgs e) {
            base.OnSourceInitialized(e);
            UpdateTaskbarIcon();
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);
            if (_hIconBig != IntPtr.Zero) {
                ShellHelper.DestroyIcon(_hIconBig);
                _hIconBig = IntPtr.Zero;
            }
            if (_hIconSmall != IntPtr.Zero) {
                ShellHelper.DestroyIcon(_hIconSmall);
                _hIconSmall = IntPtr.Zero;
            }
        }

        public void UpdateTaskbarIcon(double? explicitHue = null, bool? explicitDark = null) {
            try {
                double hue = explicitHue ?? 26.2;
                bool isDark = explicitDark ?? (_selectedThemeMode == "Dark" || (_selectedThemeMode == "Auto" && ShellHelper.IsWindowsDarkTheme()));

                if (!explicitHue.HasValue) {
                    try {
                        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        string settingsPath = Path.Combine(appData, "Panak", "settings.json");
                        if (File.Exists(settingsPath)) {
                            string json = File.ReadAllText(settingsPath);
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("ThemeColor", out var colProp) && !string.IsNullOrEmpty(colProp.GetString())) {
                                var col = ColorTranslator.FromHtml(colProp.GetString()!);
                                hue = ShellHelper.GetHueFromColor(col);
                            }
                        }
                    } catch { }
                }

                using var bmpBig = ShellHelper.RenderUninstallerLogoBitmap(48, hue, isDark);
                using var bmpSmall = ShellHelper.RenderUninstallerLogoBitmap(16, hue, isDark);

                IntPtr newBig = bmpBig.GetHicon();
                IntPtr newSmall = bmpSmall.GetHicon();

                var bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    newBig,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bs.Freeze();
                this.Icon = bs;

                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero) {
                    ShellHelper.SendMessage(hwnd, ShellHelper.WM_SETICON, (IntPtr)ShellHelper.ICON_BIG, newBig);
                    ShellHelper.SendMessage(hwnd, ShellHelper.WM_SETICON, (IntPtr)ShellHelper.ICON_SMALL, newSmall);
                }

                if (_hIconBig != IntPtr.Zero) ShellHelper.DestroyIcon(_hIconBig);
                if (_hIconSmall != IntPtr.Zero) ShellHelper.DestroyIcon(_hIconSmall);
                _hIconBig = newBig;
                _hIconSmall = newSmall;
            } catch {
                LoadFallbackIcon();
            }
        }

        private void LoadFallbackIcon() {
            try {
                var uri = new Uri("pack://application:,,,/Uninstall;component/Uninstall.ico", UriKind.RelativeOrAbsolute);
                Icon = new BitmapImage(uri);
                return;
            } catch { }

            try {
                var asm = typeof(UninstallerWindow).Assembly;
                string? resName = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("UninstallerEmbedded.png", StringComparison.OrdinalIgnoreCase) ||
                                         n.EndsWith("Uninstaller.png", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(resName)) {
                    using var stream = asm.GetManifestResourceStream(resName);
                    if (stream != null) {
                        var ms = new MemoryStream();
                        stream.CopyTo(ms);
                        ms.Position = 0;
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.StreamSource = ms;
                        bi.EndInit();
                        bi.Freeze();
                        Icon = bi;
                        return;
                    }
                }
            } catch { }

            try {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = {
                    Path.Combine(baseDir, "Uninstall.ico"),
                    Path.Combine(baseDir, "Panak.ico")
                };
                foreach (var c in candidates) {
                    if (File.Exists(c)) {
                        Icon = new BitmapImage(new Uri(c));
                        return;
                    }
                }
            } catch { }
        }

        private void LoadThemeAndLanguage() {
            string language = "hy";
            string colorHex = ShellHelper.GetDefaultPastelColorHex();
            _selectedThemeMode = "Auto";

            try {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string settingsPath = Path.Combine(appData, "Panak", "settings.json");
                if (File.Exists(settingsPath)) {
                    string json = File.ReadAllText(settingsPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Language", out var langProp) && !string.IsNullOrEmpty(langProp.GetString())) {
                        language = langProp.GetString()!;
                    }
                    if (doc.RootElement.TryGetProperty("ThemeColor", out var colProp) && !string.IsNullOrEmpty(colProp.GetString())) {
                        colorHex = colProp.GetString()!;
                    }
                    if (doc.RootElement.TryGetProperty("ThemeMode", out var modeProp) && !string.IsNullOrEmpty(modeProp.GetString())) {
                        _selectedThemeMode = modeProp.GetString()!;
                    }
                }
            } catch { }

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
                UpdateLocalizedTexts();
            } finally {
                _isUpdatingLanguage = false;
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
            if (!_isInitialized || _isUpdatingLanguage) return;
            if (LanguageComboBox?.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string lang) {
                LocalizationService.Instance.CurrentLanguage = lang;
                UpdateLocalizedTexts();
            }
        }

        private void UpdateLocalizedTexts() {
            var loc = LocalizationService.Instance;
            Title = loc["UninstallerTitle"] ?? "Panak - Ապատեղադրիչ";
            TitleText.Text = loc["UninstallerTitle"] ?? "Panak - Ապատեղադրիչ";
            SubtitleText.Text = loc["UninstallDesc"] ?? "Հեռացնել Panak-ը համակարգչից";

            FeedbackBodyText.Text = loc["UninstallApology"] ?? "Ցավում եմ, եթե ծրագիրը չի արդարացրել Ձեր սպասելիքները։ Խնդրում եմ կիսվել Ձեր կարծիքով կայքի կամ էլ. փոստի միջոցով. ես մեծ սիրով հաշվի կառնեմ այն եւ կշտկեմ թերությունները։";
            ComponentsTitleText.Text = loc["UninstallWillRemove"] ?? "Սա կհեռացնի հետեւյալ բաղադրիչները՝";
            ItemMenusText.Text = "• " + (loc["UninstallItemContextMenu"] ?? "Explorer-ի աջ կտտոցի ընտրացանկի տարրերը");
            ItemShortcutsText.Text = "• " + (loc["UninstallItemShortcuts"] ?? "Աշխատասեղանի եւ Start ընտրացանկի դյուրանցումները");
            ItemStartupText.Text = "• " + (loc["UninstallItemStartup"] ?? "Ինքնաշխատ մեկնարկի կարգավորումները");
            ItemFilesText.Text = "• " + (loc["UninstallItemFiles"] ?? "Ծրագրային նիշքերը եւ գրանցամատյանի գրառումները");

            OptMenusLabel.Text = loc["UninstallRemoveMenusAndShortcuts"] ?? "Հեռացնել կոնտեքստային մենյուները եւ դյուրանցումները";
            OptSettingsLabel.Text = loc["UninstallRemoveSettingsData"] ?? "Ջնջել կարգավորումները եւ տվյալները (%AppData%/Panak)";

            CheckDotNetStatus();

            CancelBtn.Content = loc["Cancel"] ?? "Չեղարկել";
            if (!_isCompleted) {
                MainActionBtn.Content = loc["Uninstall"] ?? "Ապատեղադրել";
            } else {
                MainActionBtn.Content = loc["Finish"] ?? "Փակել";
            }
        }

        private void CheckDotNetStatus() {
            var loc = LocalizationService.Instance;
            var (isInstalled, version) = StandaloneUninstallerService.CheckDotNet8DesktopRuntime();
            if (isInstalled) {
                OptDotNetLabel.Text = $"{loc["UninstallDotNet8"] ?? "Ջնջել .NET 8.0 Desktop Runtime"} (v{version})";
                UninstallDotNetCheckBox.IsEnabled = true;
            } else {
                OptDotNetLabel.Text = $"{loc["UninstallDotNet8"] ?? "Ջնջել .NET 8.0 Desktop Runtime"} {loc["UninstallNotInstalled"] ?? "(տեղադրված չէ)"}";
                UninstallDotNetCheckBox.IsEnabled = false;
                UninstallDotNetCheckBox.IsChecked = false;
            }
        }

        public void ApplyMaterialYouPalette(double hue) {
            try {
                hue = (hue % 360.0 + 360.0) % 360.0;
                bool isDark = _selectedThemeMode == "Dark" || (_selectedThemeMode == "Auto" && ShellHelper.IsWindowsDarkTheme());

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
                System.Windows.Media.Color wpfLogoContainer;
                System.Windows.Media.Color wpfLogoCloud;

                if (isDark) {
                    wpfSurface = toWpf(ShellHelper.ColorFromHsl(hue, 0.16, 0.11));
                    wpfContainer = toWpf(ShellHelper.ColorFromHsl(hue, 0.20, 0.18));
                    wpfCard = toWpf(ShellHelper.ColorFromHsl(hue, 0.18, 0.14));
                    wpfPrimary = toWpf(ShellHelper.ColorFromHsl(hue, 0.70, 0.72));
                    wpfOnPrimary = System.Windows.Media.Color.FromRgb(0x1C, 0x1B, 0x1F);
                    wpfOnSurface = System.Windows.Media.Color.FromRgb(0xE6, 0xE1, 0xE5);
                    wpfOnSurfaceVariant = System.Windows.Media.Color.FromRgb(0xCA, 0xC4, 0xD0);
                    wpfTrackInactive = toWpf(ShellHelper.ColorFromHsl(hue, 0.15, 0.26));

                    wpfLogoContainer = toWpf(ShellHelper.ColorFromHsl(hue, 0.25, 0.20));
                    wpfLogoCloud = toWpf(ShellHelper.ColorFromHsl(hue, 0.75, 0.78));
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

                    // Dynamic colors for Uninstaller SVG
                    var (cContainer, _, cCloud, _, _) = ShellHelper.GetLogoDynamicColors(hue, false);
                    wpfLogoContainer = toWpf(cContainer);
                    wpfLogoCloud = toWpf(cCloud);
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

                var wpfErrorContainer = isDark
                    ? toWpf(ShellHelper.ColorFromHsl((hue + 350.0) % 360.0, 0.45, 0.25))
                    : toWpf(ShellHelper.ColorFromHsl((hue + 350.0) % 360.0, 0.65, 0.88));
                var wpfOnErrorContainer = isDark
                    ? toWpf(ShellHelper.ColorFromHsl((hue + 350.0) % 360.0, 0.80, 0.90))
                    : toWpf(ShellHelper.ColorFromHsl((hue + 350.0) % 360.0, 0.80, 0.22));
                SetBrush("M3ErrorContainerBrush", wpfErrorContainer);
                SetBrush("M3OnErrorContainerBrush", wpfOnErrorContainer);

                SetBrush("LogoContainerBrush", wpfLogoContainer);
                SetBrush("LogoCloudBrush", wpfLogoCloud);

                UpdateTaskbarIcon(hue, isDark);
            } catch { }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                DragMove();
            }
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e) {
            WindowState = WindowState.Minimized;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e) {
            Close();
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

        private async void MainActionBtn_Click(object sender, RoutedEventArgs e) {
            if (_isCompleted) {
                Close();
                return;
            }

            var loc = LocalizationService.Instance;

            if (FolderLockService.Instance.HasAnyLockedFolders()) {
                MessageBox.Show(
                    loc["LockedFoldersExistWarning"] ?? "Առկա են կողպված պանակներ: Ապատեղադրելու համար խնդրում ենք նախ ապակողպել բոլոր պանակները Panak-ում:",
                    "Panak",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Transition to progress state
            PromptPanel.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;
            CancelBtn.Visibility = Visibility.Collapsed;
            MainActionBtn.IsEnabled = false;

            ProgressTitleText.Text = loc["UninstallingPanak"] ?? "Ապատեղադրվում է Panak-ը...";
            UninstallProgressBar.Value = 10;

            var progress = new Progress<(double percent, string status)>(p => {
                UninstallProgressBar.Value = Math.Clamp(p.percent * 100.0, 0.0, 100.0);
                ProgressStatusText.Text = p.status;
            });

            bool removeShortcuts = RemoveShortcutsCheckBox.IsChecked == true;
            bool removeSettings = RemoveSettingsCheckBox.IsChecked == true;
            bool uninstallDotNet = UninstallDotNetCheckBox.IsChecked == true;

            var (success, error) = await StandaloneUninstallerService.UninstallAsync(
                removeContextMenus: removeShortcuts,
                removeShortcuts: removeShortcuts,
                removeSettings: removeSettings,
                uninstallDotNet8: uninstallDotNet,
                progress: progress);

            // Show Completed state
            _isCompleted = true;
            ProgressPanel.Visibility = Visibility.Collapsed;
            CompletedPanel.Visibility = Visibility.Visible;

            if (success) {
                StatusCompletedTitle.Text = loc["OperationCompleted"] ?? "Ապատեղադրումն ավարտվեց";
                StatusCompletedSubtitle.Text = loc["UninstalledSuccess"] ?? "Panak-ը հաջողությամբ ապատեղադրված է:";
            } else {
                StatusCompletedTitle.Text = "Ապատեղադրումը ձախողվեց";
                StatusCompletedSubtitle.Text = !string.IsNullOrEmpty(error) ? error : "Ապատեղադրման ընթացքում տեղի ունեցավ սխալ:";
            }

            MainActionBtn.IsEnabled = true;
            MainActionBtn.Content = loc["Finish"] ?? "Փակել";
            MainActionBtn.Style = (Style)Resources["ActionButtonStyle"];
        }
    }
}

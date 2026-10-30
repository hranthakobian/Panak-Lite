using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Panak.Views;
using Panak.Models;

namespace Panak.Services {
    public class TrayManager : IDisposable {
        private const int WM_USER = 0x0400;
        private const int WM_TRAYICON = WM_USER + 101;

        private const int NIM_ADD = 0x00000000;
        private const int NIM_MODIFY = 0x00000001;
        private const int NIM_DELETE = 0x00000002;

        private const int NIF_MESSAGE = 0x00000001;
        private const int NIF_ICON = 0x00000002;
        private const int NIF_TIP = 0x00000004;
        private const int NIF_INFO = 0x00000010;
        private const int NIIF_INFO = 0x00000001;

        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;
        private const int NIN_BALLOONUSERCLICK = 0x0405;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public int uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static TrayManager? Instance { get; private set; }

        private HwndSource? _hwndSource;
        private IntPtr _hIcon = IntPtr.Zero;
        private bool _isAdded = false;
        private int _wmTaskbarCreated = 0;

        public event Action? SettingsRequested;
        public event Action? NewFolderRequested;
        public event Action? ExitRequested;

        public void Initialize() {
            Instance = this;
            // Create hidden message-only window
            HwndSourceParameters parameters = new HwndSourceParameters("PanakTrayMsgWindow") {
                WindowStyle = 0,
                ExtendedWindowStyle = 0
            };

            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(HwndMessageHook);

            _wmTaskbarCreated = RegisterWindowMessage("TaskbarCreated");

            AddOrUpdateTrayIcon();
        }

        private IntPtr LoadPanakTrayIcon() {
            try {
                var settings = SettingsService.Instance.Current;
                string col = string.IsNullOrEmpty(settings.ThemeColor) ? ShellHelper.GetDefaultPastelColorHex() : settings.ThemeColor;
                double hue = ShellHelper.GetHueFromHex(col);
                bool isDark = (settings.ThemeMode == "Dark") || (settings.ThemeMode != "Light" && ShellHelper.IsWindowsDarkTheme());
                using Bitmap logoBmp = IconGenerator.RenderPanakLogoBitmap(32, hue, isDark);
                return logoBmp.GetHicon();
            } catch { }

            try {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string icoPath = Path.Combine(baseDir, "Panak.ico");
                if (File.Exists(icoPath)) {
                    using var ico = new Icon(icoPath, 32, 32);
                    using var bmp = ico.ToBitmap();
                    return bmp.GetHicon();
                }

                string pngPath = Path.Combine(baseDir, "PanakLogo.png");
                if (!File.Exists(pngPath)) {
                    pngPath = Path.Combine(baseDir, "Panak logo.png");
                }
                if (File.Exists(pngPath)) {
                    using var orig = new Bitmap(pngPath);
                    using var resized = new Bitmap(orig, new System.Drawing.Size(32, 32));
                    return resized.GetHicon();
                }
            } catch { }

            // Fallback: render plate
            using (Bitmap fallbackBmp = IconGenerator.RenderPlateWithGrid(32, "Squircle", System.Drawing.Color.FromArgb(59, 130, 246), new System.Collections.Generic.List<Bitmap>())) {
                return fallbackBmp.GetHicon();
            }
        }

        private double? _overrideHue;
        private bool? _overrideIsDark;

        public void UpdateTrayIconTheme(double hue, bool isDark) {
            _overrideHue = hue;
            _overrideIsDark = isDark;
            _cachedContextMenu = null;

            if (_hwndSource == null) return;
            try {
                IntPtr oldIcon = _hIcon;
                using Bitmap logoBmp = IconGenerator.RenderPanakLogoBitmap(32, hue, isDark);
                _hIcon = logoBmp.GetHicon();

                NOTIFYICONDATA nid = new NOTIFYICONDATA {
                    cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                    hWnd = _hwndSource.Handle,
                    uID = 1001,
                    uFlags = NIF_ICON,
                    hIcon = _hIcon
                };

                if (_isAdded) {
                    Shell_NotifyIcon(NIM_MODIFY, ref nid);
                } else {
                    nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
                    nid.uCallbackMessage = WM_TRAYICON;
                    nid.szTip = "Panak";
                    _isAdded = Shell_NotifyIcon(NIM_ADD, ref nid);
                }

                if (oldIcon != IntPtr.Zero) {
                    DestroyIcon(oldIcon);
                }
            } catch { }
        }

        private void AddOrUpdateTrayIcon() {
            if (_hwndSource == null) return;
            if (_hIcon == IntPtr.Zero) {
                _hIcon = LoadPanakTrayIcon();
            }

            NOTIFYICONDATA nid = new NOTIFYICONDATA {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwndSource.Handle,
                uID = 1001,
                uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _hIcon,
                szTip = "Panak"
            };

            if (_isAdded) {
                Shell_NotifyIcon(NIM_MODIFY, ref nid);
            } else {
                _isAdded = Shell_NotifyIcon(NIM_ADD, ref nid);
                if (!_isAdded) {
                    // Taskbar notification area may still be starting up on Windows login. Retry in background.
                    System.Threading.Tasks.Task.Run(async () => {
                        for (int i = 0; i < 10 && !_isAdded; i++) {
                            await System.Threading.Tasks.Task.Delay(1500);
                            Application.Current?.Dispatcher?.Invoke(() => {
                                if (!_isAdded && _hwndSource != null) {
                                    _isAdded = Shell_NotifyIcon(NIM_ADD, ref nid);
                                }
                            });
                        }
                    });
                }
            }
        }

        private IntPtr HwndMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
            if (_wmTaskbarCreated != 0 && msg == _wmTaskbarCreated) {
                _isAdded = false;
                AddOrUpdateTrayIcon();
                handled = true;
                return IntPtr.Zero;
            }

            if (msg == WM_TRAYICON) {
                int mouseMsg = lParam.ToInt32();
                if (mouseMsg == WM_LBUTTONUP || mouseMsg == NIN_BALLOONUSERCLICK) {
                    Application.Current.Dispatcher.Invoke(() => {
                        SettingsRequested?.Invoke();
                    });
                    handled = true;
                } else if (mouseMsg == WM_RBUTTONUP) {
                    Application.Current.Dispatcher.Invoke(ShowContextMenu);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private ContextMenu? _cachedContextMenu;
        private string? _cachedMenuKey;

        public ContextMenu BuildContextMenu() {
            var loc = LocalizationService.Instance;
            var settings = SettingsService.Instance.Current;

            bool isDark = _overrideIsDark ?? ((settings.ThemeMode == "Dark") || (settings.ThemeMode != "Light" && ShellHelper.IsWindowsDarkTheme()));
            string themeColor = string.IsNullOrEmpty(settings.ThemeColor) ? ShellHelper.GetDefaultPastelColorHex() : settings.ThemeColor;
            double hue = _overrideHue ?? ShellHelper.GetHueFromHex(themeColor);
            hue = (hue % 360.0 + 360.0) % 360.0;
            string menuKey = $"{isDark}|{hue:F1}|{loc.CurrentLanguage}";
            if (_cachedContextMenu != null && _cachedMenuKey == menuKey) {
                return _cachedContextMenu;
            }

            System.Windows.Media.Color toWpf(System.Drawing.Color c) =>
                System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);

            System.Windows.Media.Color bgSurface, fgOnSurface, primaryColor, containerColor, borderCol, hoverBg, sepCol;
            if (isDark) {
                // Exactly identical to SettingsWindow's M3SurfaceBrush background in dark mode
                bgSurface = toWpf(ShellHelper.ColorFromHsl(hue, 0.16, 0.11));
                containerColor = toWpf(ShellHelper.ColorFromHsl(hue, 0.20, 0.18));
                primaryColor = toWpf(ShellHelper.ColorFromHsl(hue, 0.70, 0.72));
                fgOnSurface = System.Windows.Media.Color.FromRgb(0xE6, 0xE1, 0xE5);
                borderCol = System.Windows.Media.Color.FromArgb(40, 255, 255, 255);
                hoverBg = containerColor;
                sepCol = System.Windows.Media.Color.FromArgb(28, 255, 255, 255);
            } else {
                // Exactly identical to SettingsWindow's M3SurfaceBrush background in light mode
                var (sCol, cCol, pCol) = ShellHelper.GetMaterialYouTonalPalette(hue);
                var onSurfaceColor = ShellHelper.ColorFromHsl(hue, 0.40, 0.16);
                bgSurface = toWpf(sCol);
                containerColor = toWpf(cCol);
                primaryColor = toWpf(pCol);
                fgOnSurface = toWpf(onSurfaceColor);
                borderCol = System.Windows.Media.Color.FromArgb(32, 0, 0, 0);
                hoverBg = containerColor;
                sepCol = System.Windows.Media.Color.FromArgb(20, 0, 0, 0);
            }

            string hex(System.Windows.Media.Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            string bgSurfaceHex = hex(bgSurface);
            string borderHex = hex(borderCol);
            string hoverHex = hex(hoverBg);
            string primaryHex = hex(primaryColor);
            string onSurfaceHex = hex(fgOnSurface);
            string sepHex = hex(sepCol);

            ContextMenu menu = new ContextMenu {
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Placement = PlacementMode.MousePoint,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true,
                FocusVisualStyle = null
            };

            string menuTemplateXaml = $@"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='{{x:Type ContextMenu}}'>
    <Border x:Name='MenuBorder'
            Margin='12'
            CornerRadius='16'
            Background='{bgSurfaceHex}'
            BorderBrush='{borderHex}'
            BorderThickness='1'
            Padding='6'
            MinWidth='195'
            RenderTransformOrigin='0.5, 0.5'
            Opacity='0'>
        <Border.RenderTransform>
            <ScaleTransform ScaleX='0.88' ScaleY='0.88'/>
        </Border.RenderTransform>
        <StackPanel IsItemsHost='True'/>
    </Border>
    <ControlTemplate.Triggers>
        <EventTrigger RoutedEvent='FrameworkElement.Loaded'>
            <BeginStoryboard>
                <Storyboard>
                    <DoubleAnimation Storyboard.TargetName='MenuBorder'
                                     Storyboard.TargetProperty='Opacity'
                                     From='0' To='1' Duration='0:0:0.18'>
                        <DoubleAnimation.EasingFunction>
                            <CubicEase EasingMode='EaseOut'/>
                        </DoubleAnimation.EasingFunction>
                    </DoubleAnimation>
                    <DoubleAnimation Storyboard.TargetName='MenuBorder'
                                     Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleX)'
                                     From='0.88' To='1.0' Duration='0:0:0.24'>
                        <DoubleAnimation.EasingFunction>
                            <BackEase Amplitude='0.22' EasingMode='EaseOut'/>
                        </DoubleAnimation.EasingFunction>
                    </DoubleAnimation>
                    <DoubleAnimation Storyboard.TargetName='MenuBorder'
                                     Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleY)'
                                     From='0.88' To='1.0' Duration='0:0:0.24'>
                        <DoubleAnimation.EasingFunction>
                            <BackEase Amplitude='0.22' EasingMode='EaseOut'/>
                        </DoubleAnimation.EasingFunction>
                    </DoubleAnimation>
                </Storyboard>
            </BeginStoryboard>
        </EventTrigger>
    </ControlTemplate.Triggers>
</ControlTemplate>";

            menu.Template = (ControlTemplate)XamlReader.Parse(menuTemplateXaml);

            MenuItem CreateItem(string icon, string header, RoutedEventHandler onClick) {
                MenuItem item = new MenuItem {
                    Header = header,
                    Cursor = Cursors.Hand,
                    Height = 36,
                    FocusVisualStyle = null
                };

                string escapedIcon = SecurityElement.Escape(icon ?? "");
                string escapedHeader = SecurityElement.Escape(header ?? "");

                string itemTemplateXaml = $@"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='{{x:Type MenuItem}}'>
    <Grid x:Name='ItemRoot' Margin='0,1' Cursor='Hand' RenderTransformOrigin='0.5, 0.5'>
        <Grid.RenderTransform>
            <ScaleTransform ScaleX='1.0' ScaleY='1.0'/>
        </Grid.RenderTransform>
        <Border x:Name='ItemBorder'
                CornerRadius='10'
                Padding='10,0,12,0'
                Height='36'>
            <Border.Background>
                <SolidColorBrush Color='Transparent'/>
            </Border.Background>
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width='26'/>
                    <ColumnDefinition Width='*'/>
                </Grid.ColumnDefinitions>
                
                <TextBlock x:Name='IconText'
                           Text='{escapedIcon}'
                           FontSize='13'
                           VerticalAlignment='Center'
                           HorizontalAlignment='Center'
                           Grid.Column='0'
                           RenderTransformOrigin='0.5, 0.5'>
                    <TextBlock.RenderTransform>
                        <ScaleTransform ScaleX='1.0' ScaleY='1.0'/>
                    </TextBlock.RenderTransform>
                </TextBlock>

                <TextBlock x:Name='HeaderText'
                           Text='{escapedHeader}'
                           FontSize='12.5'
                           FontWeight='Medium'
                           VerticalAlignment='Center'
                           Grid.Column='1'
                           Margin='4,0,0,0'>
                    <TextBlock.Foreground>
                        <SolidColorBrush Color='{onSurfaceHex}'/>
                    </TextBlock.Foreground>
                    <TextBlock.RenderTransform>
                        <TranslateTransform X='0'/>
                    </TextBlock.RenderTransform>
                </TextBlock>
            </Grid>
        </Border>
    </Grid>
    <ControlTemplate.Triggers>
        <Trigger Property='IsHighlighted' Value='True'>
            <Trigger.EnterActions>
                <BeginStoryboard>
                    <Storyboard>
                        <ColorAnimation Storyboard.TargetName='ItemBorder'
                                        Storyboard.TargetProperty='(Border.Background).(SolidColorBrush.Color)'
                                        To='{hoverHex}' Duration='0:0:0.15'>
                            <ColorAnimation.EasingFunction>
                                <CubicEase EasingMode='EaseOut'/>
                            </ColorAnimation.EasingFunction>
                        </ColorAnimation>
                        <ColorAnimation Storyboard.TargetName='HeaderText'
                                        Storyboard.TargetProperty='(TextBlock.Foreground).(SolidColorBrush.Color)'
                                        To='{primaryHex}' Duration='0:0:0.15'>
                            <ColorAnimation.EasingFunction>
                                <CubicEase EasingMode='EaseOut'/>
                            </ColorAnimation.EasingFunction>
                        </ColorAnimation>
                        <DoubleAnimation Storyboard.TargetName='IconText'
                                         Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleX)'
                                         To='1.18' Duration='0:0:0.18'>
                            <DoubleAnimation.EasingFunction>
                                <BackEase Amplitude='0.3' EasingMode='EaseOut'/>
                            </DoubleAnimation.EasingFunction>
                        </DoubleAnimation>
                        <DoubleAnimation Storyboard.TargetName='IconText'
                                         Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleY)'
                                         To='1.18' Duration='0:0:0.18'>
                            <DoubleAnimation.EasingFunction>
                                <BackEase Amplitude='0.3' EasingMode='EaseOut'/>
                            </DoubleAnimation.EasingFunction>
                        </DoubleAnimation>
                        <DoubleAnimation Storyboard.TargetName='HeaderText'
                                         Storyboard.TargetProperty='(UIElement.RenderTransform).(TranslateTransform.X)'
                                         To='3' Duration='0:0:0.15'>
                            <DoubleAnimation.EasingFunction>
                                <CubicEase EasingMode='EaseOut'/>
                            </DoubleAnimation.EasingFunction>
                        </DoubleAnimation>
                    </Storyboard>
                </BeginStoryboard>
            </Trigger.EnterActions>
            <Trigger.ExitActions>
                <BeginStoryboard>
                    <Storyboard>
                        <ColorAnimation Storyboard.TargetName='ItemBorder'
                                        Storyboard.TargetProperty='(Border.Background).(SolidColorBrush.Color)'
                                        To='Transparent' Duration='0:0:0.14'>
                            <ColorAnimation.EasingFunction>
                                <CubicEase EasingMode='EaseOut'/>
                            </ColorAnimation.EasingFunction>
                        </ColorAnimation>
                        <ColorAnimation Storyboard.TargetName='HeaderText'
                                        Storyboard.TargetProperty='(TextBlock.Foreground).(SolidColorBrush.Color)'
                                        To='{onSurfaceHex}' Duration='0:0:0.14'>
                            <ColorAnimation.EasingFunction>
                                <CubicEase EasingMode='EaseOut'/>
                            </ColorAnimation.EasingFunction>
                        </ColorAnimation>
                        <DoubleAnimation Storyboard.TargetName='IconText'
                                         Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleX)'
                                         To='1.0' Duration='0:0:0.14'>
                            <DoubleAnimation.EasingFunction>
                                <CubicEase EasingMode='EaseOut'/>
                            </DoubleAnimation.EasingFunction>
                        </DoubleAnimation>
                        <DoubleAnimation Storyboard.TargetName='IconText'
                                         Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleY)'
                                         To='1.0' Duration='0:0:0.14'>
                            <DoubleAnimation.EasingFunction>
                                <CubicEase EasingMode='EaseOut'/>
                            </DoubleAnimation.EasingFunction>
                        </DoubleAnimation>
                        <DoubleAnimation Storyboard.TargetName='HeaderText'
                                         Storyboard.TargetProperty='(UIElement.RenderTransform).(TranslateTransform.X)'
                                         To='0' Duration='0:0:0.14'>
                            <DoubleAnimation.EasingFunction>
                                <CubicEase EasingMode='EaseOut'/>
                            </DoubleAnimation.EasingFunction>
                        </DoubleAnimation>
                    </Storyboard>
                </BeginStoryboard>
            </Trigger.ExitActions>
        </Trigger>
        <Trigger Property='IsPressed' Value='True'>
            <Trigger.EnterActions>
                <BeginStoryboard>
                    <Storyboard>
                        <DoubleAnimation Storyboard.TargetName='ItemRoot'
                                         Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleX)'
                                         To='0.95' Duration='0:0:0.06'>
                            <DoubleAnimation.EasingFunction>
                                <QuadraticEase EasingMode='EaseOut'/>
                            </DoubleAnimation.EasingFunction>
                        </DoubleAnimation>
                        <DoubleAnimation Storyboard.TargetName='ItemRoot'
                                         Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleY)'
                                         To='0.95' Duration='0:0:0.06'>
                            <DoubleAnimation.EasingFunction>
                                <QuadraticEase EasingMode='EaseOut'/>
                            </DoubleAnimation.EasingFunction>
                        </DoubleAnimation>
                    </Storyboard>
                </BeginStoryboard>
            </Trigger.EnterActions>
            <Trigger.ExitActions>
                <BeginStoryboard>
                    <Storyboard>
                        <DoubleAnimation Storyboard.TargetName='ItemRoot'
                                         Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleX)'
                                         To='1.0' Duration='0:0:0.14'>
                            <DoubleAnimation.EasingFunction>
                                <BackEase Amplitude='0.25' EasingMode='EaseOut'/>
                            </DoubleAnimation.EasingFunction>
                        </DoubleAnimation>
                        <DoubleAnimation Storyboard.TargetName='ItemRoot'
                                         Storyboard.TargetProperty='(UIElement.RenderTransform).(ScaleTransform.ScaleY)'
                                         To='1.0' Duration='0:0:0.14'>
                            <DoubleAnimation.EasingFunction>
                                <BackEase Amplitude='0.25' EasingMode='EaseOut'/>
                            </DoubleAnimation.EasingFunction>
                        </DoubleAnimation>
                    </Storyboard>
                </BeginStoryboard>
            </Trigger.ExitActions>
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>";

                item.Template = (ControlTemplate)XamlReader.Parse(itemTemplateXaml);
                item.Click += onClick;
                return item;
            }

            Separator CreateSeparator() {
                Separator sep = new Separator {
                    Height = 1,
                    Margin = new Thickness(0, 3, 0, 3)
                };
                string sepTemplateXaml = $@"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='{{x:Type Separator}}'>
    <Border Height='1' Margin='8,2,8,2' Background='{sepHex}' CornerRadius='0.5'/>
</ControlTemplate>";
                sep.Template = (ControlTemplate)XamlReader.Parse(sepTemplateXaml);
                return sep;
            }

            menu.Items.Add(CreateItem("⚙️", loc["Settings"], (s, e) => SettingsRequested?.Invoke()));
            menu.Items.Add(CreateItem("📁", loc["NewFolder"], (s, e) => NewFolderRequested?.Invoke()));
            menu.Items.Add(CreateItem("🔄", loc["CheckForUpdates"], async (s, e) => {
                bool found = await GitHubUpdateService.Instance.CheckForUpdatesAsync(manual: true);
                if (found) {
                    ShowBalloon(loc["UpdateAvailable"], $"{loc["LatestVersion"]}: v{GitHubUpdateService.Instance.LatestVersionString}");
                } else if (GitHubUpdateService.Instance.Status == UpdateStatus.UpToDate) {
                    ShowBalloon(loc["Updates"], loc["UpToDate"]);
                } else if (GitHubUpdateService.Instance.Status == UpdateStatus.Error) {
                    ShowBalloon(loc["Updates"], loc["UpdateError"]);
                }
            }));

            menu.Items.Add(CreateSeparator());

            menu.Items.Add(CreateItem("✕", loc["Exit"], (s, e) => ExitRequested?.Invoke()));

            _cachedContextMenu = menu;
            _cachedMenuKey = menuKey;
            return menu;
        }

        private void ShowContextMenu() {
            var menu = BuildContextMenu();
            if (_hwndSource != null) {
                ShellHelper.SetForegroundWindow(_hwndSource.Handle);
            }
            menu.IsOpen = true;
        }

        public void ShowBalloon(string title, string message) {
            if (!_isAdded || _hwndSource == null) return;
            NOTIFYICONDATA nid = new NOTIFYICONDATA {
                cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                hWnd = _hwndSource.Handle,
                uID = 1001,
                uFlags = NIF_INFO,
                szInfoTitle = title ?? "",
                szInfo = message ?? "",
                dwInfoFlags = NIIF_INFO
            };
            Shell_NotifyIcon(NIM_MODIFY, ref nid);
        }

        public void Dispose() {
            if (_isAdded && _hwndSource != null) {
                NOTIFYICONDATA nid = new NOTIFYICONDATA {
                    cbSize = Marshal.SizeOf(typeof(NOTIFYICONDATA)),
                    hWnd = _hwndSource.Handle,
                    uID = 1001
                };
                Shell_NotifyIcon(NIM_DELETE, ref nid);
                _isAdded = false;
            }

            if (_hIcon != IntPtr.Zero) {
                ShellHelper.DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }

            _hwndSource?.Dispose();
            _hwndSource = null;
        }
    }
}

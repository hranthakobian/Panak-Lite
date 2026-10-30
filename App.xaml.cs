using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Panak.Services;
using Panak.Views;

namespace Panak {
    public partial class App : Application {
        private static Mutex? _instanceMutex;
        private static bool _ownsMutex = false;
        private SingleInstanceIpc? _ipc;
        private HookService? _hookService;
        private TrayManager? _trayManager;
        private SettingsWindow? _settingsWindow;
        private WelcomeWindow? _welcomeWindow;

        protected override void OnStartup(StartupEventArgs e) {
            try {
                System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
                IntPtr hDesk = ShellHelper.OpenDesktop("Default", 0, false, 0x01FF);
                if (hDesk != IntPtr.Zero) {
                    ShellHelper.SetThreadDesktop(hDesk);
                }
            } catch { }

            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, ev) => {
                try {
                    string details = ev.ExceptionObject is Exception ex
                        ? CrashReportWindow.FormatException(ex)
                        : $"Unhandled Object: {ev.ExceptionObject}";
                    CrashReportWindow.ShowReport(details);
                } catch { }
            };

            DispatcherUnhandledException += (s, ev) => {
                try {
                    CrashReportWindow.ShowReport(ev.Exception);
                } catch { }
                ev.Handled = true;
            };

            TaskScheduler.UnobservedTaskException += (s, ev) => {
                try {
                    CrashReportWindow.ShowReport(ev.Exception);
                } catch { }
                ev.SetObserved();
            };

            string[] args = e.Args;

            // Handle direct installer/uninstaller flags first
            if (args.Length > 0 && args[0].Equals("--install-silent", StringComparison.OrdinalIgnoreCase)) {
                InstallerService.Install(createDesktopShortcut: true, createStartMenuShortcut: true, autoStart: true);
                Shutdown();
                return;
            }

            if (args.Length > 0 && args[0].Equals("--install", StringComparison.OrdinalIgnoreCase)) {
                ShutdownMode = ShutdownMode.OnLastWindowClose;
                new InstallerWindow(isUninstallMode: false).Show();
                return;
            }

            if (args.Length > 0 && args[0].Equals("--uninstall", StringComparison.OrdinalIgnoreCase)) {
                ShutdownMode = ShutdownMode.OnLastWindowClose;
                new InstallerWindow(isUninstallMode: true).Show();
                return;
            }

            // Single instance Mutex
            bool isFirstInstance;
            try {
                _instanceMutex = new Mutex(true, "PanakSingleInstanceApplicationMutex_2026", out isFirstInstance);
                _ownsMutex = isFirstInstance;
            } catch (AbandonedMutexException) {
                isFirstInstance = true;
                _ownsMutex = true;
            } catch {
                isFirstInstance = false;
                _ownsMutex = false;
            }

            if (!isFirstInstance) {
                // Secondary instance: send command via IPC to primary instance and exit
                HandleSecondaryInstance(args);
                Shutdown();
                return;
            }

            // Primary instance: start background services
            try {
                var settings = SettingsService.Instance.Current;
                if (!string.IsNullOrEmpty(settings?.Language)) {
                    LocalizationService.Instance.CurrentLanguage = settings.Language;
                }
                _ = LocalizationService.Instance.InitOtaAsync();
            } catch { }

            InitializeBackgroundServices();

            // Ensure auto-start and context menus are properly registered if enabled
            try {
                string currentExe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (!string.IsNullOrEmpty(currentExe)) {
                    InstallerService.EnsureAutoStartRegistered(currentExe);
                    if (ContextMenuRegistry.IsRegistered()) {
                        ContextMenuRegistry.RegisterAll(currentExe, LocalizationService.Instance.CurrentLanguage);
                    }
                }
            } catch { }

            // Handle initial launch arguments
            HandleInitialArgs(args);
        }

        private void InitializeBackgroundServices() {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 1. Start IPC Server
            _ipc = new SingleInstanceIpc();
            _ipc.CommandReceived += OnIpcCommandReceived;
            _ipc.StartServer();

            // 2. Start Global Hooks
            _hookService = new HookService();
            _hookService.IsPopupOpenFunc = () => PanakManager.Instance.IsPopupOpen;
            _hookService.IsPopupWithinGracePeriodFunc = () => PanakManager.Instance.IsPopupWithinGracePeriod;
            _hookService.IsPopupPinnedFunc = () => PanakManager.Instance.IsPopupPinned;
            _hookService.GetPopupHwndFunc = () => PanakManager.Instance.GetPopupHwnd();
            _hookService.CloseRequested += () => PanakManager.Instance.ClosePopup();

            _hookService.LeftDoubleClickTriggered += (item) => {
                if (!string.IsNullOrEmpty(item.Path) && Directory.Exists(item.Path)) {
                    PanakManager.Instance.OpenFolder(item.Path, item.BoundingBox);
                }
            };

            _hookService.SpaceQuickLookTriggered += (item) => {
                if (!string.IsNullOrEmpty(item.Path) && Directory.Exists(item.Path)) {
                    PanakManager.Instance.OpenFolder(item.Path, item.BoundingBox);
                }
            };

            _hookService.Start();

            // 3. Start Tray Manager
            _trayManager = new TrayManager();
            _trayManager.SettingsRequested += ShowSettings;
            _trayManager.NewFolderRequested += CreateNewDesktopPanakFolder;
            _trayManager.ExitRequested += ExitApp;
            _trayManager.Initialize();

            // 4. Start GitHub Real-time Updater (skip when running as MSIX/Microsoft Store package)
            if (!ShellHelper.IsPackagedProcess()) {
                GitHubUpdateService.Instance.UpdateAvailableEvent += (ver, notes) => {
                    Dispatcher.BeginInvoke(() => {
                        var loc = LocalizationService.Instance;
                        _trayManager?.ShowBalloon(loc["UpdateAvailable"], $"{loc["LatestVersion"]}: v{ver}");
                    });
                };
                GitHubUpdateService.Instance.Start();
            }
        }

        private void HandleSecondaryInstance(string[] args) {
            int cx = 0, cy = 0;
            if (ShellHelper.GetCursorPos(out var pt)) {
                cx = pt.X;
                cy = pt.Y;
            }

            if (args.Length >= 2 && args[0].Equals("--open", StringComparison.OrdinalIgnoreCase)) {
                SingleInstanceIpc.SendCommand($"OPEN|{args[1]}|{cx}|{cy}");
            } else if (args.Length >= 2 && args[0].Equals("--unpack", StringComparison.OrdinalIgnoreCase)) {
                SingleInstanceIpc.SendCommand($"UNPACK|{args[1]}");
            } else if (args.Length >= 2 && args[0].Equals("--create", StringComparison.OrdinalIgnoreCase)) {
                SingleInstanceIpc.SendCommand($"CREATE|{args[1]}|{cx}|{cy}");
            } else if (args.Length >= 2 && args[0].Equals("--convert", StringComparison.OrdinalIgnoreCase)) {
                SingleInstanceIpc.SendCommand($"CONVERT|{args[1]}|{cx}|{cy}");
            } else if (args.Length >= 2 && args[0].Equals("--new", StringComparison.OrdinalIgnoreCase)) {
                SingleInstanceIpc.SendCommand($"NEW|{args[1]}|{cx}|{cy}");
            } else if (args.Length >= 2 && args[0].Equals("--collect-file", StringComparison.OrdinalIgnoreCase)) {
                string tempFile = args[1];
                string targetDir = args.Length >= 3 ? args[2] : string.Empty;
                if (args.Length >= 5 && int.TryParse(args[3], out int argX) && int.TryParse(args[4], out int argY)) {
                    cx = argX;
                    cy = argY;
                }
                if (File.Exists(tempFile)) {
                    string[] files = File.ReadAllLines(tempFile);
                    try { File.Delete(tempFile); } catch { }
                    if (files.Length > 0) {
                        string filePayload = string.Join("*", files);
                        SingleInstanceIpc.SendCommand($"COLLECT|{targetDir}|{cx}|{cy}|{filePayload}");
                    }
                }
            } else if (args.Length >= 1 && args[0].Equals("--welcome", StringComparison.OrdinalIgnoreCase)) {
                SingleInstanceIpc.SendCommand("WELCOME");
            } else if (args.Length >= 1 && args[0].Equals("--settings", StringComparison.OrdinalIgnoreCase)) {
                SingleInstanceIpc.SendCommand("SETTINGS");
            } else if (args.Length >= 1 && args[0].Equals("--background", StringComparison.OrdinalIgnoreCase)) {
                // Secondary instance launched with --background: exit silently without disturbing primary instance
                return;
            } else {
                SingleInstanceIpc.SendCommand("SETTINGS");
            }
        }

        private void HandleInitialArgs(string[] args) {
            Point? clickPt = null;
            if (ShellHelper.GetCursorPos(out var pt)) {
                clickPt = new Point(pt.X, pt.Y);
            }

            if (args.Length >= 2 && args[0].Equals("--open", StringComparison.OrdinalIgnoreCase)) {
                OpenFolderFromPath(args[1], clickPt);
            } else if (args.Length >= 2 && args[0].Equals("--unpack", StringComparison.OrdinalIgnoreCase)) {
                PanakManager.Instance.UnpackPanakFolder(args[1]);
            } else if (args.Length >= 2 && args[0].Equals("--create", StringComparison.OrdinalIgnoreCase)) {
                CreateAndOpenFromPath(args[1], clickPt);
            } else if (args.Length >= 2 && args[0].Equals("--convert", StringComparison.OrdinalIgnoreCase)) {
                PanakManager.Instance.ConvertToPanakFolder(args[1]);
                OpenFolderFromPath(args[1], clickPt);
            } else if (args.Length >= 2 && args[0].Equals("--new", StringComparison.OrdinalIgnoreCase)) {
                CreateAndOpenNewInDir(args[1], clickPt);
            } else if (args.Length >= 2 && args[0].Equals("--collect-file", StringComparison.OrdinalIgnoreCase)) {
                string tempFile = args[1];
                string targetDir = args.Length >= 3 ? args[2] : string.Empty;
                if (args.Length >= 5 && double.TryParse(args[3], out double argX) && double.TryParse(args[4], out double argY)) {
                    clickPt = new Point(argX, argY);
                }
                if (File.Exists(tempFile)) {
                    string[] files = File.ReadAllLines(tempFile);
                    try { File.Delete(tempFile); } catch { }
                    if (files.Length > 0) {
                        Rect bounds = clickPt != null
                            ? new Rect(clickPt.Value.X - 30, clickPt.Value.Y - 30, 60, 60)
                            : new Rect(100, 100, 60, 60);
                        PanakManager.Instance.CollectFilesIntoPanakFolder(files, targetDir, bounds);
                    }
                }
            } else if (args.Length >= 1 && args[0].Equals("--welcome", StringComparison.OrdinalIgnoreCase)) {
                ShowWelcome();
            } else if (args.Length >= 1 && args[0].Equals("--settings", StringComparison.OrdinalIgnoreCase)) {
                ShowSettings();
            } else if (args.Length == 0 || !args[0].Equals("--background", StringComparison.OrdinalIgnoreCase)) {
                // If launched directly without args, show Settings window on startup
                ShowSettings();
            }
        }

        private void OnIpcCommandReceived(string command) {
            Dispatcher.Invoke(() => {
                string[] parts = command.Split('|', 5);
                string action = parts[0].ToUpperInvariant();
                string payload = parts.Length > 1 ? parts[1] : string.Empty;
                Point? clickPt = null;
                if (parts.Length >= 4 && double.TryParse(parts[2], out double cx) && double.TryParse(parts[3], out double cy)) {
                    clickPt = new Point(cx, cy);
                } else if (ShellHelper.GetCursorPos(out var curPt)) {
                    clickPt = new Point(curPt.X, curPt.Y);
                }

                switch (action) {
                    case "OPEN": {
                        OpenFolderFromPath(payload, clickPt);
                        break;
                    }
                    case "UNPACK": {
                        PanakManager.Instance.UnpackPanakFolder(payload);
                        break;
                    }
                    case "CREATE": {
                        CreateAndOpenFromPath(payload, clickPt);
                        break;
                    }
                    case "CONVERT": {
                        PanakManager.Instance.ConvertToPanakFolder(payload);
                        OpenFolderFromPath(payload, clickPt);
                        break;
                    }
                    case "NEW": {
                        CreateAndOpenNewInDir(payload, clickPt);
                        break;
                    }
                    case "COLLECT": {
                        string targetDir = payload;
                        string[] files = parts.Length > 4
                            ? parts[4].Split(new[] { '*' }, StringSplitOptions.RemoveEmptyEntries)
                            : Array.Empty<string>();
                        if (files.Length > 0) {
                            Rect bounds = clickPt != null
                                ? new Rect(clickPt.Value.X - 30, clickPt.Value.Y - 30, 60, 60)
                                : new Rect(100, 100, 60, 60);
                            PanakManager.Instance.CollectFilesIntoPanakFolder(files, targetDir, bounds);
                        }
                        break;
                    }
                    case "WELCOME": {
                        ShowWelcome();
                        break;
                    }
                    case "SETTINGS": {
                        ShowSettings();
                        break;
                    }
                }
            });
        }

        private void OpenFolderFromPath(string path, Point? clickPt = null) {
            if (Directory.Exists(path)) {
                Rect bounds = ExplorerHelper.GetBoundingBoxForPath(path);
                if ((bounds.IsEmpty || (bounds.X < 25 && bounds.Y < 25)) && clickPt != null) {
                    bounds = new Rect(clickPt.Value.X - 24, clickPt.Value.Y - 24, 48, 48);
                }
                PanakManager.Instance.OpenFolder(path, bounds);
            }
        }

        private void CreateAndOpenFromPath(string filePath, Point? clickPt = null) {
            var selected = ExplorerHelper.GetSelectedPathsForTarget(filePath);
            string[] filesToCollect;
            if (selected.Count > 1 && selected.Contains(filePath, StringComparer.OrdinalIgnoreCase)) {
                filesToCollect = selected.ToArray();
            } else if (selected.Count > 0) {
                filesToCollect = selected.ToArray();
            } else {
                filesToCollect = new[] { filePath };
            }
            string parentDir = Path.GetDirectoryName(filePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            Rect bounds = ExplorerHelper.GetBoundingBoxForPath(filePath);
            if ((bounds.IsEmpty || (bounds.X < 25 && bounds.Y < 25)) && clickPt != null) {
                bounds = new Rect(clickPt.Value.X - 24, clickPt.Value.Y - 24, 48, 48);
            }
            PanakManager.Instance.CollectFilesIntoPanakFolder(filesToCollect, parentDir, bounds);
        }

        private void CreateAndOpenNewInDir(string parentDir, Point? clickPt = null) {
            if (clickPt == null && ShellHelper.GetCursorPos(out var curPt)) {
                clickPt = new Point(curPt.X, curPt.Y);
            }
            string newFolder = PanakManager.Instance.CreatePanakFolder(parentDir, targetPosition: clickPt);
            if (!string.IsNullOrEmpty(newFolder)) {
                Rect bounds = (clickPt != null)
                    ? new Rect(clickPt.Value.X - 24, clickPt.Value.Y - 24, 48, 48)
                    : ExplorerHelper.GetBoundingBoxForPath(newFolder);
                PanakManager.Instance.OpenFolder(newFolder, bounds);
            }
        }

        private void CreateNewDesktopPanakFolder() {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            Point? clickPt = null;
            if (ShellHelper.GetCursorPos(out var pt)) {
                if (pt.Y > SystemParameters.WorkArea.Bottom - 30 || pt.X > SystemParameters.WorkArea.Right - 30) {
                    clickPt = new Point(SystemParameters.WorkArea.Right - 160, SystemParameters.WorkArea.Bottom - 160);
                } else {
                    clickPt = new Point(pt.X, pt.Y);
                }
            }
            string newFolder = PanakManager.Instance.CreatePanakFolder(desktop, targetPosition: clickPt);
            if (!string.IsNullOrEmpty(newFolder)) {
                Rect bounds = (clickPt != null)
                    ? new Rect(clickPt.Value.X - 24, clickPt.Value.Y - 24, 48, 48)
                    : ExplorerHelper.GetBoundingBoxForPath(newFolder);
                PanakManager.Instance.OpenFolder(newFolder, bounds);
            }
        }

        public void ShowSettings() {
            if (_settingsWindow == null || !_settingsWindow.IsLoaded) {
                _settingsWindow = new SettingsWindow();
                _settingsWindow.Closed += (s, e) => _settingsWindow = null;
                _settingsWindow.Show();
            } else {
                _settingsWindow.Activate();
            }
        }

        public void ShowWelcome() {
            Dispatcher.Invoke(() => {
                if (_welcomeWindow == null || !_welcomeWindow.IsLoaded) {
                    _welcomeWindow = new WelcomeWindow();
                    _welcomeWindow.Closed += (s, e) => _welcomeWindow = null;
                    _welcomeWindow.Show();
                } else {
                    _welcomeWindow.Activate();
                }
            });
        }

        private void ExitApp() {
            _hookService?.Dispose();
            _trayManager?.Dispose();
            _ipc?.Dispose();
            if (_ownsMutex) {
                try {
                    _instanceMutex?.ReleaseMutex();
                } catch { }
                _ownsMutex = false;
            }
            try {
                _instanceMutex?.Dispose();
            } catch { }
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e) {
            GitHubUpdateService.Instance.Dispose();
            _hookService?.Dispose();
            _trayManager?.Dispose();
            _ipc?.Dispose();
            if (_ownsMutex) {
                try {
                    _instanceMutex?.ReleaseMutex();
                } catch { }
                _ownsMutex = false;
            }
            try {
                _instanceMutex?.Dispose();
            } catch { }
            base.OnExit(e);
        }
    }
}

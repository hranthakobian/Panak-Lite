using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Panak.Models;
using Panak.Views;

namespace Panak.Services {
    public class PanakManager {
        private static PanakManager? _instance;
        public static PanakManager Instance => _instance ??= new PanakManager();

        private readonly List<FolderPopupWindow> _openPopups = new();
        private readonly List<BackdropWindow> _backdropWindows = new();
        private readonly Dictionary<string, FileSystemWatcher> _parentWatchers = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _knownFolders = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, System.Threading.Timer> _debounceTimers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> _lastUpdateTicks = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, long> _creationTimes = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _debounceLock = new();

        public bool IsPopupOpen => _openPopups.Any(w => w.IsVisible);
        public bool IsPopupWithinGracePeriod => _openPopups.Any(w => w.IsWithinOpenGracePeriod());
        public bool IsPopupPinned => _openPopups.Count > 0 && _openPopups.All(w => w.IsPinned);

        public IntPtr GetPopupHwnd() {
            var unpinned = _openPopups.LastOrDefault(w => !w.IsPinned && w.IsVisible);
            var target = unpinned ?? _openPopups.LastOrDefault(w => w.IsVisible);
            if (target != null) {
                return new System.Windows.Interop.WindowInteropHelper(target).Handle;
            }
            return IntPtr.Zero;
        }

        public void UpdateOpenPopupThemeColor(string hexColor) {
            foreach (var popup in _openPopups.ToList()) {
                try {
                    if (popup.Dispatcher.Thread.IsAlive && !popup.Dispatcher.HasShutdownStarted) {
                        if (popup.Dispatcher.CheckAccess()) {
                            popup.ApplyThemeColor(hexColor);
                        } else {
                            popup.Dispatcher.BeginInvoke(() => {
                                try { popup?.ApplyThemeColor(hexColor); } catch { }
                            });
                        }
                    }
                } catch { }
            }
        }

        public void UpdateOpenPopupThemeMode(string mode) {
            foreach (var popup in _openPopups.ToList()) {
                try {
                    if (popup.Dispatcher.Thread.IsAlive && !popup.Dispatcher.HasShutdownStarted) {
                        if (popup.Dispatcher.CheckAccess()) {
                            popup.ApplyThemeMode(mode, animate: true);
                        } else {
                            popup.Dispatcher.BeginInvoke(() => {
                                try { popup?.ApplyThemeMode(mode, animate: true); } catch { }
                            });
                        }
                    }
                } catch { }
            }
        }

        public void UpdateOpenPopupOpacity(double opacity) {
            foreach (var popup in _openPopups.ToList()) {
                try {
                    if (popup.Dispatcher.Thread.IsAlive && !popup.Dispatcher.HasShutdownStarted) {
                        if (popup.Dispatcher.CheckAccess()) {
                            popup.ApplyOpacity(opacity);
                        } else {
                            popup.Dispatcher.BeginInvoke(() => {
                                try { popup?.ApplyOpacity(opacity); } catch { }
                            });
                        }
                    }
                } catch { }
            }
        }

        public void UpdateOpenPopupSettings() {
            foreach (var popup in _openPopups.ToList()) {
                try {
                    if (popup.Dispatcher.Thread.IsAlive && !popup.Dispatcher.HasShutdownStarted) {
                        if (popup.Dispatcher.CheckAccess()) {
                            popup.ApplySettingsFromGlobal();
                        } else {
                            popup.Dispatcher.BeginInvoke(() => {
                                try { popup?.ApplySettingsFromGlobal(); } catch { }
                            });
                        }
                    }
                } catch { }
            }
        }

        public PanakManager() {
            ScanDesktopForPanakFolders();
        }

        public void ScanDesktopForPanakFolders() {
            try {
                string[] scanDirs = new[] {
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Desktop")
                }
                .Where(d => !string.IsNullOrEmpty(d))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

                foreach (var dir in scanDirs) {
                    if (Directory.Exists(dir)) {
                        foreach (var sub in Directory.GetDirectories(dir)) {
                            if (CheckIfPanakFolder(sub)) {
                                lock (_knownFolders) {
                                    _knownFolders.Add(sub);
                                }
                                EnsurePanakFolderIntegrity(sub);
                                WatchFolder(sub);
                            } else {
                                CleanupOrphanedPanakFolder(sub);
                            }
                        }

                        WatchDesktopDirectory(dir);
                    }
                }
            } catch {
                // Ignore initial scan errors
            }
        }

        public void CleanupOrphanedPanakFolder(string folderPath) {
            try {
                if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath) || CheckIfPanakFolder(folderPath)) {
                    return;
                }

                string[] panakIcos = Directory.GetFiles(folderPath, "panak_icon*.ico");
                if (panakIcos.Length == 0) {
                    return;
                }

                UnwatchFolder(folderPath);

                var dirInfo = new DirectoryInfo(folderPath);
                var allEntries = dirInfo.GetFileSystemInfos();
                bool hasNonPanakEntries = allEntries.Any(f =>
                    !f.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) &&
                    !f.Name.StartsWith("panak_icon", StringComparison.OrdinalIgnoreCase) &&
                    !f.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));

                if (!hasNonPanakEntries) {
                    // Empty ghost folder left behind after contents were moved out
                    foreach (var entry in allEntries) {
                        try {
                            File.SetAttributes(entry.FullName, FileAttributes.Normal);
                            entry.Delete();
                        } catch { }
                    }

                    for (int attempt = 0; attempt < 5; attempt++) {
                        try {
                            if (!Directory.Exists(folderPath)) break;
                            File.SetAttributes(folderPath, FileAttributes.Normal);
                            Directory.Delete(folderPath, true);
                            break;
                        } catch {
                            System.Threading.Thread.Sleep(25);
                        }
                    }

                    ShellHelper.NotifyItemDeleted(folderPath, isDirectory: true);
                } else {
                    // Regular folder with user files that had orphaned Panak icon files
                    RevertToRegularFolder(folderPath);
                }
            } catch { }
        }

        private void ScheduleGhostFolderCleanup(string folderPath) {
            if (string.IsNullOrEmpty(folderPath)) {
                return;
            }
            Task.Delay(250).ContinueWith(_ => CleanupOrphanedPanakFolder(folderPath));
            Task.Delay(650).ContinueWith(_ => CleanupOrphanedPanakFolder(folderPath));
        }

        private readonly List<FileSystemWatcher> _desktopWatchers = new();

        private void WatchDesktopDirectory(string dir) {
            try {
                var watcher = new FileSystemWatcher(dir) {
                    NotifyFilter = NotifyFilters.DirectoryName,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                void OnDesktopSubdirEvent(object sender, FileSystemEventArgs e) {
                    void CheckAndRegister() {
                        if (Directory.Exists(e.FullPath) && CheckIfPanakFolder(e.FullPath)) {
                            lock (_knownFolders) {
                                _knownFolders.Add(e.FullPath);
                            }
                            EnsurePanakFolderIntegrity(e.FullPath);
                            WatchFolder(e.FullPath);
                        }
                    }
                    CheckAndRegister();
                    Task.Delay(350).ContinueWith(_ => CheckAndRegister());
                }

                watcher.Created += OnDesktopSubdirEvent;
                watcher.Deleted += (s, e) => {
                    UnwatchFolder(e.FullPath);
                };
                watcher.Renamed += (s, e) => {
                    RenameFolder(e.OldFullPath, e.FullPath);
                    Application.Current?.Dispatcher.BeginInvoke(() => {
                        var p = _openPopups.FirstOrDefault(w => string.Equals(w.RootFolderPath, e.OldFullPath, StringComparison.OrdinalIgnoreCase));
                        p?.OnExternalFolderRenamed(e.OldFullPath, e.FullPath);
                    });
                    OnDesktopSubdirEvent(s, e);
                };

                _desktopWatchers.Add(watcher);
            } catch { }
        }

        public void EnsurePanakFolderIntegrity(string folderPath) {
            try {
                if (!Directory.Exists(folderPath)) {
                    return;
                }

                var folderAttrs = File.GetAttributes(folderPath);
                if (!folderAttrs.HasFlag(FileAttributes.ReadOnly)) {
                    File.SetAttributes(folderPath, folderAttrs | FileAttributes.ReadOnly);
                }

                string desktopIni = Path.Combine(folderPath, "desktop.ini");
                bool needsRegen = false;

                if (!File.Exists(desktopIni)) {
                    needsRegen = true;
                } else {
                    var iniAttrs = File.GetAttributes(desktopIni);
                    if (!iniAttrs.HasFlag(FileAttributes.System) || !iniAttrs.HasFlag(FileAttributes.Hidden)) {
                        File.SetAttributes(desktopIni, iniAttrs | FileAttributes.System | FileAttributes.Hidden);
                    }
                    string[] icos = Directory.GetFiles(folderPath, "panak_icon*.ico");
                    if (icos.Length == 0) {
                        needsRegen = true;
                    } else {
                        foreach (var ico in icos) {
                            var icoAttrs = File.GetAttributes(ico);
                            if (!icoAttrs.HasFlag(FileAttributes.System) || !icoAttrs.HasFlag(FileAttributes.Hidden)) {
                                File.SetAttributes(ico, icoAttrs | FileAttributes.System | FileAttributes.Hidden);
                            }
                        }
                    }
                }

                string metaFile = Path.Combine(folderPath, "panak.json");
                if (!File.Exists(metaFile)) {
                    var folderData = PanakFolderData.Load(folderPath);
                    folderData.AutoPopup = true;
                    folderData.Save(folderPath);
                }

                if (needsRegen) {
                    var folderData = PanakFolderData.Load(folderPath);
                    IconGenerator.GenerateFolderIcon(folderPath, folderData.IconShape, folderData.Color, folderData.BackgroundOpacity);
                }
            } catch {
                // Ignore integrity check errors
            }
        }

        public bool IsPanakFolder(string? path) {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) {
                return false;
            }

            return CheckIfPanakFolder(path);
        }

        public static bool CheckIfPanakFolder(string path) {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) {
                return false;
            }

            if (path.EndsWith(".panak", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            string metaFile = Path.Combine(path, "panak.json");
            if (File.Exists(metaFile)) {
                try {
                    var data = PanakFolderData.Load(path);
                    return data.AutoPopup;
                } catch {
                    return true;
                }
            }

            return false;
        }

        public string? FindKnownFolderByName(string? folderName) {
            if (string.IsNullOrEmpty(folderName)) {
                return null;
            }

            foreach (var folder in _knownFolders) {
                if (Path.GetFileName(folder).Equals(folderName, StringComparison.OrdinalIgnoreCase)) {
                    return folder;
                }
            }

            return null;
        }

        public static List<FileSystemInfo> GetUserItems(string folderPath) {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) {
                return new List<FileSystemInfo>();
            }
            try {
                var directoryInfo = new DirectoryInfo(folderPath);
                return directoryInfo.GetFileSystemInfos()
                    .Where(f => !f.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) &&
                                !f.Name.StartsWith("panak_icon", StringComparison.OrdinalIgnoreCase) &&
                                !f.Name.Equals("panak.json", StringComparison.OrdinalIgnoreCase) &&
                                !f.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) &&
                                !f.Attributes.HasFlag(FileAttributes.Hidden))
                    .OrderBy(f => !f.Attributes.HasFlag(FileAttributes.Directory))
                    .ThenBy(f => f.Name)
                    .ToList();
            } catch {
                return new List<FileSystemInfo>();
            }
        }

        public bool CheckAndDissolveFolder(string folderPath) {
            try {
                if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) {
                    return false;
                }

                if (!CheckIfPanakFolder(folderPath)) {
                    return false;
                }

                // Never dissolve while popup is open for this folder
                if (_openPopups.Any(p => string.Equals(p.RootFolderPath, folderPath, StringComparison.OrdinalIgnoreCase))) {
                    return false;
                }

                var userItems = GetUserItems(folderPath);
                if (userItems.Count > 1) {
                    return false;
                }

                // If 1 item remains inside, move it out to parent directory before deleting folder
                string? parentDir = Path.GetDirectoryName(folderPath);
                if (string.IsNullOrEmpty(parentDir) || !Directory.Exists(parentDir)) {
                    parentDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }

                if (userItems.Count == 1) {
                    var remaining = userItems[0];
                    string targetName = remaining.Name;
                    string targetPath = Path.Combine(parentDir, targetName);

                    int counter = 1;
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(targetName);
                    string ext = Path.GetExtension(targetName);
                    bool isDir = remaining.Attributes.HasFlag(FileAttributes.Directory);

                    while (File.Exists(targetPath) || Directory.Exists(targetPath)) {
                        string uniqueName = isDir ? $"{targetName} ({counter++})" : $"{nameWithoutExt} ({counter++}){ext}";
                        targetPath = Path.Combine(parentDir, uniqueName);
                    }

                    try {
                        if (isDir) {
                            Directory.Move(remaining.FullName, targetPath);
                        } else {
                            File.Move(remaining.FullName, targetPath);
                        }

                        try {
                            var attrs = File.GetAttributes(targetPath);
                            if (attrs.HasFlag(FileAttributes.ReadOnly)) {
                                File.SetAttributes(targetPath, attrs & ~FileAttributes.ReadOnly);
                            }
                        } catch { }

                        ShellHelper.NotifyShellItemChanged(targetPath);
                        ShellHelper.NotifyFolderCreated(targetPath);
                    } catch {
                        // Abort deletion to avoid user data loss if move fails
                        return false;
                    }
                }

                // Close popup window if currently open for this folder
                Application.Current?.Dispatcher.Invoke(() => {
                    var openForFolder = _openPopups.FirstOrDefault(p => string.Equals(p.RootFolderPath, folderPath, StringComparison.OrdinalIgnoreCase));
                    openForFolder?.CloseAnimated();
                });

                // Unregister and stop watching
                _knownFolders.Remove(folderPath);
                lock (_watchers) {
                    if (_watchers.TryGetValue(folderPath, out var w)) {
                        try {
                            w.EnableRaisingEvents = false;
                            w.Dispose();
                        } catch { }
                        _watchers.Remove(folderPath);
                    }
                }

                lock (_debounceLock) {
                    if (_debounceTimers.TryGetValue(folderPath, out var timer)) {
                        try { timer.Dispose(); } catch { }
                        _debounceTimers.Remove(folderPath);
                    }
                    _lastUpdateTicks.Remove(folderPath);
                }

                lock (_creationTimes) {
                    _creationTimes.Remove(folderPath);
                }

                // Clean up system files and remove ReadOnly on folder
                try {
                    string desktopIni = Path.Combine(folderPath, "desktop.ini");
                    if (File.Exists(desktopIni)) {
                        File.SetAttributes(desktopIni, FileAttributes.Normal);
                        File.Delete(desktopIni);
                    }
                } catch { }

                try {
                    string panakJson = Path.Combine(folderPath, "panak.json");
                    if (File.Exists(panakJson)) {
                        File.SetAttributes(panakJson, FileAttributes.Normal);
                        File.Delete(panakJson);
                    }
                } catch { }

                try {
                    foreach (var ico in Directory.GetFiles(folderPath, "panak_icon*.ico")) {
                        File.SetAttributes(ico, FileAttributes.Normal);
                        File.Delete(ico);
                    }
                } catch { }

                for (int attempt = 0; attempt < 5; attempt++) {
                    try {
                        if (!Directory.Exists(folderPath)) break;
                        File.SetAttributes(folderPath, FileAttributes.Normal);
                        Directory.Delete(folderPath, true);
                        break;
                    } catch {
                        System.Threading.Thread.Sleep(25);
                    }
                }

                ShellHelper.NotifyItemDeleted(folderPath, isDirectory: true);
                if (!string.IsNullOrEmpty(parentDir)) {
                    ShellHelper.NotifyFolderCreated(parentDir);
                }

                return true;
            } catch {
                return false;
            }
        }

        public void ScheduleFolderIconUpdate(string folderPath, bool immediate = false, string? shape = null, string? color = null) {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) {
                return;
            }

            if (!CheckIfPanakFolder(folderPath)) {
                UnwatchFolder(folderPath);
                ScheduleGhostFolderCleanup(folderPath);
                return;
            }

            bool inGracePeriod = false;
            lock (_creationTimes) {
                if (_creationTimes.TryGetValue(folderPath, out long createTime)) {
                    if (Environment.TickCount64 - createTime < 15000) {
                        inGracePeriod = true;
                    } else {
                        _creationTimes.Remove(folderPath);
                    }
                }
            }

            if (!inGracePeriod && CheckAndDissolveFolder(folderPath)) {
                return;
            }

            if (immediate) {
                lock (_debounceLock) {
                    if (_debounceTimers.TryGetValue(folderPath, out var timer)) {
                        try {
                            timer.Dispose();
                        } catch { }
                        _debounceTimers.Remove(folderPath);
                    }
                    _lastUpdateTicks[folderPath] = Environment.TickCount64;
                }

                System.Threading.Tasks.Task.Run(() => {
                    try {
                        if (!Directory.Exists(folderPath) || !CheckIfPanakFolder(folderPath)) {
                            UnwatchFolder(folderPath);
                            ScheduleGhostFolderCleanup(folderPath);
                            return;
                        }
                        string iconShape = shape ?? string.Empty;
                        string iconColor = color ?? string.Empty;
                        var folderData = PanakFolderData.Load(folderPath);
                        iconShape = string.IsNullOrEmpty(iconShape) ? folderData.IconShape : iconShape;
                        iconColor = string.IsNullOrEmpty(iconColor) ? folderData.Color : iconColor;
                        IconGenerator.GenerateFolderIcon(folderPath, iconShape, iconColor, folderData.BackgroundOpacity);
                    } catch { }
                });
                return;
            }

            lock (_debounceLock) {
                if (_debounceTimers.TryGetValue(folderPath, out var existingTimer)) {
                    try {
                        existingTimer.Change(200, System.Threading.Timeout.Infinite);
                    } catch { }
                } else {
                    System.Threading.Timer? newTimer = null;
                    newTimer = new System.Threading.Timer(_ => {
                        lock (_debounceLock) {
                            if (_debounceTimers.TryGetValue(folderPath, out var t) && ReferenceEquals(t, newTimer)) {
                                _debounceTimers.Remove(folderPath);
                            }
                            newTimer?.Dispose();
                            _lastUpdateTicks[folderPath] = Environment.TickCount64;
                        }

                        if (!Directory.Exists(folderPath)) {
                            return;
                        }

                        if (!CheckIfPanakFolder(folderPath)) {
                            UnwatchFolder(folderPath);
                            ScheduleGhostFolderCleanup(folderPath);
                            return;
                        }

                        bool inTimerGrace = false;
                        lock (_creationTimes) {
                            if (_creationTimes.TryGetValue(folderPath, out long createTime)) {
                                if (Environment.TickCount64 - createTime < 15000) {
                                    inTimerGrace = true;
                                } else {
                                    _creationTimes.Remove(folderPath);
                                }
                            }
                        }

                        if (!inTimerGrace && CheckAndDissolveFolder(folderPath)) {
                            return;
                        }

                        try {
                            if (!Directory.Exists(folderPath) || !CheckIfPanakFolder(folderPath)) {
                                UnwatchFolder(folderPath);
                                ScheduleGhostFolderCleanup(folderPath);
                                return;
                            }
                            var folderData = PanakFolderData.Load(folderPath);
                            IconGenerator.GenerateFolderIcon(folderPath, folderData.IconShape, folderData.Color, folderData.BackgroundOpacity);
                        } catch { }
                    }, null, 200, System.Threading.Timeout.Infinite);

                    _debounceTimers[folderPath] = newTimer;
                }
            }
        }

        public void WatchFolder(string folderPath) {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath) || !CheckIfPanakFolder(folderPath)) {
                return;
            }

            lock (_watchers) {
                if (_watchers.ContainsKey(folderPath)) {
                    return;
                }

                try {
                    var watcher = new FileSystemWatcher(folderPath) {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                        IncludeSubdirectories = false,
                        EnableRaisingEvents = true
                    };

                    void OnFileSystemEvent(object sender, FileSystemEventArgs e) {
                        if (string.IsNullOrEmpty(e.Name)) {
                            return;
                        }

                        string fileName = Path.GetFileName(e.Name);
                        if (fileName.Equals("panak.json", StringComparison.OrdinalIgnoreCase)) {
                            if (e.ChangeType == WatcherChangeTypes.Deleted || !File.Exists(Path.Combine(folderPath, "panak.json"))) {
                                if (!CheckIfPanakFolder(folderPath)) {
                                    UnwatchFolder(folderPath);
                                    ScheduleGhostFolderCleanup(folderPath);
                                }
                            }
                            return;
                        }

                        if (fileName.StartsWith("panak_icon", StringComparison.OrdinalIgnoreCase) ||
                            fileName.StartsWith("desktop.ini", StringComparison.OrdinalIgnoreCase) ||
                            fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) {
                            return;
                        }

                        if (!CheckIfPanakFolder(folderPath)) {
                            UnwatchFolder(folderPath);
                            ScheduleGhostFolderCleanup(folderPath);
                            return;
                        }

                        ScheduleFolderIconUpdate(folderPath, immediate: false);

                        Application.Current?.Dispatcher.BeginInvoke(() => {
                            foreach (var p in _openPopups.Where(w => string.Equals(w.RootFolderPath, folderPath, StringComparison.OrdinalIgnoreCase))) {
                                p.OnExternalFolderContentsChanged(folderPath);
                            }
                        });
                    }

                    watcher.Created += OnFileSystemEvent;
                    watcher.Deleted += OnFileSystemEvent;
                    watcher.Changed += OnFileSystemEvent;
                    watcher.Renamed += (s, e) => OnFileSystemEvent(s, e);

                    _watchers[folderPath] = watcher;
                    lock (_knownFolders) {
                        _knownFolders.Add(folderPath);
                    }
                } catch {
                    // Fallback
                }
            }
        }

        public void OpenFolder(string folderPath, Rect? iconBounds = null) {
            Application.Current.Dispatcher.Invoke(() => {
                if (!Directory.Exists(folderPath)) {
                    return;
                }

                // If already open in one of our windows, re-activate and bring to front
                var existing = _openPopups.FirstOrDefault(w => string.Equals(w.RootFolderPath, folderPath, StringComparison.OrdinalIgnoreCase));
                if (existing != null && existing.IsVisible) {
                    existing.Activate();
                    existing.Focus();
                    IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(existing).Handle;
                    ShellHelper.ForceForegroundWindow(hwnd);
                    return;
                }

                // Close any UNPINNED open popups, while preserving ALL PINNED POPUPS!
                var unpinned = _openPopups.Where(w => !w.IsPinned).ToList();
                foreach (var w in unpinned) {
                    w.CloseAnimated();
                }

                SetupParentWatcher(folderPath);
                if (CheckIfPanakFolder(folderPath)) {
                    _knownFolders.Add(folderPath);
                    WatchFolder(folderPath);
                }

                Rect bounds = iconBounds ?? Rect.Empty;
                if (bounds.IsEmpty) {
                    bounds = ExplorerHelper.GetBoundingBoxForPath(folderPath);
                }

                // Create new popup window
                var popupWindow = new FolderPopupWindow();
                _openPopups.Add(popupWindow);

                popupWindow.ClosedAnimated += () => {
                    _openPopups.Remove(popupWindow);
                    RemoveParentWatcher(folderPath);
                    if (!_openPopups.Any(w => !w.IsPinned)) {
                        HideFocusBackdrops();
                    }
                };

                popupWindow.OpeningAnimationStarting += () => {
                    ShowFocusBackdrops();
                };

                popupWindow.LoadFolder(folderPath, bounds);

                // Ensure any concurrently opened Explorer window for this folder is closed
                CloseExplorerWindowForPath(folderPath);
            });
        }

        private void OnPopupOpeningAnimationStarting() {
            ShowFocusBackdrops();
        }

        public void ShowFocusBackdrops() {
            // Disabled in Panak Lite
            HideFocusBackdrops();
            return;
        }

        public void HideFocusBackdrops() {
            foreach (var w in _backdropWindows) {
                w.HideBackdrop();
            }
        }

        public void EnsurePopupAboveBackdrop() {
            try {
                var backdropHwnds = _backdropWindows
                    .Where(w => w.IsVisible)
                    .Select(w => new System.Windows.Interop.WindowInteropHelper(w).Handle)
                    .Where(h => h != IntPtr.Zero)
                    .ToList();

                foreach (var popup in _openPopups.ToList()) {
                    if (popup.IsVisible) {
                        popup.Topmost = true;
                        IntPtr popupHwnd = new System.Windows.Interop.WindowInteropHelper(popup).Handle;
                        if (popupHwnd != IntPtr.Zero) {
                            // Force popup to top of all topmost windows
                            ShellHelper.SetWindowPos(popupHwnd, ShellHelper.HWND_TOPMOST, 0, 0, 0, 0,
                                ShellHelper.SWP_NOMOVE | ShellHelper.SWP_NOSIZE | ShellHelper.SWP_SHOWWINDOW);

                            // If backdrops are visible, explicitly position them immediately beneath the popup window
                            foreach (var bHwnd in backdropHwnds) {
                                ShellHelper.SetWindowPos(bHwnd, popupHwnd, 0, 0, 0, 0,
                                    ShellHelper.SWP_NOMOVE | ShellHelper.SWP_NOSIZE | ShellHelper.SWP_NOACTIVATE);
                            }
                        }
                    }
                }

                var active = _openPopups.LastOrDefault(w => w.IsVisible);
                if (active != null) {
                    active.Activate();
                    active.Focus();
                    IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(active).Handle;
                    if (hwnd != IntPtr.Zero) {
                        ShellHelper.ForceForegroundWindow(hwnd);
                    }
                }
            } catch { }
        }

        public void TemporarilyHideBackdropForDrag() {
            Application.Current?.Dispatcher.Invoke(() => {
                foreach (var w in _backdropWindows) {
                    if (w.IsVisible) {
                        w.Visibility = Visibility.Hidden;
                    }
                }
            });
        }

        public void RestoreBackdropAfterDrag() {
            Application.Current?.Dispatcher.Invoke(() => {
                if (_openPopups.Any(w => w.IsVisible && !w.IsPinned) &&
                    SettingsService.Instance.Current.FocusModeEnabled) {
                    foreach (var w in _backdropWindows) {
                        w.Visibility = Visibility.Visible;
                    }
                    EnsurePopupAboveBackdrop();
                    Application.Current?.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => {
                        EnsurePopupAboveBackdrop();
                    });
                }
            });
        }

        private static Type? _cachedShellAppType;

        private static void CloseExplorerWindowForPath(string folderPath) {
            // Never close Explorer for regular Windows folders (e.g. during Quick Look in Explorer)
            if (!CheckIfPanakFolder(folderPath)) {
                return;
            }

            Task.Run(async () => {
                string normalizedTarget = folderPath.TrimEnd('\\');
                int[] delays = new[] { 120, 350 };
                bool closedAny = false;
                for (int attempt = 0; attempt < delays.Length; attempt++) {
                    await Task.Delay(delays[attempt]);
                    try {
                        Type? shellType = _cachedShellAppType ??= Type.GetTypeFromProgID("Shell.Application");
                        if (shellType == null) {
                            return;
                        }
                        dynamic? shell = Activator.CreateInstance(shellType);
                        if (shell == null) {
                            return;
                        }
                        dynamic windows = shell.Windows();
                        int count = windows.Count;
                        for (int i = 0; i < count; i++) {
                            try {
                                dynamic item = windows.Item(i);
                                if (item == null) {
                                    continue;
                                }
                                string loc = item.LocationURL;
                                if (!string.IsNullOrEmpty(loc)) {
                                    Uri uri = new Uri(loc);
                                    if (uri.LocalPath.TrimEnd('\\').Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase)) {
                                        item.Quit();
                                        closedAny = true;
                                        continue;
                                    }
                                }
                                try {
                                    string? docPath = (string?)item.Document?.Folder?.Self?.Path;
                                    if (!string.IsNullOrEmpty(docPath) &&
                                        docPath.TrimEnd('\\').Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase)) {
                                        item.Quit();
                                        closedAny = true;
                                    }
                                } catch { }
                            } catch {
                                // Ignore window iteration errors
                            }
                        }
                        if (closedAny) {
                            break;
                        }
                    } catch {
                        // Ignore shell COM errors
                    }
                }

                // Ensure popup re-asserts foreground window only if a background explorer window was actually closed
                if (closedAny) {
                    Application.Current?.Dispatcher.BeginInvoke(() => {
                        var active = PanakManager.Instance._openPopups.LastOrDefault(w => w.IsVisible);
                        if (active != null) {
                            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(active).Handle;
                            if (hwnd != IntPtr.Zero) {
                                ShellHelper.ForceForegroundWindow(hwnd);
                            }
                        }
                    });
                }
            });
        }

        public void ClosePopup(bool force = false) {
            Application.Current.Dispatcher.Invoke(() => {
                var targets = force
                    ? _openPopups.ToList()
                    : _openPopups.Where(w => !w.IsPinned).ToList();

                foreach (var p in targets) {
                    p.CloseAnimated();
                }

                if (force || !_openPopups.Any(w => !w.IsPinned)) {
                    HideFocusBackdrops();
                }
            });
        }

        private void OnPopupClosed() {
            if (!_openPopups.Any(w => !w.IsPinned)) {
                HideFocusBackdrops();
            }
        }

        private void SetupParentWatcher(string folderPath) {
            try {
                string? parent = Path.GetDirectoryName(folderPath);
                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent)) {
                    lock (_parentWatchers) {
                        if (_parentWatchers.ContainsKey(folderPath)) {
                            return;
                        }
                        var watcher = new FileSystemWatcher(parent) {
                            NotifyFilter = NotifyFilters.DirectoryName,
                            IncludeSubdirectories = false,
                            EnableRaisingEvents = true
                        };

                        watcher.Renamed += (s, e) => {
                            if (string.Equals(e.OldFullPath, folderPath, StringComparison.OrdinalIgnoreCase)) {
                                RenameFolder(e.OldFullPath, e.FullPath);
                                Application.Current?.Dispatcher.BeginInvoke(() => {
                                    var p = _openPopups.FirstOrDefault(w => string.Equals(w.RootFolderPath, e.OldFullPath, StringComparison.OrdinalIgnoreCase));
                                    p?.OnExternalFolderRenamed(e.OldFullPath, e.FullPath);
                                });
                            }
                        };
                        _parentWatchers[folderPath] = watcher;
                    }
                }
            } catch { }
        }

        private void RemoveParentWatcher(string folderPath) {
            lock (_parentWatchers) {
                if (_parentWatchers.TryGetValue(folderPath, out var w)) {
                    try {
                        w.EnableRaisingEvents = false;
                        w.Dispose();
                    } catch { }
                    _parentWatchers.Remove(folderPath);
                }
            }
        }

        public void RenameFolder(string oldPath, string newPath) {
            bool wasWatched = false;
            lock (_watchers) {
                if (_watchers.TryGetValue(oldPath, out var watcher)) {
                    wasWatched = true;
                    try {
                        watcher.EnableRaisingEvents = false;
                        watcher.Dispose();
                    } catch { }
                    _watchers.Remove(oldPath);
                }
            }

            bool wasKnown;
            lock (_knownFolders) {
                wasKnown = _knownFolders.Remove(oldPath);
            }

            lock (_debounceLock) {
                if (_debounceTimers.TryGetValue(oldPath, out var timer)) {
                    try { timer.Dispose(); } catch { }
                    _debounceTimers.Remove(oldPath);
                }
                _lastUpdateTicks.Remove(oldPath);
            }

            bool isPanak = CheckIfPanakFolder(newPath);

            lock (_creationTimes) {
                if (_creationTimes.Remove(oldPath, out var ct) && (wasKnown || wasWatched || isPanak)) {
                    _creationTimes[newPath] = ct;
                }
            }

            lock (_parentWatchers) {
                if (_parentWatchers.Remove(oldPath, out var pw)) {
                    _parentWatchers[newPath] = pw;
                }
            }

            if (isPanak) {
                lock (_knownFolders) {
                    _knownFolders.Add(newPath);
                }
                WatchFolder(newPath);
            }
        }

        public void UnwatchFolder(string folderPath) {
            lock (_knownFolders) {
                _knownFolders.Remove(folderPath);
            }

            lock (_watchers) {
                if (_watchers.TryGetValue(folderPath, out var watcher)) {
                    try {
                        watcher.EnableRaisingEvents = false;
                        watcher.Dispose();
                    } catch { }
                    _watchers.Remove(folderPath);
                }
            }

            lock (_debounceLock) {
                if (_debounceTimers.TryGetValue(folderPath, out var timer)) {
                    try { timer.Dispose(); } catch { }
                    _debounceTimers.Remove(folderPath);
                }
                _lastUpdateTicks.Remove(folderPath);
            }

            lock (_creationTimes) {
                _creationTimes.Remove(folderPath);
            }
        }

        private void OnBackdropClicked() {
            if (IsPopupWithinGracePeriod || IsPopupPinned) {
                return;
            }
            ClosePopup();
        }

        public void ConvertToPanakFolder(string folderPath) {
            try {
                if (!Directory.Exists(folderPath)) {
                    return;
                }
                var data = PanakFolderData.Load(folderPath);
                data.AutoPopup = true;
                data.Save(folderPath);

                lock (_knownFolders) {
                    _knownFolders.Add(folderPath);
                }
                WatchFolder(folderPath);
                IconGenerator.GenerateFolderIcon(folderPath, data.IconShape, data.Color, data.BackgroundOpacity);
            } catch {
                // Ignore conversion errors
            }
        }

        public void RevertToRegularFolder(string folderPath) {
            try {
                UnwatchFolder(folderPath);
                if (!Directory.Exists(folderPath)) {
                    return;
                }

                string metaFile = Path.Combine(folderPath, "panak.json");
                if (File.Exists(metaFile)) {
                    try {
                        File.Delete(metaFile);
                    } catch { }
                }

                string iniPath = Path.Combine(folderPath, "desktop.ini");
                if (File.Exists(iniPath)) {
                    try {
                        File.SetAttributes(iniPath, FileAttributes.Normal);
                        File.Delete(iniPath);
                    } catch { }
                }

                foreach (var ico in Directory.GetFiles(folderPath, "panak_icon*.ico")) {
                    try {
                        File.SetAttributes(ico, FileAttributes.Normal);
                        File.Delete(ico);
                    } catch { }
                }

                // Remove ReadOnly attribute from folder when reverting to regular Windows folder
                try {
                    var folderAttrs = File.GetAttributes(folderPath);
                    if (folderAttrs.HasFlag(FileAttributes.ReadOnly)) {
                        File.SetAttributes(folderPath, folderAttrs & ~FileAttributes.ReadOnly);
                    }
                } catch { }

                ShellHelper.NotifyShellItemChanged(folderPath);
            } catch {
                // Ignore cleanup errors
            }
        }

        public string CreatePanakFolder(string parentDir, string[]? filesToInclude = null, string? preferredName = null, Point? targetPosition = null) {
            try {
                if (string.IsNullOrEmpty(parentDir) || !Directory.Exists(parentDir)) {
                    parentDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }

                string baseName = !string.IsNullOrEmpty(preferredName) ? preferredName : LocalizationService.Instance["NewFolder"];
                string targetPath = Path.Combine(parentDir, baseName);

                int counter = 1;
                while (Directory.Exists(targetPath) || File.Exists(targetPath)) {
                    targetPath = Path.Combine(parentDir, $"{baseName} ({counter++})");
                }

                Directory.CreateDirectory(targetPath);
                try {
                    var folderAttrs = File.GetAttributes(targetPath);
                    if (!folderAttrs.HasFlag(FileAttributes.ReadOnly)) {
                        File.SetAttributes(targetPath, folderAttrs | FileAttributes.ReadOnly);
                    }
                } catch { }

                var settings = SettingsService.Instance.Current;

                string initialShape = settings.IconShape;
                if (settings.RandomShapeOnCreate) {
                    string[] shapes = { "Squircle", "Circle", "Clover", "Scallop", "Arch" };
                    initialShape = shapes[Random.Shared.Next(shapes.Length)];
                }

                string initialColor = settings.ThemeColor;
                if (settings.RandomColorOnCreate) {
                    int randomHue = Random.Shared.Next(0, 360);
                    var (_, _, c) = ShellHelper.GetMaterialYouTonalPalette(randomHue);
                    initialColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                }

                double initialOpacity = settings.FolderOpacity;
                if (settings.RandomOpacityOnCreate) {
                    initialOpacity = Math.Round(0.40 + Random.Shared.NextDouble() * 0.55, 2);
                }

                // Immediately create panak.json metadata
                var data = new PanakFolderData {
                    Title = Path.GetFileName(targetPath),
                    Color = initialColor,
                    IconShape = initialShape,
                    BackgroundOpacity = initialOpacity,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                string metaFile = Path.Combine(targetPath, "panak.json");
                File.WriteAllText(metaFile, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
                File.SetAttributes(metaFile, FileAttributes.Hidden);

                lock (_creationTimes) {
                    _creationTimes[targetPath] = Environment.TickCount64;
                }

                // Copy or move initial files and directories before starting watcher
                if (filesToInclude != null && filesToInclude.Length > 0) {
                    foreach (var file in filesToInclude) {
                        try {
                            if (File.Exists(file)) {
                                string dest = Path.Combine(targetPath, Path.GetFileName(file));
                                int c = 1;
                                while (File.Exists(dest) || Directory.Exists(dest)) {
                                    string fn = Path.GetFileNameWithoutExtension(file);
                                    string ext = Path.GetExtension(file);
                                    dest = Path.Combine(targetPath, $"{fn} ({c++}){ext}");
                                }
                                File.Move(file, dest);
                            } else if (Directory.Exists(file)) {
                                string dest = Path.Combine(targetPath, Path.GetFileName(file));
                                int c = 1;
                                while (File.Exists(dest) || Directory.Exists(dest)) {
                                    dest = Path.Combine(targetPath, $"{Path.GetFileName(file)} ({c++})");
                                }
                                Directory.Move(file, dest);
                            }
                        } catch {
                            // Ignore single file or directory move failure
                        }
                    }
                }

                _knownFolders.Add(targetPath);
                WatchFolder(targetPath);

                // Generate initial Android-style 2x2 preview icon
                IconGenerator.GenerateFolderIcon(targetPath, data.IconShape, data.Color, data.BackgroundOpacity);

                // Refresh Explorer and Desktop
                ShellHelper.NotifyFolderCreated(targetPath);

                // Position desktop icon at targetPosition if on desktop
                if (targetPosition != null) {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                    if (parentDir.TrimEnd('\\').Equals(desktop.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) ||
                        parentDir.TrimEnd('\\').Equals(commonDesktop.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) {
                        ExplorerHelper.SetDesktopItemPosition(Path.GetFileName(targetPath), (int)targetPosition.Value.X, (int)targetPosition.Value.Y);
                    }
                }

                return targetPath;
            } catch {
                return string.Empty;
            }
        }

        public string CollectFilesIntoPanakFolder(string[] files, string? targetDir = null, Rect? bounds = null) {
            try {
                if (files == null || files.Length == 0) {
                    return string.Empty;
                }

                var validFiles = files.Where(f => !string.IsNullOrEmpty(f) && (File.Exists(f) || Directory.Exists(f))).ToArray();
                if (validFiles.Length == 0) {
                    return string.Empty;
                }

                if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir)) {
                    string first = validFiles.FirstOrDefault(f => !string.IsNullOrEmpty(f)) ?? string.Empty;
                    if (!string.IsNullOrEmpty(first)) {
                        targetDir = Path.GetDirectoryName(first);
                    }
                    if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir)) {
                        targetDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    }
                }

                Point? targetPos = null;
                if (bounds != null && !bounds.Value.IsEmpty) {
                    targetPos = new Point(bounds.Value.X + bounds.Value.Width / 2, bounds.Value.Y + bounds.Value.Height / 2);
                }

                string baseName = Path.GetFileNameWithoutExtension(validFiles[0]);
                if (string.IsNullOrEmpty(baseName)) {
                    baseName = LocalizationService.Instance["NewFolder"];
                }
                string newFolder = CreatePanakFolder(targetDir, validFiles, baseName, targetPos);
                if (!string.IsNullOrEmpty(newFolder)) {
                    Rect openBounds = (bounds != null && !bounds.Value.IsEmpty)
                        ? bounds.Value
                        : ExplorerHelper.GetBoundingBoxForPath(newFolder);
                    OpenFolder(newFolder, openBounds);
                }
                return newFolder;
            } catch {
                return string.Empty;
            }
        }

        public bool UnpackPanakFolder(string folderPath) {
            try {
                if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) {
                    return false;
                }

                string? parentDir = Path.GetDirectoryName(folderPath);
                if (string.IsNullOrEmpty(parentDir) || !Directory.Exists(parentDir)) {
                    parentDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }

                // 1. Close any open popup windows for this folder
                Application.Current?.Dispatcher.Invoke(() => {
                    var openForThis = _openPopups.Where(w => string.Equals(w.RootFolderPath, folderPath, StringComparison.OrdinalIgnoreCase)).ToList();
                    foreach (var w in openForThis) {
                        w.CloseAnimated();
                        _openPopups.Remove(w);
                    }
                    RemoveParentWatcher(folderPath);
                    if (!_openPopups.Any(w => !w.IsPinned)) {
                        HideFocusBackdrops();
                    }
                });

                // 2. Unregister and stop watching this folder
                _knownFolders.Remove(folderPath);
                lock (_watchers) {
                    if (_watchers.TryGetValue(folderPath, out var w)) {
                        try {
                            w.EnableRaisingEvents = false;
                            w.Dispose();
                        } catch { }
                        _watchers.Remove(folderPath);
                    }
                }

                lock (_debounceLock) {
                    if (_debounceTimers.TryGetValue(folderPath, out var timer)) {
                        try { timer.Dispose(); } catch { }
                        _debounceTimers.Remove(folderPath);
                    }
                    _lastUpdateTicks.Remove(folderPath);
                }

                lock (_creationTimes) {
                    _creationTimes.Remove(folderPath);
                }

                // 3. Move all user items to parent directory
                var dirInfo = new DirectoryInfo(folderPath);
                var userItems = dirInfo.GetFileSystemInfos()
                    .Where(f => !f.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) &&
                                !f.Name.StartsWith("panak_icon", StringComparison.OrdinalIgnoreCase) &&
                                !f.Name.Equals("panak.json", StringComparison.OrdinalIgnoreCase) &&
                                !f.Name.Equals("panak_lock.json", StringComparison.OrdinalIgnoreCase) &&
                                !f.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => !f.Attributes.HasFlag(FileAttributes.Directory))
                    .ThenBy(f => f.Name)
                    .ToList();
                foreach (var item in userItems) {
                    try {
                        string targetName = item.Name;
                        string targetPath = Path.Combine(parentDir, targetName);
                        int counter = 1;
                        bool isDir = item.Attributes.HasFlag(FileAttributes.Directory);
                        string nameWithoutExt = Path.GetFileNameWithoutExtension(targetName);
                        string ext = Path.GetExtension(targetName);

                        while (File.Exists(targetPath) || Directory.Exists(targetPath)) {
                            string uniqueName = isDir ? $"{targetName} ({counter++})" : $"{nameWithoutExt} ({counter++}){ext}";
                            targetPath = Path.Combine(parentDir, uniqueName);
                        }

                        if (isDir) {
                            Directory.Move(item.FullName, targetPath);
                        } else {
                            File.Move(item.FullName, targetPath);
                        }

                        try {
                            var attrs = File.GetAttributes(targetPath);
                            if (attrs.HasFlag(FileAttributes.ReadOnly)) {
                                File.SetAttributes(targetPath, attrs & ~FileAttributes.ReadOnly);
                            }
                        } catch { }

                        ShellHelper.NotifyShellItemChanged(targetPath);
                        ShellHelper.NotifyFolderCreated(targetPath);
                    } catch {
                        // Continue moving other items if one fails
                    }
                }

                // 4. Remove metadata files and folder
                try {
                    string desktopIni = Path.Combine(folderPath, "desktop.ini");
                    if (File.Exists(desktopIni)) {
                        File.SetAttributes(desktopIni, FileAttributes.Normal);
                        File.Delete(desktopIni);
                    }
                } catch { }

                try {
                    string panakJson = Path.Combine(folderPath, "panak.json");
                    if (File.Exists(panakJson)) {
                        File.SetAttributes(panakJson, FileAttributes.Normal);
                        File.Delete(panakJson);
                    }
                } catch { }

                try {
                    foreach (var ico in Directory.GetFiles(folderPath, "panak_icon*.ico")) {
                        File.SetAttributes(ico, FileAttributes.Normal);
                        File.Delete(ico);
                    }
                } catch { }

                for (int attempt = 0; attempt < 5; attempt++) {
                    try {
                        if (!Directory.Exists(folderPath)) break;
                        File.SetAttributes(folderPath, FileAttributes.Normal);
                        Directory.Delete(folderPath, true);
                        break;
                    } catch {
                        System.Threading.Thread.Sleep(25);
                    }
                }

                ShellHelper.NotifyItemDeleted(folderPath, isDirectory: true);
                if (!string.IsNullOrEmpty(parentDir)) {
                    ShellHelper.NotifyFolderCreated(parentDir);
                }

                return true;
            } catch {
                return false;
            }
        }
    }
}

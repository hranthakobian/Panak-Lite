#pragma warning disable CS8602
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;

namespace Panak.Services {
    public static class ExplorerHelper {
        public struct ItemInfo {
            public string? Path;
            public Rect BoundingBox;
            public bool IsFolder;
        }

        private static Type? _shellAppType;
        private static Type? GetShellAppType() {
            return _shellAppType ??= Type.GetTypeFromProgID("Shell.Application");
        }

        public static void WarmUp() {
            System.Threading.Tasks.Task.Run(() => {
                try {
                    GetDesktopSelectedPathFastCore();
                } catch { }
            });
        }

        private static T? RunComSafe<T>(Func<T?> func, int timeoutMs = 150) {
            if (System.Threading.Thread.CurrentThread.GetApartmentState() == System.Threading.ApartmentState.STA) {
                try {
                    var task = System.Threading.Tasks.Task.Run(func);
                    if (task.Wait(timeoutMs)) {
                        return task.Result;
                    }
                    return default;
                } catch {
                    return default;
                }
            }
            try {
                return func();
            } catch {
                return default;
            }
        }

        private static string[]? _cachedDesktopDirs;
        private static string[] GetDesktopDirs() {
            return _cachedDesktopDirs ??= new[] {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Desktop")
            };
        }

        public static bool IsExplorerClassName(string? cls) {
            if (string.IsNullOrEmpty(cls)) return false;
            return cls.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
                   cls.Equals("ExploreWClass", StringComparison.OrdinalIgnoreCase) ||
                   cls.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
                   cls.Equals("WorkerW", StringComparison.OrdinalIgnoreCase) ||
                   cls.Equals("SysListView32", StringComparison.OrdinalIgnoreCase) ||
                   cls.Equals("SHELLDLL_DefView", StringComparison.OrdinalIgnoreCase) ||
                   cls.Equals("DirectUIHWND", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsExplorerOrDesktopWindow(IntPtr hwnd) {
            if (hwnd == IntPtr.Zero) return false;
            StringBuilder sb = new StringBuilder(64);
            IntPtr root = ShellHelper.GetAncestor(hwnd, ShellHelper.GA_ROOT);
            if (root != IntPtr.Zero) {
                ShellHelper.GetClassName(root, sb, sb.Capacity);
                string rootCls = sb.ToString();
                if (rootCls.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
                    rootCls.Equals("ExploreWClass", StringComparison.OrdinalIgnoreCase) ||
                    rootCls.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
                    rootCls.Equals("WorkerW", StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            sb.Clear();
            ShellHelper.GetClassName(hwnd, sb, sb.Capacity);
            return IsExplorerClassName(sb.ToString());
        }

        public static bool IsForegroundOrCursorExplorerOrDesktop() {
            IntPtr fg = ShellHelper.GetForegroundWindow();
            if (IsExplorerOrDesktopWindow(fg)) return true;
            if (ShellHelper.GetCursorPos(out var pt)) {
                IntPtr underPt = ShellHelper.WindowFromPoint(pt);
                if (IsExplorerOrDesktopWindow(underPt)) return true;
            }
            return false;
        }

        public static ItemInfo? GetItemUnderCursor(int timeoutMs = 150) {
            return RunComSafe<ItemInfo?>(GetItemUnderCursorCore, timeoutMs);
        }

        private static ItemInfo? GetItemUnderCursorCore() {
            try {
                if (!ShellHelper.GetCursorPos(out var pt)) {
                    return null;
                }

                IntPtr hwndUnderCursor = ShellHelper.WindowFromPoint(pt);
                if (!IsExplorerOrDesktopWindow(hwndUnderCursor)) {
                    return null;
                }

                Point winPoint = new Point(pt.X, pt.Y);
                AutomationElement? element = AutomationElement.FromPoint(winPoint);
                if (element == null) {
                    return null;
                }

                // Look for ListItem, TreeViewItem, or DataItem (WinUI File Explorer)
                AutomationElement? current = element;
                while (current != null && current != AutomationElement.RootElement) {
                    var controlType = current.Current.ControlType;
                    if (controlType == ControlType.ListItem || controlType == ControlType.TreeItem || controlType == ControlType.DataItem) {
                        break;
                    }
                    current = TreeWalker.ControlViewWalker.GetParent(current);
                }

                AutomationElement targetElement = current ?? element;
                Rect bounds = targetElement.Current.BoundingRectangle;

                string itemName = targetElement.Current.Name;
                if (string.IsNullOrWhiteSpace(itemName) && element != null) {
                    itemName = element.Current.Name;
                }
                if (string.IsNullOrWhiteSpace(itemName)) {
                    return null;
                }

                // Query Shell.Application for the file path
                string? path = GetPathFromShellWindows(pt, itemName);

                if (string.IsNullOrEmpty(path)) {
                    // Check desktop directories
                    string[] desktopDirs = GetDesktopDirs();

                    foreach (var d in desktopDirs) {
                        if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) {
                            string candidate = Path.Combine(d, itemName);
                            if (File.Exists(candidate) || Directory.Exists(candidate)) {
                                path = candidate;
                                break;
                            }
                            try {
                                var dirMatches = Directory.GetDirectories(d, itemName + ".*");
                                if (dirMatches.Length > 0) {
                                    path = dirMatches[0];
                                    break;
                                }
                                var matches = Directory.GetFiles(d, itemName + ".*");
                                if (matches.Length > 0) {
                                    path = matches[0];
                                    break;
                                }
                            } catch { }
                        }
                    }

                    // Also check known Panak folders
                    if (string.IsNullOrEmpty(path)) {
                        string? match = PanakManager.Instance.FindKnownFolderByName(itemName);
                        if (!string.IsNullOrEmpty(match)) {
                            path = match;
                        }
                    }
                }

                bool isFolder = false;
                if (!string.IsNullOrEmpty(path)) {
                    isFolder = Directory.Exists(path);
                }

                return new ItemInfo {
                    Path = path,
                    BoundingBox = bounds.IsEmpty ? new Rect(pt.X - 24, pt.Y - 24, 48, 48) : bounds,
                    IsFolder = isFolder
                };
            } catch {
                return null;
            }
        }

        public static Rect GetBoundingBoxForPath(string folderPath) {
            if (string.IsNullOrEmpty(folderPath)) {
                return Rect.Empty;
            }
            return RunComSafe<Rect>(() => GetBoundingBoxForPathCore(folderPath), 250);
        }

        private static Rect GetBoundingBoxForPathCore(string folderPath) {
            if (string.IsNullOrEmpty(folderPath)) {
                return Rect.Empty;
            }

            string folderName = Path.GetFileName(folderPath.TrimEnd('\\', '/'));
            string? parentDir = Path.GetDirectoryName(folderPath.TrimEnd('\\', '/'));

            // 1. Search UIAutomation in File Explorer windows (targeting window hosting this folder's parent directory)
            try {
                Type? shellType = GetShellAppType();
                if (shellType != null) {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell != null) {
                        dynamic windows = shell.Windows();
                        int count = windows.Count;
                        for (int i = 0; i < count; i++) {
                            dynamic? item = windows.Item(i);
                            if (item == null) continue;
                            try {
                                dynamic? doc = item.Document;
                                if (doc != null && doc.Folder != null && doc.Folder.Self != null) {
                                    string? winFolder = (string?)doc.Folder.Self.Path;
                                    if (!string.IsNullOrEmpty(winFolder) && (string.Equals(winFolder, parentDir, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(parentDir))) {
                                        long hwndLong = item.HWND;
                                        if (hwndLong != 0) {
                                            IntPtr winHwnd = new IntPtr(hwndLong);
                                            AutomationElement? expElem = AutomationElement.FromHandle(winHwnd);
                                            if (expElem != null) {
                                                var found = expElem.FindFirst(TreeScope.Descendants,
                                                    new PropertyCondition(AutomationElement.NameProperty, folderName));
                                                if (found != null) {
                                                    Rect r = found.Current.BoundingRectangle;
                                                    if (!r.IsEmpty && r.Width > 0 && r.Height > 0) {
                                                        return r;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            } catch { }
                        }
                    }
                }
            } catch { }

            // 2. Search Desktop list in UIAutomation (scoped specifically to Desktop window for instant lookup)
            try {
                IntPtr listView = GetDesktopListView();
                if (listView != IntPtr.Zero) {
                    AutomationElement? lvElem = AutomationElement.FromHandle(listView);
                    if (lvElem != null) {
                        var found = lvElem.FindFirst(TreeScope.Children,
                            new PropertyCondition(AutomationElement.NameProperty, folderName));
                        if (found != null) {
                            Rect r = found.Current.BoundingRectangle;
                            if (!r.IsEmpty && r.Width > 0 && r.Height > 0) {
                                return r;
                            }
                        }
                    }
                }

                IntPtr progman = ShellHelper.FindWindow("Progman", null);
                IntPtr defView = ShellHelper.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (defView == IntPtr.Zero) {
                    IntPtr workerW = ShellHelper.GetWallpaperWorkerW();
                    if (workerW != IntPtr.Zero) {
                        defView = ShellHelper.FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                    }
                }
                if (defView != IntPtr.Zero) {
                    AutomationElement? desktopElem = AutomationElement.FromHandle(defView);
                    if (desktopElem != null) {
                        var found = desktopElem.FindFirst(TreeScope.Children,
                            new PropertyCondition(AutomationElement.NameProperty, folderName));
                        if (found != null) {
                            Rect r = found.Current.BoundingRectangle;
                            if (!r.IsEmpty && r.Width > 0 && r.Height > 0) {
                                return r;
                            }
                        }
                    }
                }
            } catch { }

            // 3. Search UIAutomation in Foreground / Active window
            try {
                IntPtr fgHwnd = ShellHelper.GetForegroundWindow();
                if (fgHwnd != IntPtr.Zero) {
                    IntPtr rootHwnd = ShellHelper.GetAncestor(fgHwnd, ShellHelper.GA_ROOT);
                    IntPtr target = rootHwnd != IntPtr.Zero ? rootHwnd : fgHwnd;
                    if (IsExplorerOrDesktopWindow(target)) {
                        AutomationElement? fgElem = AutomationElement.FromHandle(target);
                        if (fgElem != null) {
                            var found = fgElem.FindFirst(TreeScope.Descendants,
                                new PropertyCondition(AutomationElement.NameProperty, folderName));
                            if (found != null) {
                                Rect r = found.Current.BoundingRectangle;
                                if (!r.IsEmpty && r.Width > 0 && r.Height > 0) {
                                    return r;
                                }
                            }
                        }
                    }
                }
            } catch { }

            // 4. Check if item under cursor matches
            try {
                var cursorItem = GetItemUnderCursorCore();
                if (cursorItem != null && !cursorItem.Value.BoundingBox.IsEmpty) {
                    if (string.Equals(cursorItem.Value.Path, folderPath, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(Path.GetFileName(cursorItem.Value.Path), folderName, StringComparison.OrdinalIgnoreCase)) {
                        return cursorItem.Value.BoundingBox;
                    }
                }
            } catch { }

            // 5. Fallback to cursor position
            if (ShellHelper.GetCursorPos(out var pt)) {
                return new Rect(pt.X - 24, pt.Y - 24, 48, 48);
            }

            return Rect.Empty;
        }

        public static ItemInfo? GetSelectedItemInActiveExplorer(bool resolveBounds = false) {
            return RunComSafe<ItemInfo?>(() => GetSelectedItemInActiveExplorerCore(resolveBounds), resolveBounds ? 250 : 120);
        }

        private static ItemInfo? GetSelectedItemInActiveExplorerCore(bool resolveBounds) {
            try {
                IntPtr activeHwnd = ShellHelper.GetForegroundWindow();
                ShellHelper.POINT pt = default;
                bool hasCursor = ShellHelper.GetCursorPos(out pt);

                string cls = string.Empty;
                if (activeHwnd != IntPtr.Zero) {
                    StringBuilder className = new StringBuilder(256);
                    ShellHelper.GetClassName(activeHwnd, className, className.Capacity);
                    cls = className.ToString();
                }

                bool isExplorer = cls.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
                                  cls.Equals("ExploreWClass", StringComparison.OrdinalIgnoreCase) ||
                                  cls.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
                                  cls.Equals("WorkerW", StringComparison.OrdinalIgnoreCase);

                IntPtr targetHwnd = activeHwnd;

                // If foreground window is not directly Explorer/Desktop, check its root ancestor
                if (!isExplorer && activeHwnd != IntPtr.Zero) {
                    IntPtr rootHwnd = ShellHelper.GetAncestor(activeHwnd, ShellHelper.GA_ROOT);
                    if (rootHwnd != IntPtr.Zero && rootHwnd != activeHwnd) {
                        StringBuilder rootCls = new StringBuilder(256);
                        ShellHelper.GetClassName(rootHwnd, rootCls, rootCls.Capacity);
                        string rootClsStr = rootCls.ToString();
                        if (rootClsStr.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
                            rootClsStr.Equals("ExploreWClass", StringComparison.OrdinalIgnoreCase) ||
                            rootClsStr.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
                            rootClsStr.Equals("WorkerW", StringComparison.OrdinalIgnoreCase)) {
                            targetHwnd = rootHwnd;
                            cls = rootClsStr;
                            isExplorer = true;
                        }
                    }
                }

                // If foreground window is still not Explorer/Desktop, check window under cursor
                if (!isExplorer && hasCursor) {
                    IntPtr hwndUnderPoint = ShellHelper.WindowFromPoint(pt);
                    IntPtr rootUnderPoint = ShellHelper.GetAncestor(hwndUnderPoint, ShellHelper.GA_ROOT);
                    if (rootUnderPoint != IntPtr.Zero) {
                        StringBuilder ptCls = new StringBuilder(256);
                        ShellHelper.GetClassName(rootUnderPoint, ptCls, ptCls.Capacity);
                        string ptClsStr = ptCls.ToString();
                        if (ptClsStr.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
                            ptClsStr.Equals("ExploreWClass", StringComparison.OrdinalIgnoreCase) ||
                            ptClsStr.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
                            ptClsStr.Equals("WorkerW", StringComparison.OrdinalIgnoreCase)) {
                            targetHwnd = rootUnderPoint;
                            cls = ptClsStr;
                            isExplorer = true;
                        }
                    }
                }

                if (!isExplorer && activeHwnd != IntPtr.Zero && IsExplorerOrDesktopWindow(activeHwnd)) {
                    IntPtr root = ShellHelper.GetAncestor(activeHwnd, ShellHelper.GA_ROOT);
                    targetHwnd = root != IntPtr.Zero ? root : activeHwnd;
                    StringBuilder sb = new StringBuilder(256);
                    ShellHelper.GetClassName(targetHwnd, sb, sb.Capacity);
                    cls = sb.ToString();
                    isExplorer = true;
                }

                if (!isExplorer) {
                    return null;
                }

                bool isDesktop = cls.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
                                 cls.Equals("WorkerW", StringComparison.OrdinalIgnoreCase) ||
                                 cls.Equals("SHELLDLL_DefView", StringComparison.OrdinalIgnoreCase) ||
                                 cls.Equals("SysListView32", StringComparison.OrdinalIgnoreCase);

                bool isExplorerWindow = cls.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
                                        cls.Equals("ExploreWClass", StringComparison.OrdinalIgnoreCase);

                // FIRST: Query Shell COM API on MTA worker (< 8ms), avoids RPC_E_CANTCALLOUT_ININPUTSYNCCALL and hook timeout
                string? selectedPath = isDesktop
                    ? GetDesktopSelectedPathFastCore()
                    : GetActiveExplorerSelectedPathCore(targetHwnd, allowSlowUiaFallback: resolveBounds);

                // If not found yet and target was neither desktop nor a CabinetWClass/ExploreWClass window, desktop might still have focus
                if (string.IsNullOrEmpty(selectedPath) && !isDesktop && !isExplorerWindow) {
                    selectedPath = GetDesktopSelectedPathFastCore();
                }

                Rect bounds = Rect.Empty;

                // Fallback to UIAutomation ONLY if COM did not return path and slow UIA is permitted
                if (string.IsNullOrEmpty(selectedPath) && resolveBounds) {
                    AutomationElement? focused = null;
                    try {
                        focused = AutomationElement.FocusedElement;
                        if (focused != null) {
                            var ct = focused.Current.ControlType;
                            if (ct == ControlType.ListItem || ct == ControlType.DataItem || ct == ControlType.TreeItem) {
                                Rect r = focused.Current.BoundingRectangle;
                                if (!r.IsEmpty && r.Width > 0 && r.Height > 0) {
                                    bounds = r;
                                }
                            }
                        }
                    } catch { }

                    if (focused != null && !string.IsNullOrWhiteSpace(focused.Current.Name)) {
                        string focusedName = focused.Current.Name;

                        // Check desktop directories
                        string[] desktopDirs = GetDesktopDirs();
                        foreach (var d in desktopDirs) {
                            if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) {
                                string candidate = Path.Combine(d, focusedName);
                                if (File.Exists(candidate) || Directory.Exists(candidate)) {
                                    selectedPath = candidate;
                                    break;
                                }
                                try {
                                    var dirMatches = Directory.GetDirectories(d, focusedName + ".*");
                                    if (dirMatches.Length > 0) {
                                        selectedPath = dirMatches[0];
                                        break;
                                    }
                                    var matches = Directory.GetFiles(d, focusedName + ".*");
                                    if (matches.Length > 0) {
                                        selectedPath = matches[0];
                                        break;
                                    }
                                } catch { }
                            }
                        }

                        // Also check folders of open Explorer windows
                        if (string.IsNullOrEmpty(selectedPath)) {
                            try {
                                Type? shellType = GetShellAppType();
                                if (shellType != null) {
                                    dynamic? shell = Activator.CreateInstance(shellType);
                                    if (shell != null) {
                                        dynamic windows = shell.Windows();
                                        int cnt = windows.Count;
                                        for (int w = 0; w < cnt; w++) {
                                            dynamic? item = windows.Item(w);
                                            dynamic? folderObj = item?.Document?.Folder;
                                            if (folderObj?.Self != null) {
                                                string? dir = (string?)folderObj.Self.Path;
                                                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) {
                                                    string candidate = Path.Combine(dir, focusedName);
                                                    if (Directory.Exists(candidate) || File.Exists(candidate)) {
                                                        selectedPath = candidate;
                                                        break;
                                                    }
                                                    var dirMatches = Directory.GetDirectories(dir, focusedName + ".*");
                                                    if (dirMatches.Length > 0) {
                                                        selectedPath = dirMatches[0];
                                                        break;
                                                    }
                                                    var matches = Directory.GetFiles(dir, focusedName + ".*");
                                                    if (matches.Length > 0) {
                                                        selectedPath = matches[0];
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            } catch { }
                        }

                        // Also check known Panak folders
                        if (string.IsNullOrEmpty(selectedPath)) {
                            string? match = PanakManager.Instance.FindKnownFolderByName(focusedName);
                            if (!string.IsNullOrEmpty(match)) {
                                selectedPath = match;
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(selectedPath)) {
                    return null;
                }

                if (resolveBounds) {
                    if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) {
                        bounds = GetBoundingBoxForPathCore(selectedPath);
                    }

                    if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) {
                        if (hasCursor) {
                            bounds = new Rect(pt.X - 24, pt.Y - 24, 48, 48);
                        } else {
                            bounds = new Rect(100, 100, 48, 48);
                        }
                    }
                }

                return new ItemInfo {
                    Path = selectedPath,
                    BoundingBox = bounds,
                    IsFolder = Directory.Exists(selectedPath)
                };
            } catch {
                return null;
            }
        }

        public static string? GetDesktopSelectedPathFast() {
            return RunComSafe<string?>(GetDesktopSelectedPathFastCore, 120);
        }

        private static string? GetDesktopSelectedPathFastCore() {
            try {
                Type? shellType = GetShellAppType();
                if (shellType == null) return null;
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell == null) return null;
                dynamic windows = shell.Windows();
                IShellWindows sw = (IShellWindows)windows;
                object loc = 0; // CSIDL_DESKTOP
                object locRoot = 0;
                int hwnd = 0;
                // SWC_DESKTOP = 8, SWFO_NEEDDISPATCH = 1
                object disp = sw.FindWindowSW(ref loc, ref locRoot, 8, out hwnd, 1);
                if (disp != null) {
                    dynamic d = disp;
                    dynamic? doc = d.Document;
                    if (doc != null) {
                        string? firstSelectedFile = null;
                        dynamic? selected = doc.SelectedItems();
                        if (selected != null) {
                            int cnt = (int)selected.Count;
                            for (int i = 0; i < cnt; i++) {
                                dynamic? item = selected.Item(i);
                                if (item != null) {
                                    string? spath = (string?)item.Path;
                                    if (!string.IsNullOrEmpty(spath)) {
                                        if (Directory.Exists(spath)) {
                                            return spath;
                                        }
                                        if (firstSelectedFile == null && File.Exists(spath)) {
                                            firstSelectedFile = spath;
                                        }
                                    }
                                }
                            }
                        }

                        dynamic? focusedItem = doc.FocusedItem;
                        if (focusedItem != null) {
                            string? fpath = (string?)focusedItem.Path;
                            if (!string.IsNullOrEmpty(fpath) && Directory.Exists(fpath)) {
                                return fpath;
                            }
                            if (firstSelectedFile != null) {
                                return firstSelectedFile;
                            }
                            if (!string.IsNullOrEmpty(fpath) && File.Exists(fpath)) {
                                return fpath;
                            }
                        }

                        if (firstSelectedFile != null) {
                            return firstSelectedFile;
                        }
                    }
                }
            } catch { }
            return null;
        }

        private static string? GetPathFromShellWindows(ShellHelper.POINT pt, string itemName) {
            try {
                Type? shellType = GetShellAppType();
                if (shellType == null) {
                    return null;
                }

                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell == null) {
                    return null;
                }

                dynamic windows = shell.Windows();
                int count = windows.Count;

                IntPtr hwndUnderPoint = ShellHelper.WindowFromPoint(pt);
                IntPtr rootUnderPoint = ShellHelper.GetAncestor(hwndUnderPoint, ShellHelper.GA_ROOT);
                long targetHwnd = (rootUnderPoint != IntPtr.Zero ? rootUnderPoint : hwndUnderPoint).ToInt64();

                // Pass 1: Prioritize the window under cursor
                for (int i = 0; i < count; i++) {
                    dynamic item = windows.Item(i);
                    if (item == null) continue;

                    try {
                        long hwnd = item.HWND;
                        if (hwnd == targetHwnd) {
                            dynamic doc = item.Document;
                            if (doc != null) {
                                try {
                                    dynamic? folderObj = doc.Folder;
                                    if (folderObj?.Self != null) {
                                        string? folderPath = (string?)folderObj.Self.Path;
                                        if (!string.IsNullOrEmpty(folderPath) && !string.IsNullOrWhiteSpace(itemName)) {
                                            string direct = Path.Combine(folderPath, itemName);
                                            if (Directory.Exists(direct) || File.Exists(direct)) {
                                                return direct;
                                            }
                                            var dirMatches = Directory.GetDirectories(folderPath, itemName + ".*");
                                            if (dirMatches.Length > 0) return dirMatches[0];
                                            var matches = Directory.GetFiles(folderPath, itemName + ".*");
                                            if (matches.Length > 0) return matches[0];
                                        }
                                    }
                                } catch { }

                                dynamic? selected = doc.SelectedItems();
                                if (selected != null && (int)selected.Count > 0) {
                                    int cnt = (int)selected.Count;
                                    for (int s = 0; s < cnt; s++) {
                                        dynamic? sel = selected.Item(s);
                                        if (sel != null) {
                                            string? path = sel.Path;
                                            if (!string.IsNullOrEmpty(path) && Path.GetFileName(path).Equals(itemName, StringComparison.OrdinalIgnoreCase)) {
                                                return path;
                                            }
                                        }
                                    }
                                }

                                dynamic? focusedItem = doc.FocusedItem;
                                if (focusedItem != null) {
                                    string? fpath = focusedItem.Path;
                                    if (!string.IsNullOrEmpty(fpath) && Path.GetFileName(fpath).Equals(itemName, StringComparison.OrdinalIgnoreCase)) {
                                        return fpath;
                                    }
                                }
                            }
                        }
                    } catch { }
                }

                // Pass 2: Check other Explorer windows if not found in active window
                for (int i = 0; i < count; i++) {
                    dynamic item = windows.Item(i);
                    if (item == null) continue;

                    try {
                        long hwnd = item.HWND;
                        if (hwnd == targetHwnd) continue;

                        dynamic doc = item.Document;
                        if (doc == null) continue;

                        try {
                            dynamic? folderObj = doc.Folder;
                            if (folderObj?.Self != null) {
                                string? folderPath = (string?)folderObj.Self.Path;
                                if (!string.IsNullOrEmpty(folderPath) && !string.IsNullOrWhiteSpace(itemName)) {
                                    string direct = Path.Combine(folderPath, itemName);
                                    if (Directory.Exists(direct) || File.Exists(direct)) {
                                        return direct;
                                    }
                                }
                            }
                        } catch { }

                        dynamic? selected = doc.SelectedItems();
                        if (selected != null && (int)selected.Count > 0) {
                            int cnt = (int)selected.Count;
                            for (int s = 0; s < cnt; s++) {
                                dynamic? sel = selected.Item(s);
                                if (sel != null) {
                                    string? path = sel.Path;
                                    if (!string.IsNullOrEmpty(path) && Path.GetFileName(path).Equals(itemName, StringComparison.OrdinalIgnoreCase)) {
                                        return path;
                                    }
                                }
                            }
                        }
                    } catch { }
                }

                // Check Desktop shell window
                try {
                    IShellWindows sw = (IShellWindows)windows;
                    object loc = 0; // CSIDL_DESKTOP
                    object locRoot = 0;
                    int hwnd = 0;
                    object disp = sw.FindWindowSW(ref loc, ref locRoot, 8, out hwnd, 1);
                    if (disp != null) {
                        dynamic d = disp;
                        dynamic? doc = d.Document;
                        if (doc != null) {
                            dynamic? focusedItem = doc.FocusedItem;
                            if (focusedItem != null) {
                                string? fpath = focusedItem.Path;
                                if (!string.IsNullOrEmpty(fpath) && !string.IsNullOrWhiteSpace(itemName) &&
                                    Path.GetFileName(fpath).Equals(itemName, StringComparison.OrdinalIgnoreCase)) {
                                    return fpath;
                                }
                            }
                            dynamic? selected = doc.SelectedItems();
                            if (selected != null && (int)selected.Count > 0) {
                                int cnt = (int)selected.Count;
                                for (int s = 0; s < cnt; s++) {
                                    dynamic? sel = selected.Item(s);
                                    if (sel != null) {
                                        string? path = sel.Path;
                                        if (!string.IsNullOrEmpty(path) && !string.IsNullOrWhiteSpace(itemName) &&
                                            Path.GetFileName(path).Equals(itemName, StringComparison.OrdinalIgnoreCase)) {
                                            return path;
                                        }
                                    }
                                }
                            }
                        }
                    }
                } catch { }
            } catch { }

            return null;
        }

        private static readonly Guid SID_STopLevelBrowser = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837");
        private static readonly Guid IID_IShellBrowser = new Guid("000214E2-0000-0000-C000-000000000046");

        [ComImport]
        [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IComServiceProvider {
            [PreserveSig]
            int QueryService(ref Guid guidService, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppvObject);
        }

        [ComImport]
        [Guid("000214E2-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellBrowser {
            [PreserveSig]
            int GetWindow(out IntPtr phwnd);
        }

        private static IntPtr GetShellTabHwnd(object? webBrowserItem) {
            if (webBrowserItem is IComServiceProvider sp) {
                Guid sid = SID_STopLevelBrowser;
                Guid iid = IID_IShellBrowser;
                if (sp.QueryService(ref sid, ref iid, out object browserObj) == 0 && browserObj is IShellBrowser sb) {
                    if (sb.GetWindow(out IntPtr hwnd) == 0) {
                        return hwnd;
                    }
                }
            }
            return IntPtr.Zero;
        }

        private static string? GetActiveExplorerSelectedPath(IntPtr activeHwnd) {
            return RunComSafe<string?>(() => GetActiveExplorerSelectedPathCore(activeHwnd, allowSlowUiaFallback: false), 120);
        }

        private static string? GetActiveExplorerSelectedPathCore(IntPtr activeHwnd, bool allowSlowUiaFallback = false) {
            try {
                Type? shellType = GetShellAppType();
                if (shellType == null) {
                    return null;
                }

                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell == null) {
                    return null;
                }

                dynamic windows = shell.Windows();
                int count = windows.Count;
                IntPtr rootHwnd = ShellHelper.GetAncestor(activeHwnd, ShellHelper.GA_ROOT);
                IntPtr targetPtr = rootHwnd != IntPtr.Zero ? rootHwnd : activeHwnd;
                long targetHwnd = targetPtr.ToInt64();

                uint activePid = 0;
                if (activeHwnd != IntPtr.Zero) {
                    ShellHelper.GetWindowThreadProcessId(activeHwnd, out activePid);
                }

                // In Windows 11 multi-tab File Explorer, the active tab's ShellTabWindowClass is the top child of CabinetWClass
                IntPtr activeTabHwnd = targetPtr != IntPtr.Zero
                    ? ShellHelper.FindWindowEx(targetPtr, IntPtr.Zero, "ShellTabWindowClass", null)
                    : IntPtr.Zero;

                var activeMatches = new List<dynamic>();
                var otherMatches = new List<dynamic>();

                for (int i = 0; i < count; i++) {
                    dynamic item = windows.Item(i);
                    if (item == null) continue;
                    try {
                        long hwnd = item.HWND;
                        if ((int)hwnd == (int)activeHwnd.ToInt64() || (int)hwnd == (int)targetHwnd) {
                            IntPtr tabHwnd = GetShellTabHwnd(item);
                            if (tabHwnd != IntPtr.Zero && ((activeTabHwnd != IntPtr.Zero && tabHwnd == activeTabHwnd) || IsWindowVisible(tabHwnd))) {
                                activeMatches.Add(item);
                            } else {
                                otherMatches.Add(item);
                            }
                        }
                    } catch { }
                }

                // If we identified the exact active tab in a multi-tab Explorer window, prioritize it exclusively first
                var matchingTabs = new List<dynamic>(activeMatches.Count + otherMatches.Count);
                matchingTabs.AddRange(activeMatches);
                matchingTabs.AddRange(otherMatches);

                // If no exact HWND matched (e.g. Windows 11 Explorer tab islands), match by PID or single window
                if (matchingTabs.Count == 0) {
                    for (int i = 0; i < count; i++) {
                        dynamic item = windows.Item(i);
                        if (item == null) continue;
                        try {
                            long hwnd = item.HWND;
                            uint itemPid = 0;
                            if (hwnd != 0) {
                                ShellHelper.GetWindowThreadProcessId(new IntPtr(hwnd), out itemPid);
                            }
                            if (count == 1 || (activePid != 0 && itemPid == activePid)) {
                                matchingTabs.Add(item);
                            }
                        } catch { }
                    }
                }

                if (matchingTabs.Count == 0) {
                    return null;
                }

                var primaryTabs = activeMatches.Count > 0 ? activeMatches : matchingTabs;

                // PASS 1: Check SelectedItems() on active tab first (then fallback tabs) - PREFER A DIRECTORY
                foreach (var tab in primaryTabs) {
                    try {
                        dynamic? doc = tab.Document;
                        if (doc != null) {
                            dynamic? selected = doc.SelectedItems();
                            if (selected != null && (int)selected.Count > 0) {
                                int cnt = (int)selected.Count;
                                for (int s = 0; s < cnt; s++) {
                                    dynamic? sel = selected.Item(s);
                                    if (sel != null) {
                                        string? sp = (string?)sel.Path;
                                        if (!string.IsNullOrEmpty(sp) && Directory.Exists(sp)) {
                                            return sp;
                                        }
                                    }
                                }
                            }
                        }
                    } catch { }
                }

                // PASS 2: Check FocusedItem on active tab first (e.g. keyboard arrow navigation) - PREFER A DIRECTORY
                foreach (var tab in primaryTabs) {
                    try {
                        dynamic? doc = tab.Document;
                        if (doc != null) {
                            dynamic? focusedItem = doc.FocusedItem;
                            if (focusedItem != null) {
                                string? foc = (string?)focusedItem.Path;
                                if (!string.IsNullOrEmpty(foc) && Directory.Exists(foc)) {
                                    return foc;
                                }
                            }
                        }
                    } catch { }
                }

                // PASS 3: Check SelectedItems() across all matching tabs - PREFER A DIRECTORY
                if (activeMatches.Count > 0 && otherMatches.Count > 0) {
                    foreach (var tab in otherMatches) {
                        try {
                            dynamic? doc = tab.Document;
                            if (doc != null) {
                                dynamic? selected = doc.SelectedItems();
                                if (selected != null && (int)selected.Count > 0) {
                                    int cnt = (int)selected.Count;
                                    for (int s = 0; s < cnt; s++) {
                                        dynamic? sel = selected.Item(s);
                                        if (sel != null) {
                                            string? sp = (string?)sel.Path;
                                            if (!string.IsNullOrEmpty(sp) && Directory.Exists(sp)) {
                                                return sp;
                                            }
                                        }
                                    }
                                }
                            }
                        } catch { }
                    }
                }

                // PASS 4: Check SelectedItems() across candidate tabs - ANY SELECTED ITEM (e.g. file)
                foreach (var tab in primaryTabs) {
                    try {
                        dynamic? doc = tab.Document;
                        if (doc != null) {
                            dynamic? selected = doc.SelectedItems();
                            if (selected != null && (int)selected.Count > 0) {
                                int cnt = (int)selected.Count;
                                for (int s = 0; s < cnt; s++) {
                                    dynamic? sel = selected.Item(s);
                                    if (sel != null) {
                                        string? sp = (string?)sel.Path;
                                        if (!string.IsNullOrEmpty(sp)) {
                                            return sp;
                                        }
                                    }
                                }
                            }
                        }
                    } catch { }
                }

                // PASS 5: UIAutomation FocusedElement in File Explorer (only when slow UIA fallback is explicitly allowed)
                if (allowSlowUiaFallback) {
                    try {
                        var focusedElem = AutomationElement.FocusedElement;
                        if (focusedElem != null && !string.IsNullOrWhiteSpace(focusedElem.Current.Name)) {
                            string itemName = focusedElem.Current.Name;
                            foreach (var tab in matchingTabs) {
                                try {
                                    dynamic? doc = tab.Document;
                                    dynamic? folderObj = doc?.Folder;
                                    if (folderObj?.Self != null) {
                                        string? folderPath = (string?)folderObj.Self.Path;
                                        if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath)) {
                                            string direct = Path.Combine(folderPath, itemName);
                                            if (Directory.Exists(direct) || File.Exists(direct)) {
                                                return direct;
                                            }
                                            var dirMatches = Directory.GetDirectories(folderPath, itemName + ".*");
                                            if (dirMatches.Length > 0) return dirMatches[0];
                                            var fileMatches = Directory.GetFiles(folderPath, itemName + ".*");
                                            if (fileMatches.Length > 0) return fileMatches[0];
                                        }
                                    }
                                } catch { }
                            }
                        }
                    } catch { }
                }
            } catch {
                // COM error
            }
            return null;
        }

        [ComImport]
        [Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85")]
        [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        private interface IShellWindows {
            int Count { get; }
            [return: MarshalAs(UnmanagedType.IDispatch)]
            object Item([In, Optional] object index);
            [return: MarshalAs(UnmanagedType.IDispatch)]
            object FindWindowSW([In] ref object pvarLoc, [In] ref object pvarLocRoot, [In] int swClass, out int pHWND, [In] int swFlags);
        }

        public static List<string> GetDesktopSelectedPaths() {
            return RunComSafe<List<string>>(GetDesktopSelectedPathsCore, 150) ?? new List<string>();
        }

        private static List<string> GetDesktopSelectedPathsCore() {
            var result = new List<string>();
            try {
                Type? shellType = GetShellAppType();
                if (shellType == null) return result;
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell == null) return result;
                dynamic windows = shell.Windows();
                IShellWindows sw = (IShellWindows)windows;
                object loc = 0; // CSIDL_DESKTOP
                object locRoot = 0;
                int hwnd = 0;
                // SWC_DESKTOP = 8, SWFO_NEEDDISPATCH = 1
                object disp = sw.FindWindowSW(ref loc, ref locRoot, 8, out hwnd, 1);
                if (disp != null) {
                    dynamic d = disp;
                    dynamic? doc = d.Document;
                    if (doc != null) {
                        dynamic? selected = doc.SelectedItems();
                        if (selected != null && (int)selected.Count > 0) {
                            int cnt = (int)selected.Count;
                            for (int s = 0; s < cnt; s++) {
                                dynamic? sel = selected.Item(s);
                                if (sel != null) {
                                    string? path = (string?)sel.Path;
                                    if (!string.IsNullOrEmpty(path) && !result.Contains(path, StringComparer.OrdinalIgnoreCase)) {
                                        result.Add(path);
                                    }
                                }
                            }
                        }
                    }
                }
            } catch { }
            return result;
        }

        public static List<string> GetSelectedPathsForTarget(string? targetPath = null) {
            return RunComSafe<List<string>>(() => GetSelectedPathsForTargetCore(targetPath), 200) ?? new List<string>();
        }

        private static List<string> GetSelectedPathsForTargetCore(string? targetPath = null) {
            var result = new List<string>();
            bool isTargetOnDesktop = false;
            string targetDir = string.Empty;

            if (!string.IsNullOrEmpty(targetPath)) {
                targetDir = Path.GetDirectoryName(targetPath) ?? string.Empty;
                var dDirs = GetDesktopDirs();
                for (int i = 0; i < dDirs.Length; i++) {
                    if (string.Equals(targetDir, dDirs[i], StringComparison.OrdinalIgnoreCase)) {
                        isTargetOnDesktop = true;
                        break;
                    }
                }
            }

            // 1. If target is on Desktop or target not specified, check Desktop selection first
            if (isTargetOnDesktop || string.IsNullOrEmpty(targetPath)) {
                var desktopItems = GetDesktopSelectedPathsCore();
                if (desktopItems.Count > 0) {
                    if (string.IsNullOrEmpty(targetPath) || desktopItems.Contains(targetPath, StringComparer.OrdinalIgnoreCase)) {
                        return desktopItems;
                    }
                }
            }

            // 2. Query open Explorer windows via Shell.Application
            try {
                Type? shellType = GetShellAppType();
                if (shellType != null) {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell != null) {
                        dynamic windows = shell.Windows();
                        int count = windows.Count;

                        // First pass: look for window whose SelectedItems contains targetPath
                        for (int i = 0; i < count; i++) {
                            dynamic item = windows.Item(i);
                            if (item == null) continue;
                            try {
                                dynamic? doc = item.Document;
                                if (doc != null) {
                                    dynamic? selected = doc.SelectedItems();
                                    if (selected != null && (int)selected.Count > 0) {
                                        var windowList = new List<string>();
                                        int cnt = (int)selected.Count;
                                        for (int s = 0; s < cnt; s++) {
                                            dynamic? sel = selected.Item(s);
                                            if (sel != null) {
                                                string? p = (string?)sel.Path;
                                                if (!string.IsNullOrEmpty(p) && !windowList.Contains(p, StringComparer.OrdinalIgnoreCase)) {
                                                    windowList.Add(p);
                                                }
                                            }
                                        }

                                        if (!string.IsNullOrEmpty(targetPath) && windowList.Contains(targetPath, StringComparer.OrdinalIgnoreCase)) {
                                            return windowList;
                                        }

                                        if (string.IsNullOrEmpty(targetPath) && windowList.Count > 0) {
                                            IntPtr activeHwnd = ShellHelper.GetForegroundWindow();
                                            IntPtr rootHwnd = ShellHelper.GetAncestor(activeHwnd, ShellHelper.GA_ROOT);
                                            long hwnd = item.HWND;
                                            if (hwnd == activeHwnd.ToInt64() || (rootHwnd != IntPtr.Zero && hwnd == rootHwnd.ToInt64())) {
                                                return windowList;
                                            }
                                            if (result.Count == 0) {
                                                result = windowList;
                                            }
                                        }
                                    }
                                }
                            } catch { }
                        }

                        // Second pass: match by window's folder path
                        if (!string.IsNullOrEmpty(targetDir)) {
                            for (int i = 0; i < count; i++) {
                                dynamic item = windows.Item(i);
                                if (item == null) continue;
                                try {
                                    dynamic? doc = item.Document;
                                    if (doc != null) {
                                        string? folderPath = (string?)doc.Folder?.Self?.Path;
                                        if (!string.IsNullOrEmpty(folderPath) && folderPath.TrimEnd('\\').Equals(targetDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) {
                                            dynamic? selected = doc.SelectedItems();
                                            if (selected != null && (int)selected.Count > 0) {
                                                var windowList = new List<string>();
                                                int cnt = (int)selected.Count;
                                                for (int s = 0; s < cnt; s++) {
                                                    dynamic? sel = selected.Item(s);
                                                    if (sel != null) {
                                                        string? p = (string?)sel.Path;
                                                        if (!string.IsNullOrEmpty(p) && !windowList.Contains(p, StringComparer.OrdinalIgnoreCase)) {
                                                            windowList.Add(p);
                                                        }
                                                    }
                                                }
                                                if (windowList.Count > 0) {
                                                    return windowList;
                                                }
                                            }
                                        }
                                    }
                                } catch { }
                            }
                        }
                    }
                }
            } catch { }

            // 3. Check Desktop listview via UIAutomation directly
            try {
                IntPtr listview = GetDesktopListView();
                if (listview != IntPtr.Zero) {
                    AutomationElement? desktopElem = AutomationElement.FromHandle(listview);
                    if (desktopElem != null) {
                        var selectedCondition = new PropertyCondition(SelectionItemPattern.IsSelectedProperty, true);
                        var selectedElements = desktopElem.FindAll(TreeScope.Children, selectedCondition);
                        if (selectedElements != null && selectedElements.Count > 0) {
                            string[] desktopDirs = GetDesktopDirs();

                            var desktopSelected = new List<string>();
                            foreach (AutomationElement el in selectedElements) {
                                string name = el.Current.Name;
                                if (string.IsNullOrEmpty(name)) continue;
                                foreach (var d in desktopDirs) {
                                    if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) {
                                        string candidate = Path.Combine(d, name);
                                        if (File.Exists(candidate) || Directory.Exists(candidate)) {
                                            if (!desktopSelected.Contains(candidate, StringComparer.OrdinalIgnoreCase)) {
                                                desktopSelected.Add(candidate);
                                            }
                                            break;
                                        }
                                    }
                                }
                            }

                            if (desktopSelected.Count > 0) {
                                if (string.IsNullOrEmpty(targetPath) || desktopSelected.Contains(targetPath, StringComparer.OrdinalIgnoreCase)) {
                                    return desktopSelected;
                                }
                            }
                        }
                    }
                }
            } catch { }

            // 4. Return any items found in open windows
            if (result.Count > 0) {
                return result;
            }

            // 5. If specific target was given and not found in any multi-selection, return the single target
            if (!string.IsNullOrEmpty(targetPath)) {
                return new List<string> { targetPath };
            }

            return result;
        }

        public static List<string> GetActiveExplorerSelectedPaths() {
            return GetSelectedPathsForTarget(null);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public ShellHelper.RECT rcCaret;
        }

        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        public static bool IsFocusInsideTextEdit() {
            try {
                if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase ||
                    System.Windows.Input.Keyboard.FocusedElement is System.Windows.Controls.PasswordBox) {
                    return true;
                }

                IntPtr fgHwnd = ShellHelper.GetForegroundWindow();
                if (fgHwnd == IntPtr.Zero) return false;

                uint threadId = ShellHelper.GetWindowThreadProcessId(fgHwnd, out _);
                if (threadId == 0) return false;

                GUITHREADINFO gti = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
                if (GetGUIThreadInfo(threadId, ref gti)) {
                    // Check focused control class
                    if (gti.hwndFocus != IntPtr.Zero && IsWindowVisible(gti.hwndFocus)) {
                        StringBuilder sb = new StringBuilder(64);
                        ShellHelper.GetClassName(gti.hwndFocus, sb, sb.Capacity);
                        string cls = sb.ToString();
                        if (cls.IndexOf("Edit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            cls.IndexOf("TextBox", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            cls.IndexOf("SearchEdit", StringComparison.OrdinalIgnoreCase) >= 0) {
                            return true;
                        }
                    }

                    // Check caret control class (only if it's an actual edit/text control)
                    if (gti.hwndCaret != IntPtr.Zero && gti.hwndCaret != gti.hwndFocus && IsWindowVisible(gti.hwndCaret)) {
                        StringBuilder sbCaret = new StringBuilder(64);
                        ShellHelper.GetClassName(gti.hwndCaret, sbCaret, sbCaret.Capacity);
                        string caretCls = sbCaret.ToString();
                        if (caretCls.IndexOf("Edit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            caretCls.IndexOf("TextBox", StringComparison.OrdinalIgnoreCase) >= 0) {
                            return true;
                        }
                    }
                }
            } catch {
                // Fallback
            }
            return false;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public static IntPtr GetDesktopListView() {
            // 1. Try Progman
            IntPtr progman = ShellHelper.FindWindow("Progman", null);
            if (progman != IntPtr.Zero) {
                IntPtr defView = ShellHelper.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (defView != IntPtr.Zero) {
                    IntPtr lv = ShellHelper.FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
                    if (lv != IntPtr.Zero) {
                        return lv;
                    }
                }
            }

            // 2. Try WorkerW windows (when desktop wallpaper/active desktop is running)
            IntPtr worker = IntPtr.Zero;
            do {
                worker = ShellHelper.FindWindowEx(IntPtr.Zero, worker, "WorkerW", null);
                if (worker != IntPtr.Zero) {
                    IntPtr defView = ShellHelper.FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (defView != IntPtr.Zero) {
                        IntPtr lv = ShellHelper.FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
                        if (lv != IntPtr.Zero) {
                            return lv;
                        }
                    }
                }
            } while (worker != IntPtr.Zero);

            // 3. Try GetShellWindow
            IntPtr shellWnd = ShellHelper.GetShellWindow();
            if (shellWnd != IntPtr.Zero) {
                IntPtr defView = ShellHelper.FindWindowEx(shellWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (defView != IntPtr.Zero) {
                    IntPtr lv = ShellHelper.FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
                    if (lv != IntPtr.Zero) {
                        return lv;
                    }
                }
            }

            return IntPtr.Zero;
        }

        public static void SetDesktopItemPosition(string itemName, int x, int y) {
            System.Threading.Tasks.Task.Run(async () => {
                for (int attempt = 0; attempt < 6; attempt++) {
                    await System.Threading.Tasks.Task.Delay(attempt == 0 ? 80 : 160);

                    try {
                        IntPtr listview = GetDesktopListView();
                        if (listview != IntPtr.Zero) {
                            AutomationElement? desktopElem = AutomationElement.FromHandle(listview);
                            if (desktopElem != null) {
                                var children = desktopElem.FindAll(TreeScope.Children,
                                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
                                if (children != null && children.Count > 0) {
                                    int itemIndex = -1;
                                    for (int i = 0; i < children.Count; i++) {
                                        string name = children[i].Current.Name;
                                        if (name.Equals(itemName, StringComparison.OrdinalIgnoreCase) ||
                                            name.StartsWith(itemName, StringComparison.OrdinalIgnoreCase)) {
                                            itemIndex = i;
                                            break;
                                        }
                                    }

                                    if (itemIndex >= 0) {
                                        int posX = Math.Max(10, x - 24);
                                        int posY = Math.Max(10, y - 24);

                                        IntPtr lparam = (IntPtr)((short)posY << 16 | (ushort)(short)posX);
                                        SendMessage(listview, 0x100F /* LVM_SETITEMPOSITION */, (IntPtr)itemIndex, lparam);
                                        return;
                                    }
                                }
                            }
                        }
                    } catch {
                        // Ignore
                    }
                }
            });
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;

namespace Panak.Services {
    public class HookService : IDisposable {
        private const int WH_MOUSE_LL = 14;
        private const int WH_KEYBOARD_LL = 13;

        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int VK_SPACE = 0x20;
        private const int VK_ESCAPE = 0x1B;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_SHIFT_KEY = 0x10;
        private const int VK_CONTROL_KEY = 0x11;
        private const int VK_MENU_KEY = 0x12; // Alt

        private IntPtr _mouseHook = IntPtr.Zero;
        private IntPtr _keyboardHook = IntPtr.Zero;

        // Keep references alive to avoid Garbage Collection
        private readonly LowLevelProc _mouseProc;
        private readonly LowLevelProc _keyboardProc;

        private DateTime _lastRightUp = DateTime.MinValue;
        private POINT _lastRightUpPt;
        private ExplorerHelper.ItemInfo? _lastRightUpItem;

        private DateTime _lastLeftDown = DateTime.MinValue;
        private POINT _lastLeftDownPt;
        private bool _suppressNextLeftUp = false;
        private uint _suppressedKeyVk = 0;

        // Left drag & drop (file onto file)
        private ExplorerHelper.ItemInfo? _leftDragSourceItem;
        private List<string>? _leftDragSelectedFiles;
        private POINT _leftDragStartPt;
        private bool _isLeftButtonDown = false;
        private bool _isLeftDragging = false;

        // Cached double-click metrics (refreshed periodically instead of every click)
        private static long _lastMetricsTick = 0;
        private static uint _cachedDoubleClickTime = 500;
        private static int _cachedMaxDeltaX = 4;
        private static int _cachedMaxDeltaY = 4;

        private static void GetDoubleClickMetrics(out uint dcTime, out int maxDx, out int maxDy) {
            long now = Environment.TickCount64;
            if (now - _lastMetricsTick > 10000) {
                _lastMetricsTick = now;
                uint t = ShellHelper.GetDoubleClickTime();
                _cachedDoubleClickTime = t > 0 ? t : 500;
                int dx = ShellHelper.GetSystemMetrics(ShellHelper.SM_CXDOUBLECLK);
                _cachedMaxDeltaX = dx > 0 ? dx : 4;
                int dy = ShellHelper.GetSystemMetrics(ShellHelper.SM_CYDOUBLECLK);
                _cachedMaxDeltaY = dy > 0 ? dy : 4;
            }
            dcTime = _cachedDoubleClickTime;
            maxDx = _cachedMaxDeltaX;
            maxDy = _cachedMaxDeltaY;
        }

        private readonly struct ParsedHotkey {
            public readonly uint Vk;
            public readonly bool Ctrl;
            public readonly bool Alt;
            public readonly bool Shift;
            public readonly bool IsValid;

            public ParsedHotkey(uint vk, bool ctrl, bool alt, bool shift, bool isValid) {
                Vk = vk;
                Ctrl = ctrl;
                Alt = alt;
                Shift = shift;
                IsValid = isValid;
            }
        }

        private static string? _lastHotkeyStr1;
        private static ParsedHotkey _lastParsed1;
        private static string? _lastHotkeyStr2;
        private static ParsedHotkey _lastParsed2;

        private static ParsedHotkey GetOrParseHotkey(string hotkeyString) {
            if ( ReferenceEquals(hotkeyString, _lastHotkeyStr1) || string.Equals(hotkeyString, _lastHotkeyStr1, StringComparison.Ordinal)) {
                return _lastParsed1;
            }
            if (ReferenceEquals(hotkeyString, _lastHotkeyStr2) || string.Equals(hotkeyString, _lastHotkeyStr2, StringComparison.Ordinal)) {
                return _lastParsed2;
            }

            var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) {
                return default;
            }

            bool reqCtrl = false;
            bool reqAlt = false;
            bool reqShift = false;
            string mainKeyName = "";

            foreach (var part in parts) {
                if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase)) {
                    reqCtrl = true;
                } else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) {
                    reqAlt = true;
                } else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) {
                    reqShift = true;
                } else {
                    mainKeyName = part;
                }
            }

            uint vk = string.IsNullOrEmpty(mainKeyName) ? 0 : ConvertKeyNameToVk(mainKeyName);
            var parsed = new ParsedHotkey(vk, reqCtrl, reqAlt, reqShift, vk != 0);

            _lastHotkeyStr2 = _lastHotkeyStr1;
            _lastParsed2 = _lastParsed1;
            _lastHotkeyStr1 = hotkeyString;
            _lastParsed1 = parsed;
            return parsed;
        }

        public event Action<ExplorerHelper.ItemInfo>? LeftDoubleClickTriggered;
        public event Action<ExplorerHelper.ItemInfo>? RightDoubleClickTriggered;
        public event Action<ExplorerHelper.ItemInfo>? SpaceQuickLookTriggered;
        public event Action? CloseRequested;
        public event Action<List<string>, string, Rect>? FileDroppedOnOtherFile;

        public Func<bool>? IsPopupOpenFunc { get; set; }
        public Func<bool>? IsPopupWithinGracePeriodFunc { get; set; }
        public Func<bool>? IsPopupPinnedFunc { get; set; }
        public Func<IntPtr>? GetPopupHwndFunc { get; set; }

        public HookService() {
            _mouseProc = MouseHookCallback;
            _keyboardProc = KeyboardHookCallback;
        }

        public void Start() {
            if (_mouseHook == IntPtr.Zero) {
                ExplorerHelper.WarmUp();
                using Process curProcess = Process.GetCurrentProcess();
                using ProcessModule curModule = curProcess.MainModule!;
                IntPtr hMod = GetModuleHandle(curModule.ModuleName);
                _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);
                _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
            }
        }

        public void Stop() {
            if (_mouseHook != IntPtr.Zero) {
                UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }
            if (_keyboardHook != IntPtr.Zero) {
                UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = IntPtr.Zero;
            }
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
            if (nCode >= 0) {
                try {
                    int msg = wParam.ToInt32();

                    // Fast path for WM_MOUSEMOVE: exit immediately before Marshal.PtrToStructure when not holding left button
                    if (msg == WM_MOUSEMOVE) {
                        if (!_isLeftButtonDown || _isLeftDragging || !SettingsService.Instance.Current.CombineFilesOnDrop) {
                            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
                        }

                        MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                        int dx = Math.Abs(hookStruct.pt.x - _leftDragStartPt.x);
                        int dy = Math.Abs(hookStruct.pt.y - _leftDragStartPt.y);
                        if (dx > 8 || dy > 8) {
                            _isLeftDragging = true;
                            if (_leftDragSourceItem == null) {
                                Task.Run(() => {
                                    try {
                                        var curItem = ExplorerHelper.GetItemUnderCursor();
                                        if (curItem != null && !string.IsNullOrEmpty(curItem.Value.Path) && !curItem.Value.IsFolder && File.Exists(curItem.Value.Path)) {
                                            _leftDragSourceItem = curItem.Value;
                                            _leftDragSelectedFiles = ExplorerHelper.GetActiveExplorerSelectedPaths();
                                        }
                                    } catch { }
                                });
                            }
                        }
                        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
                    }

                    // Detect click outside active folder popup
                    if (msg == WM_LBUTTONDOWN || msg == WM_RBUTTONDOWN) {
                        if (IsPopupOpenFunc?.Invoke() == true) {
                            if (IsPopupPinnedFunc?.Invoke() != true) {
                                if (IsPopupWithinGracePeriodFunc?.Invoke() != true) {
                                    IntPtr popupHwnd = GetPopupHwndFunc?.Invoke() ?? IntPtr.Zero;
                                    if (popupHwnd != IntPtr.Zero) {
                                        MSLLHOOKSTRUCT hs = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                                        ShellHelper.POINT pt = new ShellHelper.POINT { X = hs.pt.x, Y = hs.pt.y };
                                        IntPtr clickedHwnd = ShellHelper.WindowFromPoint(pt);
                                        IntPtr clickedRoot = ShellHelper.GetAncestor(clickedHwnd, ShellHelper.GA_ROOT);

                                        if (clickedHwnd != popupHwnd && clickedRoot != popupHwnd) {
                                            Application.Current.Dispatcher.BeginInvoke(() => {
                                                CloseRequested?.Invoke();
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (msg == WM_LBUTTONDOWN) {
                        if (_suppressNextLeftUp) {
                            _suppressNextLeftUp = false;
                        }

                        MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                        ShellHelper.POINT pt = new ShellHelper.POINT { X = hookStruct.pt.x, Y = hookStruct.pt.y };
                        IntPtr clickedHwnd = ShellHelper.WindowFromPoint(pt);

                        // Ignore clicks outside Windows Explorer / Desktop completely
                        if (!ExplorerHelper.IsExplorerOrDesktopWindow(clickedHwnd)) {
                            _isLeftButtonDown = false;
                            _isLeftDragging = false;
                            _leftDragStartPt = default;
                            _leftDragSourceItem = null;
                            _leftDragSelectedFiles = null;
                            _lastLeftDown = DateTime.MinValue;
                            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
                        }

                        // Track potential drag source item inside Explorer/Desktop
                        _isLeftButtonDown = true;
                        _leftDragStartPt = hookStruct.pt;
                        _isLeftDragging = false;
                        _leftDragSourceItem = null;
                        _leftDragSelectedFiles = null;

                        GetDoubleClickMetrics(out uint doubleClickTime, out int maxDeltaX, out int maxDeltaY);

                        TimeSpan elapsed = DateTime.Now - _lastLeftDown;
                        int dx = Math.Abs(hookStruct.pt.x - _lastLeftDownPt.x);
                        int dy = Math.Abs(hookStruct.pt.y - _lastLeftDownPt.y);

                        if (elapsed.TotalMilliseconds <= doubleClickTime && dx <= maxDeltaX && dy <= maxDeltaY) {
                            ExplorerHelper.ItemInfo? item = ExplorerHelper.GetSelectedItemInActiveExplorer(resolveBounds: false);
                            if (item == null || string.IsNullOrEmpty(item.Value.Path) || !PanakManager.Instance.IsPanakFolder(item.Value.Path)) {
                                item = ExplorerHelper.GetItemUnderCursor(timeoutMs: 60);
                            }

                            if (item != null && !string.IsNullOrEmpty(item.Value.Path) && PanakManager.Instance.IsPanakFolder(item.Value.Path)) {
                                _lastLeftDown = DateTime.MinValue;
                                _isLeftButtonDown = false;
                                _suppressNextLeftUp = true;
                                var targetItem = item.Value;
                                Application.Current.Dispatcher.BeginInvoke(() => {
                                    LeftDoubleClickTriggered?.Invoke(targetItem);
                                });
                                return (IntPtr)1; // Swallow second click so Windows Explorer does not open the folder!
                            } else {
                                _lastLeftDown = DateTime.Now;
                                _lastLeftDownPt = hookStruct.pt;
                            }
                        } else {
                            _lastLeftDown = DateTime.Now;
                            _lastLeftDownPt = hookStruct.pt;
                        }
                    } else if (msg == WM_LBUTTONUP) {
                        _isLeftButtonDown = false;
                        _leftDragStartPt = default;

                        if (_suppressNextLeftUp) {
                            _suppressNextLeftUp = false;
                            _isLeftDragging = false;
                            _leftDragSourceItem = null;
                            _leftDragSelectedFiles = null;
                            return (IntPtr)1; // Swallow mouse up
                        }

                        bool wasDragging = _isLeftDragging;
                        var srcItem = _leftDragSourceItem;
                        var selectedFiles = _leftDragSelectedFiles;

                        _isLeftDragging = false;
                        _leftDragSourceItem = null;
                        _leftDragSelectedFiles = null;

                        if (wasDragging && srcItem.HasValue && !string.IsNullOrEmpty(srcItem.Value.Path) && SettingsService.Instance.Current.CombineFilesOnDrop) {
                            Task.Run(() => {
                                try {
                                    var dropTarget = ExplorerHelper.GetItemUnderCursor();
                                    if (dropTarget != null && !string.IsNullOrEmpty(dropTarget.Value.Path) && !dropTarget.Value.IsFolder && File.Exists(dropTarget.Value.Path)) {
                                        if (!string.Equals(dropTarget.Value.Path, srcItem.Value.Path, StringComparison.OrdinalIgnoreCase)) {
                                            var files = new List<string>();
                                            if (selectedFiles != null && selectedFiles.Contains(srcItem.Value.Path, StringComparer.OrdinalIgnoreCase)) {
                                                files.AddRange(selectedFiles);
                                            } else {
                                                files.Add(srcItem.Value.Path);
                                            }
                                            if (!files.Contains(dropTarget.Value.Path, StringComparer.OrdinalIgnoreCase)) {
                                                files.Add(dropTarget.Value.Path);
                                            }
                                            var targetPath = dropTarget.Value.Path;
                                            var targetBounds = dropTarget.Value.BoundingBox;
                                            Application.Current.Dispatcher.BeginInvoke(() => {
                                                FileDroppedOnOtherFile?.Invoke(files, targetPath, targetBounds);
                                            });
                                        }
                                    }
                                } catch { }
                            });
                        }
                    } else if (msg == WM_RBUTTONUP) {
                        if (SettingsService.Instance.Current.RightDoubleClickEnabled) {
                            MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                            GetDoubleClickMetrics(out uint doubleClickTime, out int maxDeltaX, out int maxDeltaY);

                            TimeSpan elapsed = DateTime.Now - _lastRightUp;
                            int dx = Math.Abs(hookStruct.pt.x - _lastRightUpPt.x);
                            int dy = Math.Abs(hookStruct.pt.y - _lastRightUpPt.y);

                            if (elapsed.TotalMilliseconds <= doubleClickTime && dx <= maxDeltaX && dy <= maxDeltaY) {
                                _lastRightUp = DateTime.MinValue;
                                var targetItem = _lastRightUpItem;
                                _lastRightUpItem = null;

                                if (targetItem == null || string.IsNullOrEmpty(targetItem.Value.Path)) {
                                    ShellHelper.POINT pt = new ShellHelper.POINT { X = hookStruct.pt.x, Y = hookStruct.pt.y };
                                    if (ExplorerHelper.IsExplorerOrDesktopWindow(ShellHelper.WindowFromPoint(pt))) {
                                        targetItem = ExplorerHelper.GetItemUnderCursor(timeoutMs: 80);
                                    }
                                }

                                if (targetItem != null && !string.IsNullOrEmpty(targetItem.Value.Path) && Directory.Exists(targetItem.Value.Path)) {
                                    var item = targetItem.Value;
                                    ShellHelper.DismissActiveContextMenu();
                                    Application.Current.Dispatcher.BeginInvoke(() => {
                                        RightDoubleClickTriggered?.Invoke(item);
                                    });
                                    return (IntPtr)1; // Suppress context menu
                                }
                            } else {
                                _lastRightUp = DateTime.Now;
                                _lastRightUpPt = hookStruct.pt;
                                ShellHelper.POINT pt = new ShellHelper.POINT { X = hookStruct.pt.x, Y = hookStruct.pt.y };
                                IntPtr wndUnderCursor = ShellHelper.WindowFromPoint(pt);
                                if (ExplorerHelper.IsExplorerOrDesktopWindow(wndUnderCursor)) {
                                    Task.Run(() => {
                                        try {
                                            _lastRightUpItem = ExplorerHelper.GetItemUnderCursor(timeoutMs: 120);
                                        } catch { }
                                    });
                                } else {
                                    _lastRightUpItem = null;
                                }
                            }
                        }
                    }
                } catch { }
            }
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        public static bool MatchesHotkey(string? hotkeyString, uint vkCode) {
            if (string.IsNullOrWhiteSpace(hotkeyString)) return false;

            ParsedHotkey parsed = GetOrParseHotkey(hotkeyString);
            if (!parsed.IsValid || vkCode != parsed.Vk) {
                return false;
            }

            // Only query modifier key states when the main virtual key matches
            bool isCtrlPressed = (GetAsyncKeyState(VK_CONTROL_KEY) & 0x8000) != 0;
            if (parsed.Ctrl != isCtrlPressed) return false;

            bool isAltPressed = (GetAsyncKeyState(VK_MENU_KEY) & 0x8000) != 0;
            if (parsed.Alt != isAltPressed) return false;

            bool isShiftPressed = (GetAsyncKeyState(VK_SHIFT_KEY) & 0x8000) != 0;
            return parsed.Shift == isShiftPressed;
        }

        public static uint ConvertKeyNameToVk(string keyName) {
            if (string.IsNullOrWhiteSpace(keyName)) return 0;
            string norm = keyName.Trim();

            if (norm.Equals("Space", StringComparison.OrdinalIgnoreCase) || norm.Equals("␣", StringComparison.OrdinalIgnoreCase)) return 0x20;
            if (norm.Equals("Escape", StringComparison.OrdinalIgnoreCase) || norm.Equals("Esc", StringComparison.OrdinalIgnoreCase)) return 0x1B;
            if (norm.Equals("Tab", StringComparison.OrdinalIgnoreCase)) return 0x09;
            if (norm.Equals("Return", StringComparison.OrdinalIgnoreCase) || norm.Equals("Enter", StringComparison.OrdinalIgnoreCase)) return 0x0D;
            if (norm.Equals("Back", StringComparison.OrdinalIgnoreCase) || norm.Equals("Backspace", StringComparison.OrdinalIgnoreCase)) return 0x08;

            if (Enum.TryParse<System.Windows.Input.Key>(norm, true, out var wpfKey)) {
                int vk = System.Windows.Input.KeyInterop.VirtualKeyFromKey(wpfKey);
                if (vk > 0) return (uint)vk;
            }

            if (norm.Length == 1) {
                char ch = char.ToUpperInvariant(norm[0]);
                if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) {
                    return (uint)ch;
                }
            }

            return 0;
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
            if (nCode >= 0) {
                try {
                    int msg = wParam.ToInt32();
                    if (msg == WM_KEYUP || msg == WM_SYSKEYUP) {
                        if (_suppressedKeyVk != 0) {
                            KBDLLHOOKSTRUCT upStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                            if (upStruct.vkCode == _suppressedKeyVk) {
                                _suppressedKeyVk = 0;
                                return (IntPtr)1;
                            }
                        }
                        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
                    }

                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN) {
                        KBDLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                        bool isPopupOpen = IsPopupOpenFunc?.Invoke() ?? false;
                        bool isPopupPinned = IsPopupPinnedFunc?.Invoke() ?? false;

                        string closeHotkey = SettingsService.Instance.Current.ClosePopupHotkey ?? "Escape";
                        string quickLookHotkey = SettingsService.Instance.Current.QuickLookHotkey ?? "Space";

                        if (isPopupOpen && MatchesHotkey(closeHotkey, hookStruct.vkCode)) {
                            if (!isPopupPinned) {
                                _suppressedKeyVk = hookStruct.vkCode;
                                Application.Current.Dispatcher.BeginInvoke(() => {
                                    CloseRequested?.Invoke();
                                });
                                return (IntPtr)1;
                            }
                        }

                        bool isSpaceKey = (hookStruct.vkCode == 0x20) &&
                            ((GetAsyncKeyState(VK_CONTROL_KEY) & 0x8000) == 0) &&
                            ((GetAsyncKeyState(VK_MENU_KEY) & 0x8000) == 0) &&
                            ((GetAsyncKeyState(VK_SHIFT_KEY) & 0x8000) == 0);

                        bool isQuickLookTrigger = isSpaceKey || MatchesHotkey(quickLookHotkey, hookStruct.vkCode);

                        if (isQuickLookTrigger) {
                            if (isPopupOpen) {
                                if (!isPopupPinned && !ExplorerHelper.IsFocusInsideTextEdit()) {
                                    _suppressedKeyVk = hookStruct.vkCode;
                                    Application.Current.Dispatcher.BeginInvoke(() => {
                                        CloseRequested?.Invoke();
                                    });
                                    return (IntPtr)1;
                                }
                            } else if (SettingsService.Instance.Current.SpaceQuickLookEnabled && ExplorerHelper.IsForegroundOrCursorExplorerOrDesktop()) {
                                if (!ExplorerHelper.IsFocusInsideTextEdit()) {
                                    var item = ExplorerHelper.GetSelectedItemInActiveExplorer(resolveBounds: false);
                                    if (item == null || string.IsNullOrEmpty(item.Value.Path)) {
                                        var cursorItem = ExplorerHelper.GetItemUnderCursor(timeoutMs: 60);
                                        if (cursorItem != null && !string.IsNullOrEmpty(cursorItem.Value.Path) && Directory.Exists(cursorItem.Value.Path)) {
                                            item = cursorItem;
                                        }
                                    }

                                    if (item != null && !string.IsNullOrEmpty(item.Value.Path) && Directory.Exists(item.Value.Path)) {
                                        _suppressedKeyVk = hookStruct.vkCode;
                                        var targetItem = item.Value;
                                        Application.Current.Dispatcher.BeginInvoke(() => {
                                            SpaceQuickLookTriggered?.Invoke(targetItem);
                                        });
                                        return (IntPtr)1; // Swallow key so Explorer/Desktop never deselects the folder
                                    }
                                }
                            }
                        }

                        // Enter key on Panak folder in Windows Explorer or Desktop
                        if (hookStruct.vkCode == 0x0D) { // VK_RETURN
                            if (!isPopupOpen && ExplorerHelper.IsForegroundOrCursorExplorerOrDesktop() && !ExplorerHelper.IsFocusInsideTextEdit()) {
                                var item = ExplorerHelper.GetSelectedItemInActiveExplorer(resolveBounds: false);
                                if (item == null || string.IsNullOrEmpty(item.Value.Path)) {
                                    var cursorItem = ExplorerHelper.GetItemUnderCursor(timeoutMs: 60);
                                    if (cursorItem != null && !string.IsNullOrEmpty(cursorItem.Value.Path) && PanakManager.Instance.IsPanakFolder(cursorItem.Value.Path)) {
                                        item = cursorItem;
                                    }
                                }

                                if (item != null && !string.IsNullOrEmpty(item.Value.Path) && PanakManager.Instance.IsPanakFolder(item.Value.Path)) {
                                    _suppressedKeyVk = hookStruct.vkCode;
                                    var targetItem = item.Value;
                                    Application.Current.Dispatcher.BeginInvoke(() => {
                                        LeftDoubleClickTriggered?.Invoke(targetItem);
                                    });
                                    return (IntPtr)1; // Swallow Enter key so Windows Explorer does not open the folder!
                                }
                            }
                        }
                    }
                } catch { }
            }
            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        public void Dispose() {
            Stop();
        }
    }
}

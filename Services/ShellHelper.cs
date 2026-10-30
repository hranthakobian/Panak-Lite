using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Panak.Services {
    public static class ShellHelper {
        public const uint SHCNE_UPDATEITEM = 0x00002000;
        public const uint SHCNE_UPDATEDIR = 0x00001000;
        public const uint SHCNE_ASSOCCHANGED = 0x08000000;
        public const uint SHCNF_PATHW = 0x0005;
        public const uint SHCNF_FLUSH = 0x1000;
        public const uint SHCNF_FLUSHNOWAIT = 0x2000;

        public const uint WM_COMMAND = 0x0111;
        public const uint WM_KEYDOWN = 0x0100;
        public const uint WM_KEYUP = 0x0101;
        public const int VK_F5 = 0x74;
        public const int CMD_SHELL_REFRESH_1 = 28931; // 0x7103
        public const int CMD_SHELL_REFRESH_2 = 41504; // 0xA220

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetThreadDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool CloseDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateWindow(IntPtr hWnd);

        private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, System.Text.StringBuilder? packageFullName);

        private static bool? _isPackaged;
        public static bool IsPackagedProcess() {
            if (_isPackaged.HasValue) return _isPackaged.Value;
            try {
                int length = 0;
                int result = GetCurrentPackageFullName(ref length, null);
                _isPackaged = (result != APPMODEL_ERROR_NO_PACKAGE);
            } catch {
                _isPackaged = false;
            }
            return _isPackaged.Value;
        }

        public const uint SHGFI_ICON = 0x000000100;
        public const uint SHGFI_LARGEICON = 0x000000000;
        public const uint SHGFI_SMALLICON = 0x000000001;
        public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFOLDERCUSTOMSETTINGS {
            public uint dwSize;
            public uint dwMask;
            public IntPtr pvid;
            public string pszWebViewTemplate;
            public uint cchWebViewTemplate;
            public string pszWebViewTemplateVersion;
            public string pszInfoTip;
            public uint cchInfoTip;
            public IntPtr pclsid;
            public uint dwFlags;
            public string pszIconFile;
            public uint cchIconFile;
            public int iIconIndex;
            public string pszLogo;
            public uint cchLogo;
        }

        public const uint FCSM_ICONFILE = 0x00000010;
        public const uint FCS_FORCEWRITE = 0x00000002;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern int SHGetSetFolderCustomSettings(ref SHFOLDERCUSTOMSETTINGS pfcs, string pszPath, uint dwReadWrite);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        public const uint WM_SETICON = 0x0080;
        public const int ICON_SMALL = 0;
        public const int ICON_BIG = 1;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll", SetLastError = true)]
        public static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        public static extern uint GetDoubleClickTime();

        [DllImport("user32.dll")]
        public static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);


        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll")]
        public static extern bool StretchBlt(IntPtr hdcDest, int nXOriginDest, int nYOriginDest, int nWidthDest, int nHeightDest,
            IntPtr hdcSrc, int nXOriginSrc, int nYOriginSrc, int nWidthSrc, int nHeightSrc, int dwRop);

        [DllImport("gdi32.dll")]
        public static extern int SetStretchBltMode(IntPtr hdc, int iStretchMode);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public static readonly IntPtr HWND_TOP = new IntPtr(0);
        public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;

        public static void ForceForegroundWindow(IntPtr hWnd) {
            if (hWnd == IntPtr.Zero) {
                return;
            }
            try {
                IntPtr fgHwnd = GetForegroundWindow();
                uint fgThread = GetWindowThreadProcessId(fgHwnd, out _);
                uint currentThread = GetCurrentThreadId();

                if (fgThread != 0 && fgThread != currentThread) {
                    AttachThreadInput(currentThread, fgThread, true);
                    BringWindowToTop(hWnd);
                    SetForegroundWindow(hWnd);
                    SetActiveWindow(hWnd);
                    AttachThreadInput(currentThread, fgThread, false);
                } else {
                    BringWindowToTop(hWnd);
                    SetForegroundWindow(hWnd);
                    SetActiveWindow(hWnd);
                }
            } catch {
                SetForegroundWindow(hWnd);
            }
        }

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        public const uint GA_ROOT = 2;

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        public static IntPtr GetWindowLong(IntPtr hWnd, int nIndex) {
            if (IntPtr.Size == 8)
                return GetWindowLongPtr64(hWnd, nIndex);
            return GetWindowLong32(hWnd, nIndex);
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        public static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong) {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            return SetWindowLong32(hWnd, nIndex, dwNewLong);
        }

        public const int SM_CXDOUBLECLK = 36;
        public const int SM_CYDOUBLECLK = 37;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT {
            public int X;
            public int Y;
        }


        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

        [DllImport("user32.dll")]
        public static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        public const uint SHCNE_MKDIR = 0x00000008;
        public const uint SHCNE_CREATE = 0x00000002;
        public const uint SHCNE_DELETE = 0x00000004;
        public const uint SHCNE_RMDIR = 0x00000010;
        public const uint SHCNE_RENAMEFOLDER = 0x00020000;

        public static void NotifyFolderRenamed(string oldPath, string newPath) {
            try {
                IntPtr pOld = Marshal.StringToHGlobalUni(oldPath);
                IntPtr pNew = Marshal.StringToHGlobalUni(newPath);
                string parentOld = Path.GetDirectoryName(oldPath) ?? string.Empty;
                string parentNew = Path.GetDirectoryName(newPath) ?? string.Empty;
                IntPtr pParentOld = !string.IsNullOrEmpty(parentOld) ? Marshal.StringToHGlobalUni(parentOld) : IntPtr.Zero;
                IntPtr pParentNew = !string.IsNullOrEmpty(parentNew) ? Marshal.StringToHGlobalUni(parentNew) : IntPtr.Zero;

                SHChangeNotify(SHCNE_RENAMEFOLDER, SHCNF_PATHW, pOld, pNew);
                if (pParentOld != IntPtr.Zero) {
                    SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, pParentOld, IntPtr.Zero);
                }
                if (pParentNew != IntPtr.Zero && pParentNew != pParentOld) {
                    SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, pParentNew, IntPtr.Zero);
                }

                Marshal.FreeHGlobal(pOld);
                Marshal.FreeHGlobal(pNew);
                if (pParentOld != IntPtr.Zero) Marshal.FreeHGlobal(pParentOld);
                if (pParentNew != IntPtr.Zero && pParentNew != pParentOld) Marshal.FreeHGlobal(pParentNew);
            } catch {
                // Ignore notification failure
            }
        }

        public static void NotifyFolderIconChanged(string folderPath, string? iconFilePath = null) {
            try {
                if (!Directory.Exists(folderPath)) {
                    return;
                }

                // Ensure folder ReadOnly attribute is set so Windows Explorer honors desktop.ini
                try {
                    var folderAttrs = File.GetAttributes(folderPath);
                    if (!folderAttrs.HasFlag(FileAttributes.ReadOnly)) {
                        File.SetAttributes(folderPath, folderAttrs | FileAttributes.ReadOnly);
                    }
                } catch { }

                // Force Windows Shell to register the new custom folder icon settings immediately
                if (!string.IsNullOrEmpty(iconFilePath) && File.Exists(iconFilePath)) {
                    try {
                        var fcs = new SHFOLDERCUSTOMSETTINGS();
                        fcs.dwSize = (uint)Marshal.SizeOf(typeof(SHFOLDERCUSTOMSETTINGS));
                        fcs.dwMask = FCSM_ICONFILE;
                        fcs.pszIconFile = Path.GetFileName(iconFilePath);
                        fcs.iIconIndex = 0;
                        SHGetSetFolderCustomSettings(ref fcs, folderPath, FCS_FORCEWRITE);
                    } catch { }
                }

                string desktopIni = Path.Combine(folderPath, "desktop.ini");
                IntPtr pIni = File.Exists(desktopIni) ? Marshal.StringToHGlobalUni(desktopIni) : IntPtr.Zero;
                IntPtr pIco = (!string.IsNullOrEmpty(iconFilePath) && File.Exists(iconFilePath)) ? Marshal.StringToHGlobalUni(iconFilePath) : IntPtr.Zero;
                IntPtr pPath = Marshal.StringToHGlobalUni(folderPath);
                string parent = Path.GetDirectoryName(folderPath) ?? string.Empty;
                IntPtr pParent = !string.IsNullOrEmpty(parent) ? Marshal.StringToHGlobalUni(parent) : IntPtr.Zero;

                uint notifyFlags = SHCNF_PATHW | SHCNF_FLUSHNOWAIT;

                if (pIco != IntPtr.Zero) {
                    SHChangeNotify(SHCNE_UPDATEITEM, notifyFlags, pIco, IntPtr.Zero);
                }

                if (pIni != IntPtr.Zero) {
                    SHChangeNotify(SHCNE_UPDATEITEM, notifyFlags, pIni, IntPtr.Zero);
                }

                SHChangeNotify(SHCNE_UPDATEITEM, notifyFlags, pPath, IntPtr.Zero);
                SHChangeNotify(SHCNE_UPDATEDIR, notifyFlags, pPath, IntPtr.Zero);

                if (pParent != IntPtr.Zero) {
                    SHChangeNotify(SHCNE_UPDATEITEM, notifyFlags, pParent, IntPtr.Zero);
                    SHChangeNotify(SHCNE_UPDATEDIR, notifyFlags, pParent, IntPtr.Zero);
                }

                if (pIco != IntPtr.Zero) {
                    Marshal.FreeHGlobal(pIco);
                }
                if (pIni != IntPtr.Zero) {
                    Marshal.FreeHGlobal(pIni);
                }
                Marshal.FreeHGlobal(pPath);
                if (pParent != IntPtr.Zero) {
                    Marshal.FreeHGlobal(pParent);
                }

                // Trigger instant visual refresh on Desktop and Explorer windows in real-time
                RefreshDesktopAndExplorerViews();
            } catch {
                // Ignore notification failure
            }
        }

        public static (IntPtr defView, IntPtr listView) GetDesktopHandles() {
            try {
                IntPtr progman = FindWindow("Progman", null);
                IntPtr defView = IntPtr.Zero;
                if (progman != IntPtr.Zero) {
                    defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
                }

                if (defView == IntPtr.Zero) {
                    IntPtr workerW = IntPtr.Zero;
                    while ((workerW = FindWindowEx(IntPtr.Zero, workerW, "WorkerW", null)) != IntPtr.Zero) {
                        defView = FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                        if (defView != IntPtr.Zero) break;
                    }
                }

                if (defView == IntPtr.Zero) {
                    try {
                        IntPtr hDesk = OpenDesktop("Default", 0, false, 0x01FF);
                        if (hDesk != IntPtr.Zero) {
                            SetThreadDesktop(hDesk);
                            progman = FindWindow("Progman", null);
                            if (progman != IntPtr.Zero) {
                                defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
                            }
                            if (defView == IntPtr.Zero) {
                                IntPtr workerW = IntPtr.Zero;
                                while ((workerW = FindWindowEx(IntPtr.Zero, workerW, "WorkerW", null)) != IntPtr.Zero) {
                                    defView = FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                                    if (defView != IntPtr.Zero) break;
                                }
                            }
                        }
                    } catch { }
                }

                IntPtr listView = IntPtr.Zero;
                if (defView != IntPtr.Zero) {
                    listView = FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
                }

                return (defView, listView);
            } catch {
                return (IntPtr.Zero, IntPtr.Zero);
            }
        }

        public static IntPtr GetDesktopDefViewHandle() {
            return GetDesktopHandles().defView;
        }

        private static long _lastDesktopRefreshTick = 0;

        public static void RefreshDesktopAndExplorerViews() {
            try {
                long now = Environment.TickCount64;
                if (now - _lastDesktopRefreshTick < 250) {
                    return;
                }
                _lastDesktopRefreshTick = now;

                var (defView, listView) = GetDesktopHandles();
                if (listView != IntPtr.Zero) {
                    InvalidateRect(listView, IntPtr.Zero, false);
                } else if (defView != IntPtr.Zero) {
                    InvalidateRect(defView, IntPtr.Zero, false);
                }
            } catch { }
        }

        public static void NotifyShellItemChanged(string path) {
            try {
                IntPtr pPath = Marshal.StringToHGlobalUni(path);
                string parent = Path.GetDirectoryName(path) ?? string.Empty;
                IntPtr pParent = !string.IsNullOrEmpty(parent) ? Marshal.StringToHGlobalUni(parent) : IntPtr.Zero;

                SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, pPath, IntPtr.Zero);
                SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, pPath, IntPtr.Zero);
                if (pParent != IntPtr.Zero) {
                    SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, pParent, IntPtr.Zero);
                    SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, pParent, IntPtr.Zero);
                }

                Marshal.FreeHGlobal(pPath);
                if (pParent != IntPtr.Zero) {
                    Marshal.FreeHGlobal(pParent);
                }
            } catch {
                // Ignore notification failure
            }
        }

        public static void NotifyFolderCreated(string folderPath) {
            try {
                IntPtr pPath = Marshal.StringToHGlobalUni(folderPath);
                string parent = Path.GetDirectoryName(folderPath) ?? string.Empty;
                IntPtr pParent = !string.IsNullOrEmpty(parent) ? Marshal.StringToHGlobalUni(parent) : IntPtr.Zero;

                SHChangeNotify(SHCNE_MKDIR, SHCNF_PATHW, pPath, IntPtr.Zero);
                SHChangeNotify(SHCNE_CREATE, SHCNF_PATHW, pPath, IntPtr.Zero);
                if (pParent != IntPtr.Zero) {
                    SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, pParent, IntPtr.Zero);
                }
                SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, pPath, IntPtr.Zero);

                Marshal.FreeHGlobal(pPath);
                if (pParent != IntPtr.Zero) {
                    Marshal.FreeHGlobal(pParent);
                }
            } catch {
                // Fallback
            }
        }

        public static void NotifyShellAssocChanged() {
            try {
                SHChangeNotify(SHCNE_ASSOCCHANGED, 0 /* SHCNF_IDLIST */, IntPtr.Zero, IntPtr.Zero);
            } catch {
                // Ignore notification failure
            }
        }

        public static void NotifyItemDeleted(string path, bool isDirectory = false) {
            try {
                IntPtr pPath = Marshal.StringToHGlobalUni(path);
                string parent = Path.GetDirectoryName(path) ?? string.Empty;
                IntPtr pParent = !string.IsNullOrEmpty(parent) ? Marshal.StringToHGlobalUni(parent) : IntPtr.Zero;

                uint eventId = isDirectory ? SHCNE_RMDIR : SHCNE_DELETE;
                SHChangeNotify(eventId, SHCNF_PATHW, pPath, IntPtr.Zero);
                if (pParent != IntPtr.Zero) {
                    SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW, pParent, IntPtr.Zero);
                }

                Marshal.FreeHGlobal(pPath);
                if (pParent != IntPtr.Zero) {
                    Marshal.FreeHGlobal(pParent);
                }
            } catch {
                // Ignore notification failure
            }
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLinkClass { }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("0000010b-0000-0000-C000-000000000046")]
        private interface IPersistFile {
            void GetClassID(out Guid pClassID);
            [PreserveSig]
            int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] string ppszFileName);
        }

        public static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".ico", ".tif", ".tiff", ".svg"
        };

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory {
            [PreserveSig]
            int GetImage([In] SIZE size, [In] int flags, [Out] out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE {
            public int cx;
            public int cy;
            public SIZE(int x, int y) { cx = x; cy = y; }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            IntPtr pbc,
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [Out, MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        private static readonly Guid IID_IShellItemImageFactory = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");

        // High-performance in-memory caching for icons and thumbnails across all system operations
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BitmapSource> _extensionIconSourceCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (long LastWriteTime, BitmapSource Source)> _fileIconSourceCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (long LastWriteTime, BitmapSource Source)> _thumbnailSourceCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Bitmap> _extensionIconBitmapCache = new(StringComparer.OrdinalIgnoreCase);
        private static BitmapSource? _cachedFolderIconLarge;
        private static BitmapSource? _cachedFolderIconSmall;

        public static string? ResolveShortcut(string lnkPath) {
            try {
                if (!File.Exists(lnkPath)) return null;
                var link = (IShellLinkW)new ShellLinkClass();
                var file = (IPersistFile)link;
                file.Load(lnkPath, 0);
                var sb = new System.Text.StringBuilder(1024);
                link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
                string target = sb.ToString();
                if (!string.IsNullOrEmpty(target) && (File.Exists(target) || Directory.Exists(target))) {
                    return target;
                }
            } catch { }
            return null;
        }

        public static Bitmap? GetShellThumbnailBitmap(string filePath, int targetDim) {
            try {
                int hr = SHCreateItemFromParsingName(filePath, IntPtr.Zero, IID_IShellItemImageFactory, out var factory);
                if (hr == 0 && factory != null) {
                    IntPtr hBitmap;
                    int hrThumb = factory.GetImage(new SIZE(targetDim, targetDim), 0x0, out hBitmap);
                    if (hrThumb == 0 && hBitmap != IntPtr.Zero) {
                        try {
                            using var temp = Image.FromHbitmap(hBitmap);
                            return new Bitmap(temp);
                        } finally {
                            DeleteObject(hBitmap);
                        }
                    }
                }
            } catch { }

            if (filePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) {
                return RenderSvgFallbackBitmap(filePath, targetDim);
            }

            return null;
        }

        public static BitmapSource? GetShellThumbnailSource(string filePath, int targetDim) {
            try {
                int hr = SHCreateItemFromParsingName(filePath, IntPtr.Zero, IID_IShellItemImageFactory, out var factory);
                if (hr == 0 && factory != null) {
                    IntPtr hBitmap;
                    int hrThumb = factory.GetImage(new SIZE(targetDim, targetDim), 0x0, out hBitmap);
                    if (hrThumb == 0 && hBitmap != IntPtr.Zero) {
                        try {
                            using var temp = Image.FromHbitmap(hBitmap);
                            using var bmp = new Bitmap(temp);
                            return CreateFrozenBitmapSourceFast(bmp);
                        } finally {
                            DeleteObject(hBitmap);
                        }
                    }
                }
            } catch { }

            if (filePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) {
                using var fb = RenderSvgFallbackBitmap(filePath, targetDim);
                if (fb != null) {
                    return CreateFrozenBitmapSourceFast(fb);
                }
            }

            return null;
        }

        public static Bitmap? RenderSvgFallbackBitmap(string filePath, int targetDim) {
            try {
                if (!File.Exists(filePath)) return null;
                string content = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(content)) return null;

                var match = System.Text.RegularExpressions.Regex.Match(content, @"<path[^>]*\bd=""([^""]+)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!match.Success) {
                    match = System.Text.RegularExpressions.Regex.Match(content, @"\bd=""([^""]+)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }

                if (match.Success) {
                    string pathData = match.Groups[1].Value;
                    var geom = System.Windows.Media.Geometry.Parse(pathData);
                    var bounds = geom.Bounds;
                    if (bounds.Width > 0 && bounds.Height > 0) {
                        double scale = Math.Min((targetDim - 8) / bounds.Width, (targetDim - 8) / bounds.Height);
                        var transform = new System.Windows.Media.TransformGroup();
                        transform.Children.Add(new System.Windows.Media.TranslateTransform(-bounds.X, -bounds.Y));
                        transform.Children.Add(new System.Windows.Media.ScaleTransform(scale, scale));
                        transform.Children.Add(new System.Windows.Media.TranslateTransform((targetDim - bounds.Width * scale) / 2.0, (targetDim - bounds.Height * scale) / 2.0));
                        geom.Transform = transform;

                        var visual = new System.Windows.Media.DrawingVisual();
                        using (var dc = visual.RenderOpen()) {
                            var fillMatch = System.Text.RegularExpressions.Regex.Match(content, @"fill=""(#?[a-zA-Z0-9]+)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            System.Windows.Media.Brush brush = System.Windows.Media.Brushes.RoyalBlue;
                            if (fillMatch.Success && !fillMatch.Groups[1].Value.Equals("none", StringComparison.OrdinalIgnoreCase)) {
                                try {
                                    var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(fillMatch.Groups[1].Value);
                                    brush = new System.Windows.Media.SolidColorBrush(c);
                                } catch { }
                            }
                            dc.DrawGeometry(brush, null, geom);
                        }

                        var rtb = new RenderTargetBitmap(targetDim, targetDim, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                        rtb.Render(visual);
                        rtb.Freeze();

                        var enc = new PngBitmapEncoder();
                        enc.Frames.Add(BitmapFrame.Create(rtb));
                        using var ms = new MemoryStream();
                        enc.Save(ms);
                        ms.Position = 0;
                        return new Bitmap(ms);
                    }
                }
            } catch { }
            return null;
        }

        public static BitmapSource? GetFileThumbnailSource(string filePath, bool isLarge = true) {
            try {
                string resolvedPath = filePath;
                if (filePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) {
                    string? target = ResolveShortcut(filePath);
                    if (!string.IsNullOrEmpty(target) && (File.Exists(target) || Directory.Exists(target))) {
                        resolvedPath = target;
                    }
                }

                string ext = Path.GetExtension(resolvedPath);
                if (ImageExtensions.Contains(ext) && File.Exists(resolvedPath)) {
                    long lwt = 0;
                    try { lwt = File.GetLastWriteTimeUtc(resolvedPath).Ticks; } catch { }
                    string cacheKey = (isLarge ? "L:" : "S:") + resolvedPath;
                    if (lwt > 0 && _thumbnailSourceCache.TryGetValue(cacheKey, out var cachedThumb) && cachedThumb.LastWriteTime == lwt) {
                        return cachedThumb.Source;
                    }

                    if (ext.Equals(".svg", StringComparison.OrdinalIgnoreCase)) {
                        int targetDim = isLarge ? 96 : 48;
                        var svgThumb = GetShellThumbnailSource(resolvedPath, targetDim);
                        if (svgThumb != null) {
                            if (lwt > 0) {
                                _thumbnailSourceCache[cacheKey] = (lwt, svgThumb);
                            }
                            return svgThumb;
                        }
                    } else {
                        try {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.UriSource = new Uri(resolvedPath);
                            bmp.DecodePixelWidth = isLarge ? 96 : 48;
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                            bmp.EndInit();
                            bmp.Freeze();
                            if (lwt > 0) {
                                _thumbnailSourceCache[cacheKey] = (lwt, bmp);
                            }
                            return bmp;
                        } catch {
                            // Fallback to file icon
                        }
                    }
                }
            } catch { }

            return GetFileIconSource(filePath, isLarge);
        }

        public static Bitmap? GetFileThumbnailBitmap(string filePath, bool isLarge = true) {
            try {
                string resolvedPath = filePath;
                if (filePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) {
                    string? target = ResolveShortcut(filePath);
                    if (!string.IsNullOrEmpty(target) && (File.Exists(target) || Directory.Exists(target))) {
                        resolvedPath = target;
                    }
                }

                string ext = Path.GetExtension(resolvedPath);
                if (ImageExtensions.Contains(ext) && File.Exists(resolvedPath)) {
                    if (ext.Equals(".svg", StringComparison.OrdinalIgnoreCase)) {
                        int targetDim = isLarge ? 64 : 32;
                        var svgBmp = GetShellThumbnailBitmap(resolvedPath, targetDim);
                        if (svgBmp != null) {
                            return svgBmp;
                        }
                    } else {
                        try {
                            using var fs = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            using var orig = new Bitmap(fs);
                            int targetDim = isLarge ? 64 : 32;
                            return new Bitmap(orig, new System.Drawing.Size(targetDim, targetDim));
                        } catch { }
                    }
                }
            } catch { }

            return GetFileIconBitmap(filePath, isLarge);
        }

        public static BitmapSource? GetFolderIconSource(bool isLarge = true) {
            try {
                if (isLarge && _cachedFolderIconLarge != null) return _cachedFolderIconLarge;
                if (!isLarge && _cachedFolderIconSmall != null) return _cachedFolderIconSmall;

                string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                if (!string.IsNullOrEmpty(sysDir) && Directory.Exists(sysDir)) {
                    var src = GetFileIconSource(sysDir, isLarge);
                    if (src != null) return src;
                }

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrEmpty(baseDir) && Directory.Exists(baseDir)) {
                    var src = GetFileIconSource(baseDir, isLarge);
                    if (src != null) return src;
                }
            } catch { }

            return null;
        }

        public static BitmapSource? GetFastPreviewIconSource(string filePath, bool isDir, string ext, long lastWriteUtcTicks, bool isLarge, out bool needsAsyncUpgrade) {
            needsAsyncUpgrade = false;
            try {
                if (isDir) {
                    return GetFolderIconSource(isLarge);
                }

                string cacheKey = (isLarge ? "L:" : "S:") + filePath;
                if (ImageExtensions.Contains(ext)) {
                    if (lastWriteUtcTicks > 0 && _thumbnailSourceCache.TryGetValue(cacheKey, out var cachedThumb) && cachedThumb.LastWriteTime == lastWriteUtcTicks) {
                        return cachedThumb.Source;
                    }
                    needsAsyncUpgrade = true;
                    return GetFileIconSource("sample" + ext, isLarge);
                }

                bool isSpecificFile = ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                                      ext.Equals(".ico", StringComparison.OrdinalIgnoreCase) ||
                                      ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase);

                if (isSpecificFile) {
                    if (lastWriteUtcTicks > 0 && _fileIconSourceCache.TryGetValue(cacheKey, out var cachedIcon) && cachedIcon.LastWriteTime == lastWriteUtcTicks) {
                        return cachedIcon.Source;
                    }
                    needsAsyncUpgrade = true;
                    return GetFileIconSource("sample.exe", isLarge);
                }

                return GetFileIconSource(string.IsNullOrEmpty(ext) ? filePath : ("sample" + ext), isLarge);
            } catch {
                return null;
            }
        }

        public static BitmapSource? GetFileIconSource(string filePath, bool isLarge = true) {
            try {
                bool isDir = Directory.Exists(filePath);
                if (isDir) {
                    if (isLarge && _cachedFolderIconLarge != null) return _cachedFolderIconLarge;
                    if (!isLarge && _cachedFolderIconSmall != null) return _cachedFolderIconSmall;
                }

                string ext = Path.GetExtension(filePath);
                bool isSpecificFile = ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                                      ext.Equals(".ico", StringComparison.OrdinalIgnoreCase) ||
                                      ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase);

                string extCacheKey = (isLarge ? "L:" : "S:") + (isDir ? ":folder:" : ext);
                if (!isDir && !isSpecificFile && !string.IsNullOrEmpty(ext)) {
                    if (_extensionIconSourceCache.TryGetValue(extCacheKey, out var cachedSource)) {
                        return cachedSource;
                    }
                } else if (!isDir && isSpecificFile && File.Exists(filePath)) {
                    long lwt = 0;
                    try { lwt = File.GetLastWriteTimeUtc(filePath).Ticks; } catch { }
                    string fileKey = (isLarge ? "L:" : "S:") + filePath;
                    if (lwt > 0 && _fileIconSourceCache.TryGetValue(fileKey, out var cachedEntry) && cachedEntry.LastWriteTime == lwt) {
                        return cachedEntry.Source;
                    }
                }

                BitmapSource? result = null;
                SHFILEINFO shinfo = new SHFILEINFO();
                uint flags = SHGFI_ICON | (isLarge ? SHGFI_LARGEICON : SHGFI_SMALLICON);

                uint dwAttr = 0;
                if (!isDir && (!isSpecificFile || !File.Exists(filePath))) {
                    flags |= SHGFI_USEFILEATTRIBUTES;
                    dwAttr = 0x00000080u; // FILE_ATTRIBUTE_NORMAL (in-memory lookup by extension without disk I/O)
                } else if (!Directory.Exists(filePath) && isDir) {
                    flags |= SHGFI_USEFILEATTRIBUTES;
                    dwAttr = 0x00000010u; // FILE_ATTRIBUTE_DIRECTORY
                }

                IntPtr hImg = SHGetFileInfo(filePath, dwAttr, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
                if (shinfo.hIcon != IntPtr.Zero) {
                    try {
                        using (Icon icon = Icon.FromHandle(shinfo.hIcon)) {
                            using Bitmap bitmap = icon.ToBitmap();
                            result = CreateFrozenBitmapSourceFast(bitmap);
                        }
                    } finally {
                        DestroyIcon(shinfo.hIcon);
                    }
                }

                if (result == null && File.Exists(filePath)) {
                    using (Icon? assoc = Icon.ExtractAssociatedIcon(filePath)) {
                        if (assoc != null) {
                            using Bitmap bmp = assoc.ToBitmap();
                            result = CreateFrozenBitmapSourceFast(bmp);
                        }
                    }
                }

                if (result != null) {
                    if (isDir) {
                        if (isLarge) _cachedFolderIconLarge = result;
                        else _cachedFolderIconSmall = result;
                    } else if (!isSpecificFile && !string.IsNullOrEmpty(ext)) {
                        _extensionIconSourceCache[extCacheKey] = result;
                    } else if (isSpecificFile && File.Exists(filePath)) {
                        long lwt = 0;
                        try { lwt = File.GetLastWriteTimeUtc(filePath).Ticks; } catch { }
                        if (lwt > 0) {
                            string fileKey = (isLarge ? "L:" : "S:") + filePath;
                            _fileIconSourceCache[fileKey] = (lwt, result);
                        }
                    }
                }

                return result;
            } catch {
                return null;
            }
        }

        public static Bitmap? GetFileIconBitmap(string filePath, bool isLarge = true) {
            try {
                string ext = Path.GetExtension(filePath);
                bool isDir = Directory.Exists(filePath);
                string key = (isLarge ? "L:" : "S:") + (isDir ? ":folder:" : ext);
                if (!isDir && !ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(ext)) {
                    if (_extensionIconBitmapCache.TryGetValue(key, out var cachedBmp)) {
                        return (Bitmap)cachedBmp.Clone();
                    }
                }

                SHFILEINFO shinfo = new SHFILEINFO();
                uint flags = SHGFI_ICON | (isLarge ? SHGFI_LARGEICON : SHGFI_SMALLICON);

                if (!File.Exists(filePath) && !isDir) {
                    flags |= SHGFI_USEFILEATTRIBUTES;
                }

                IntPtr hImg = SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
                if (shinfo.hIcon != IntPtr.Zero) {
                    try {
                        using (Icon icon = Icon.FromHandle(shinfo.hIcon)) {
                            Bitmap res = new Bitmap(icon.ToBitmap());
                            if (!isDir && !ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(ext)) {
                                _extensionIconBitmapCache[key] = (Bitmap)res.Clone();
                            }
                            return res;
                        }
                    } finally {
                        DestroyIcon(shinfo.hIcon);
                    }
                }

                if (File.Exists(filePath)) {
                    using (Icon? assoc = Icon.ExtractAssociatedIcon(filePath)) {
                        if (assoc != null) {
                            Bitmap res = new Bitmap(assoc.ToBitmap());
                            if (!isDir && !ext.Equals(".exe", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(ext)) {
                                _extensionIconBitmapCache[key] = (Bitmap)res.Clone();
                            }
                            return res;
                        }
                    }
                }
            } catch {
                // Fallback
            }
            return null;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct AccentPolicy {
            public int AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WindowCompositionAttributeData {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [DllImport("user32.dll")]
        public static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [StructLayout(LayoutKind.Sequential)]
        public struct DWM_BLURBEHIND {
            public uint dwFlags;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fEnable;
            public IntPtr hRgnBlur;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fTransitionOnMaximized;
        }

        public const uint DWM_BB_ENABLE = 0x00000001;

        [DllImport("dwmapi.dll")]
        public static extern int DwmEnableBlurBehindWindow(IntPtr hWnd, ref DWM_BLURBEHIND pBlurBehind);

        public const uint WDA_NONE = 0x00000000;
        public const uint WDA_MONITOR = 0x00000001;
        public const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        [DllImport("user32.dll")]
        public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFOEX {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        public static List<Rect> GetAllMonitorsBounds() {
            var monitors = new List<Rect>();
            try {
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data) => {
                    var mi = new MONITORINFOEX();
                    mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                    if (GetMonitorInfo(hMon, ref mi)) {
                        monitors.Add(new Rect(
                            mi.rcMonitor.Left,
                            mi.rcMonitor.Top,
                            Math.Max(1, mi.rcMonitor.Right - mi.rcMonitor.Left),
                            Math.Max(1, mi.rcMonitor.Bottom - mi.rcMonitor.Top)
                        ));
                    } else {
                        monitors.Add(new Rect(
                            rc.Left,
                            rc.Top,
                            Math.Max(1, rc.Right - rc.Left),
                            Math.Max(1, rc.Bottom - rc.Top)
                        ));
                    }
                    return true;
                }, IntPtr.Zero);
            } catch { }

            if (monitors.Count == 0) {
                monitors.Add(new Rect(
                    SystemParameters.VirtualScreenLeft,
                    SystemParameters.VirtualScreenTop,
                    SystemParameters.VirtualScreenWidth,
                    SystemParameters.VirtualScreenHeight
                ));
            }
            return monitors;
        }

        public static Color GetWindowsAccentColor() {
            try {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
                if (key != null) {
                    object? val = key.GetValue("ColorizationColor");
                    if (val is int intVal) {
                        uint colorVal = unchecked((uint)intVal);
                        byte r = (byte)((colorVal >> 16) & 0xFF);
                        byte g = (byte)((colorVal >> 8) & 0xFF);
                        byte b = (byte)(colorVal & 0xFF);
                        return Color.FromArgb(255, r, g, b);
                    }
                    val = key.GetValue("AccentColor");
                    if (val is int accentVal) {
                        uint colorVal = unchecked((uint)accentVal);
                        byte r = (byte)(colorVal & 0xFF);
                        byte g = (byte)((colorVal >> 8) & 0xFF);
                        byte b = (byte)((colorVal >> 16) & 0xFF);
                        return Color.FromArgb(255, r, g, b);
                    }
                }
            } catch {
                // Fallback
            }
            return Color.FromArgb(255, 0, 120, 212); // Windows Default Blue #0078D4
        }

        public const string DefaultPastelColorHex = "#FCDDC5";

        public static string GetWindowsAccentColorHex() {
            var c = GetWindowsAccentColor();
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        public static Color ColorFromHsl(double hue, double s, double l) {
            hue = (hue % 360.0 + 360.0) % 360.0;
            s = Math.Clamp(s, 0.0, 1.0);
            l = Math.Clamp(l, 0.0, 1.0);

            double q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
            double p = 2.0 * l - q;

            double r = HueToRgb(p, q, hue / 360.0 + 1.0 / 3.0);
            double g = HueToRgb(p, q, hue / 360.0);
            double b = HueToRgb(p, q, hue / 360.0 - 1.0 / 3.0);

            return Color.FromArgb(
                255,
                (int)Math.Clamp(Math.Round(r * 255.0), 0, 255),
                (int)Math.Clamp(Math.Round(g * 255.0), 0, 255),
                (int)Math.Clamp(Math.Round(b * 255.0), 0, 255));
        }

        public static string ColorFromHslHex(double hue, double s, double l) {
            var c = ColorFromHsl(hue, s, l);
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        public static (string surfaceHex, string containerHex, string primaryHex) GetMaterialYouTonalPaletteHex(double hue) {
            double diff = Math.Abs(hue - 26.2);
            if (diff > 180) diff = 360 - diff;
            if (diff < 4.0) {
                return ("#FEF4EB", "#FCDDC5", "#7A5427");
            }

            string surface = ColorFromHslHex(hue + 2.2, 0.905, 0.959);
            string container = ColorFromHslHex(hue, 0.90, 0.88);
            string primary = ColorFromHslHex(hue + 6.3, 0.516, 0.316);
            return (surface, container, primary);
        }

        public static (Color surface, Color container, Color primary) GetMaterialYouTonalPalette(double hue) {
            double diff = Math.Abs(hue - 26.2);
            if (diff > 180) diff = 360 - diff;
            if (diff < 4.0) {
                return (
                    Color.FromArgb(255, 0xFE, 0xF4, 0xEB),
                    Color.FromArgb(255, 0xFC, 0xDD, 0xC5),
                    Color.FromArgb(255, 0x7A, 0x54, 0x27)
                );
            }

            var surface = ColorFromHsl(hue + 2.2, 0.905, 0.959);
            var container = ColorFromHsl(hue, 0.90, 0.88);
            var primary = ColorFromHsl(hue + 6.3, 0.516, 0.316);
            return (surface, container, primary);
        }

        public static (Color container, Color triangle, Color cloud, Color shield, Color ellipse) GetLogoDynamicColors(double hue, bool isDark) {
            hue = (hue % 360.0 + 360.0) % 360.0;
            if (isDark) {
                var container = ColorFromHsl(hue, 0.28, 0.24);
                var triangle = ColorFromHsl(hue, 0.75, 0.82);
                var cloud = ColorFromHsl(hue, 0.55, 0.68);
                var shield = ColorFromHsl((hue + 18.0) % 360.0, 0.65, 0.65);
                var ellipse = ColorFromHsl((hue + 349.0) % 360.0, 0.65, 0.65);
                return (container, triangle, cloud, shield, ellipse);
            } else {
                var container = ColorFromHsl(hue, 0.50, 0.80);
                var triangle = ColorFromHsl(hue, 0.75, 0.16);
                var cloud = ColorFromHsl(hue, 0.48, 0.36);
                var shield = ColorFromHsl((hue + 18.0) % 360.0, 0.61, 0.51);
                var ellipse = ColorFromHsl((hue + 349.0) % 360.0, 0.60, 0.51);
                return (container, triangle, cloud, shield, ellipse);
            }
        }

        private static System.Windows.Media.Geometry? s_uninstallerContainerGeom;
        private static System.Windows.Media.Geometry? s_uninstallerBinGeom;

        public static Bitmap RenderUninstallerLogoBitmap(int size, double hue, bool isDark) {
            Bitmap? result = null;
            void CoreRender() {
                Color colContainer;
                Color colCloud;
                if (isDark) {
                    colContainer = ColorFromHsl(hue, 0.25, 0.20);
                    colCloud = ColorFromHsl(hue, 0.75, 0.78);
                } else {
                    var (cContainer, _, cCloud, _, _) = GetLogoDynamicColors(hue, false);
                    colContainer = cContainer;
                    colCloud = cCloud;
                }

                if (s_uninstallerContainerGeom == null) {
                    var g1 = System.Windows.Media.Geometry.Parse(
                        "M59.4086 1.20571C76.2803 -3.9745 92.0771 11.8223 86.8968 28.694L84.1639 37.595C82.8719 41.8029 82.8719 46.3016 84.1639 50.5095L86.8968 59.4105C92.0771 76.2822 76.2803 92.079 59.4086 86.8988L50.5076 84.1659C46.2997 82.8739 41.8009 82.8739 37.593 84.1659L28.6921 86.8988C11.8203 92.079 -3.97645 76.2822 1.20376 59.4105L3.93668 50.5095C5.22865 46.3016 5.22865 41.8029 3.93668 37.595L1.20376 28.694C-3.97645 11.8223 11.8203 -3.9745 28.692 1.20571L37.593 3.93863C41.8009 5.23061 46.2997 5.23061 50.5076 3.93863L59.4086 1.20571Z");
                    g1.Freeze();
                    s_uninstallerContainerGeom = g1;
                }
                if (s_uninstallerBinGeom == null) {
                    var g2 = System.Windows.Media.Geometry.Parse(
                        "M35.6213 72C34.1618 72 32.9141 70.9497 32.6652 69.5116L26.6078 34.5116C26.2903 32.6775 27.7024 31 29.5638 31L59.5221 31C61.3543 31 62.7585 32.6279 62.4897 34.4402L57.2976 69.4402C57.0794 70.911 55.8169 72 54.3301 72L35.6213 72Z");
                    g2.Freeze();
                    s_uninstallerBinGeom = g2;
                }

                var dv = new System.Windows.Media.DrawingVisual();
                using (var dc = dv.RenderOpen()) {
                    double scale = size / 88.0;
                    dc.PushTransform(new System.Windows.Media.ScaleTransform(scale, scale));

                    System.Windows.Media.Color toWpf(Color c) => System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);
                    var brushContainer = new System.Windows.Media.SolidColorBrush(toWpf(colContainer));
                    var brushCloud = new System.Windows.Media.SolidColorBrush(toWpf(colCloud));

                    // 1. Scalloped squircle container badge base
                    dc.DrawGeometry(brushContainer, null, s_uninstallerContainerGeom);

                    // 2. Trash can container bin
                    dc.DrawGeometry(brushCloud, null, s_uninstallerBinGeom);

                    // 3. Trash can lid
                    dc.DrawRoundedRectangle(brushCloud, null, new System.Windows.Rect(26, 18, 37, 9), 3, 3);

                    dc.Pop();
                }

                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(dv);
                var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                var bmpData = bmp.LockBits(new Rectangle(0, 0, size, size), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                try {
                    rtb.CopyPixels(System.Windows.Int32Rect.Empty, bmpData.Scan0, bmpData.Stride * size, bmpData.Stride);
                } finally {
                    bmp.UnlockBits(bmpData);
                }
                result = bmp;
            }

            if (System.Threading.Thread.CurrentThread.GetApartmentState() == System.Threading.ApartmentState.STA) {
                CoreRender();
            } else {
                var t = new System.Threading.Thread(CoreRender);
                t.SetApartmentState(System.Threading.ApartmentState.STA);
                t.Start();
                t.Join();
            }

            return result!;
        }

        public static Color ColorFromPastelHue(double hue) {
            return ColorFromHsl(hue, 0.90, 0.88);
        }

        private static double HueToRgb(double p, double q, double t) {
            if (t < 0) t += 1.0;
            if (t > 1) t -= 1.0;
            if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
            return p;
        }

        public static string ColorFromPastelHueHex(double hue) {
            var c = ColorFromPastelHue(hue);
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        public static double GetHueFromColor(Color color) {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            if (delta < 0.0001) return 26.2; // Default to #FCDDC5 peach hue if grayscale

            double hue;
            if (Math.Abs(max - r) < 0.0001) {
                hue = 60.0 * (((g - b) / delta) % 6);
            } else if (Math.Abs(max - g) < 0.0001) {
                hue = 60.0 * (((b - r) / delta) + 2);
            } else {
                hue = 60.0 * (((r - g) / delta) + 4);
            }

            if (hue < 0) hue += 360.0;
            return Math.Clamp(hue, 0.0, 360.0);
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, double> _hexToHueCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Windows.Media.Color> _wpfColorCache = new(StringComparer.OrdinalIgnoreCase);

        public static bool TryParseHexRgb(string hexColor, out byte a, out byte r, out byte g, out byte b) {
            a = 255; r = 0; g = 0; b = 0;
            if (string.IsNullOrWhiteSpace(hexColor)) return false;
            ReadOnlySpan<char> span = hexColor.AsSpan().Trim();
            if (span.Length > 0 && span[0] == '#') span = span.Slice(1);
            if (span.Length == 6) {
                if (byte.TryParse(span.Slice(0, 2), System.Globalization.NumberStyles.HexNumber, null, out r) &&
                    byte.TryParse(span.Slice(2, 2), System.Globalization.NumberStyles.HexNumber, null, out g) &&
                    byte.TryParse(span.Slice(4, 2), System.Globalization.NumberStyles.HexNumber, null, out b)) {
                    return true;
                }
            } else if (span.Length == 8) {
                if (byte.TryParse(span.Slice(0, 2), System.Globalization.NumberStyles.HexNumber, null, out a) &&
                    byte.TryParse(span.Slice(2, 2), System.Globalization.NumberStyles.HexNumber, null, out r) &&
                    byte.TryParse(span.Slice(4, 2), System.Globalization.NumberStyles.HexNumber, null, out g) &&
                    byte.TryParse(span.Slice(6, 2), System.Globalization.NumberStyles.HexNumber, null, out b)) {
                    return true;
                }
            }
            return false;
        }

        public static System.Windows.Media.Color ParseWpfColorFast(string hexColor, System.Windows.Media.Color fallback = default) {
            if (string.IsNullOrWhiteSpace(hexColor)) return fallback;
            if (_wpfColorCache.TryGetValue(hexColor, out var cached)) return cached;
            if (TryParseHexRgb(hexColor, out byte a, out byte r, out byte g, out byte b)) {
                var col = System.Windows.Media.Color.FromArgb(a, r, g, b);
                if (_wpfColorCache.Count < 2048) _wpfColorCache[hexColor] = col;
                return col;
            }
            try {
                var col = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
                if (_wpfColorCache.Count < 2048) _wpfColorCache[hexColor] = col;
                return col;
            } catch {
                return fallback;
            }
        }

        public static void SetResourceBrushColor(System.Windows.ResourceDictionary resources, string key, System.Windows.Media.Color color) {
            if (resources.Contains(key) && resources[key] is System.Windows.Media.SolidColorBrush existing && !existing.IsFrozen) {
                existing.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, null);
                if (existing.Color != color) {
                    existing.Color = color;
                }
            } else {
                resources[key] = new System.Windows.Media.SolidColorBrush(color);
            }
        }

        public static void AnimateResourceBrushColor(System.Windows.ResourceDictionary resources, string key, System.Windows.Media.Color color, TimeSpan duration) {
            if (resources.Contains(key) && resources[key] is System.Windows.Media.SolidColorBrush existing && !existing.IsFrozen) {
                if (existing.Color != color) {
                    var anim = new System.Windows.Media.Animation.ColorAnimation {
                        To = color,
                        Duration = duration,
                        EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                    };
                    existing.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, anim);
                }
            } else {
                resources[key] = new System.Windows.Media.SolidColorBrush(color);
            }
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<System.Windows.Media.SolidColorBrush, object> _ownedMutableBrushes = new();
        private static readonly object _ownedBrushSentinel = new();

        public static System.Windows.Media.SolidColorBrush UpdateOrCreateBrush(System.Windows.Media.Brush? current, System.Windows.Media.Color color) {
            if (current is System.Windows.Media.SolidColorBrush existing && !existing.IsFrozen && _ownedMutableBrushes.TryGetValue(existing, out _)) {
                existing.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, null);
                if (existing.Color != color) {
                    existing.Color = color;
                }
                return existing;
            }
            var created = new System.Windows.Media.SolidColorBrush(color);
            _ownedMutableBrushes.Add(created, _ownedBrushSentinel);
            return created;
        }

        public static System.Windows.Media.SolidColorBrush AnimateOrCreateBrush(System.Windows.Media.Brush? current, System.Windows.Media.Color color, TimeSpan duration) {
            if (current is System.Windows.Media.SolidColorBrush existing && !existing.IsFrozen && _ownedMutableBrushes.TryGetValue(existing, out _)) {
                if (existing.Color != color) {
                    var anim = new System.Windows.Media.Animation.ColorAnimation {
                        To = color,
                        Duration = duration,
                        EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                    };
                    existing.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, anim);
                }
                return existing;
            }
            var created = new System.Windows.Media.SolidColorBrush(color);
            _ownedMutableBrushes.Add(created, _ownedBrushSentinel);
            return created;
        }

        public static double GetHueFromHex(string hexColor) {
            try {
                if (string.IsNullOrWhiteSpace(hexColor)) {
                    return 26.2;
                }
                if (_hexToHueCache.TryGetValue(hexColor, out double cachedHue)) {
                    return cachedHue;
                }
                double hue;
                if (TryParseHexRgb(hexColor, out _, out byte r, out byte g, out byte b)) {
                    hue = GetHueFromColor(Color.FromArgb(255, r, g, b));
                } else {
                    var c = ColorTranslator.FromHtml(hexColor);
                    hue = GetHueFromColor(c);
                }
                if (_hexToHueCache.Count < 2048) {
                    _hexToHueCache[hexColor] = hue;
                }
                return hue;
            } catch {
                return 26.2;
            }
        }

        public static string GetWindowsAccentPastelHex() {
            try {
                var c = GetWindowsAccentColor();
                double r = c.R / 255.0;
                double g = c.G / 255.0;
                double b = c.B / 255.0;
                double max = Math.Max(r, Math.Max(g, b));
                double min = Math.Min(r, Math.Min(g, b));
                if (max - min < 0.05) {
                    return DefaultPastelColorHex;
                }
                double hue = GetHueFromColor(c);
                return ColorFromPastelHueHex(hue);
            } catch {
                return DefaultPastelColorHex;
            }
        }

        public static string GetDefaultPastelColorHex() {
            return DefaultPastelColorHex;
        }

        public static BitmapSource CreateFrozenBitmapSourceFast(System.Drawing.Bitmap bmp, bool ignoreAlpha = false) {
            var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
            var lockFormat = ignoreAlpha ? System.Drawing.Imaging.PixelFormat.Format32bppRgb : System.Drawing.Imaging.PixelFormat.Format32bppArgb;
            var wpfFormat = ignoreAlpha ? System.Windows.Media.PixelFormats.Bgr32 : System.Windows.Media.PixelFormats.Bgra32;
            var bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, lockFormat);
            try {
                var src = BitmapSource.Create(
                    bmp.Width,
                    bmp.Height,
                    96,
                    96,
                    wpfFormat,
                    null,
                    bmpData.Scan0,
                    bmpData.Stride * bmp.Height,
                    bmpData.Stride);
                src.Freeze();
                return src;
            } finally {
                bmp.UnlockBits(bmpData);
            }
        }

        public static BitmapSource? CaptureScreenArea(int x, int y, int width, int height, double dpiScale = 1.0, bool downsampleForBlur = false) {
            try {
                if (width <= 0 || height <= 0) {
                    return null;
                }
                int physWidth = (int)Math.Max(1, Math.Round(width * dpiScale));
                int physHeight = (int)Math.Max(1, Math.Round(height * dpiScale));
                int physX = (int)Math.Round(x * dpiScale);
                int physY = (int)Math.Round(y * dpiScale);

                // Downsample 4x in each dimension (16x fewer pixels) when used as a blurred backdrop
                int bmpW = downsampleForBlur && physWidth > 160 ? Math.Max(64, physWidth / 4) : physWidth;
                int bmpH = downsampleForBlur && physHeight > 160 ? Math.Max(64, physHeight / 4) : physHeight;

                using var bmp = new System.Drawing.Bitmap(bmpW, bmpH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (var g = System.Drawing.Graphics.FromImage(bmp)) {
                    if (downsampleForBlur && (bmpW != physWidth || bmpH != physHeight)) {
                        IntPtr hdcScreen = GetDC(IntPtr.Zero);
                        if (hdcScreen != IntPtr.Zero) {
                            IntPtr hdcBmp = g.GetHdc();
                            try {
                                SetStretchBltMode(hdcBmp, 3); // COLORONCOLOR
                                bool ok = StretchBlt(hdcBmp, 0, 0, bmpW, bmpH, hdcScreen, physX, physY, physWidth, physHeight, 0x40CC0020 /* SRCCOPY | CAPTUREBLT */);
                                if (!ok) {
                                    g.CopyFromScreen(physX, physY, 0, 0, new System.Drawing.Size(physWidth, physHeight),
                                        System.Drawing.CopyPixelOperation.SourceCopy | System.Drawing.CopyPixelOperation.CaptureBlt);
                                }
                            } finally {
                                g.ReleaseHdc(hdcBmp);
                                ReleaseDC(IntPtr.Zero, hdcScreen);
                            }
                        } else {
                            g.CopyFromScreen(physX, physY, 0, 0, new System.Drawing.Size(physWidth, physHeight),
                                System.Drawing.CopyPixelOperation.SourceCopy | System.Drawing.CopyPixelOperation.CaptureBlt);
                        }
                    } else {
                        g.CopyFromScreen(physX, physY, 0, 0, new System.Drawing.Size(physWidth, physHeight),
                            System.Drawing.CopyPixelOperation.SourceCopy | System.Drawing.CopyPixelOperation.CaptureBlt);
                    }
                }

                // If on legacy Windows pre-19041 and bitmap capture completely failed (all black), fall back to wallpaper
                if (!IsWindows10Build19041OrGreater && IsBitmapTooDarkOrBlack(bmp)) {
                    var wpFallback = CropWallpaperArea(x, y, width, height);
                    if (wpFallback != null) return wpFallback;
                }

                return CreateFrozenBitmapSourceFast(bmp, ignoreAlpha: true);
            } catch {
                return CropWallpaperArea(x, y, width, height);
            }
        }

        public static bool IsWindows11OrGreater => Environment.OSVersion.Version.Build >= 22000;
        public static bool IsWindows10OrGreater => Environment.OSVersion.Version.Major >= 10;
        public static bool IsWindows10Build19041OrGreater => Environment.OSVersion.Version.Build >= 19041;

        public static void OptimizeWindowForOs(IntPtr hwnd, bool isDark = true, bool roundCorners = true) {
            if (hwnd == IntPtr.Zero) return;
            try {
                if (IsWindows10Build19041OrGreater) {
                    int darkVal = isDark ? 1 : 0;
                    DwmSetWindowAttribute(hwnd, 20, ref darkVal, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
                }
                if (IsWindows11OrGreater) {
                    if (roundCorners) {
                        int cornerPref = 2; // DWMWCP_ROUND
                        DwmSetWindowAttribute(hwnd, 33, ref cornerPref, sizeof(int)); // DWMWA_WINDOW_CORNER_PREFERENCE
                    } else {
                        int cornerPref = 1; // DWMWCP_DONOTROUND
                        DwmSetWindowAttribute(hwnd, 33, ref cornerPref, sizeof(int));
                    }
                    // Suppress DWM system border / outline entirely (DWMWA_BORDER_COLOR = 34, DWMWA_COLOR_NONE = 0xFFFFFFFE)
                    int colorNone = unchecked((int)0xFFFFFFFE);
                    DwmSetWindowAttribute(hwnd, 34, ref colorNone, sizeof(int));
                }
            } catch { }
        }

        public static void OptimizeWindowForOs(System.Windows.Window window, bool isDark = true, bool roundCorners = true) {
            if (window == null) return;
            try {
                System.Windows.Media.RenderOptions.SetBitmapScalingMode(window, System.Windows.Media.BitmapScalingMode.LowQuality);
                var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                if (hwnd != IntPtr.Zero) {
                    OptimizeWindowForOs(hwnd, isDark, roundCorners);
                }
            } catch { }
        }

        public static bool IsBitmapTooDarkOrBlack(System.Drawing.Bitmap bmp) {
            try {
                int w = bmp.Width;
                int h = bmp.Height;
                if (w < 2 || h < 2) return true;
                var c1 = bmp.GetPixel(w / 2, h / 2);
                var c2 = bmp.GetPixel(w / 4, h / 4);
                var c3 = bmp.GetPixel(Math.Min(w - 1, (3 * w) / 4), Math.Min(h - 1, (3 * h) / 4));
                double avg = (c1.R + c1.G + c1.B + c2.R + c2.G + c2.B + c3.R + c3.G + c3.B) / 9.0;
                return avg < 5.0;
            } catch {
                return false;
            }
        }

        private static BitmapSource? _lastCropSource;
        private static System.Windows.Int32Rect _lastCropRect;
        private static BitmapSource? _lastCroppedResult;

        public static BitmapSource? CropWallpaperArea(int x, int y, int width, int height) {
            try {
                var wp = GetDesktopWallpaperBitmap();
                if (wp == null) return null;
                double screenW = System.Windows.SystemParameters.PrimaryScreenWidth;
                double screenH = System.Windows.SystemParameters.PrimaryScreenHeight;
                if (screenW <= 0 || screenH <= 0 || width <= 0 || height <= 0) return wp;

                double scaleX = wp.PixelWidth / screenW;
                double scaleY = wp.PixelHeight / screenH;

                int cropX = Math.Clamp((int)(x * scaleX), 0, Math.Max(0, wp.PixelWidth - 1));
                int cropY = Math.Clamp((int)(y * scaleY), 0, Math.Max(0, wp.PixelHeight - 1));
                int cropW = Math.Clamp((int)(width * scaleX), 1, wp.PixelWidth - cropX);
                int cropH = Math.Clamp((int)(height * scaleY), 1, wp.PixelHeight - cropY);

                var rect = new System.Windows.Int32Rect(cropX, cropY, cropW, cropH);
                if (ReferenceEquals(_lastCropSource, wp) && _lastCropRect == rect && _lastCroppedResult != null) {
                    return _lastCroppedResult;
                }

                var cropped = new System.Windows.Media.Imaging.CroppedBitmap(wp, rect);
                cropped.Freeze();
                _lastCropSource = wp;
                _lastCropRect = rect;
                _lastCroppedResult = cropped;
                return cropped;
            } catch {
                return null;
            }
        }

        private static IntPtr _cachedWorkerW = IntPtr.Zero;
        private static DateTime _lastWorkerWSearch = DateTime.MinValue;

        public static IntPtr GetWallpaperWorkerW() {
            if (_cachedWorkerW != IntPtr.Zero && (DateTime.UtcNow - _lastWorkerWSearch).TotalSeconds < 10) {
                if (IsWindow(_cachedWorkerW)) return _cachedWorkerW;
            }
            _lastWorkerWSearch = DateTime.UtcNow;
            _cachedWorkerW = IntPtr.Zero;

            try {
                IntPtr progman = FindWindow("Progman", null);
                if (progman != IntPtr.Zero) {
                    IntPtr res;
                    SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), new IntPtr(0x1), 0, 1000, out res);
                    IntPtr foundWorkerW = IntPtr.Zero;
                    EnumWindows((hWnd, lParam) => {
                        IntPtr shellView = FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                        if (shellView != IntPtr.Zero) {
                            foundWorkerW = FindWindowEx(IntPtr.Zero, hWnd, "WorkerW", null);
                        }
                        return true;
                    }, IntPtr.Zero);
                    if (foundWorkerW != IntPtr.Zero) {
                        _cachedWorkerW = foundWorkerW;
                    }
                }
                if (_cachedWorkerW == IntPtr.Zero) {
                    // Fallback: search for top-level WorkerW windows without SHELLDLL_DefView
                    IntPtr w = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "WorkerW", null);
                    while (w != IntPtr.Zero) {
                        IntPtr defView = FindWindowEx(w, IntPtr.Zero, "SHELLDLL_DefView", null);
                        if (defView == IntPtr.Zero) {
                            _cachedWorkerW = w;
                            break;
                        }
                        w = FindWindowEx(IntPtr.Zero, w, "WorkerW", null);
                    }
                }
            } catch { }
            return _cachedWorkerW;
        }

        private static bool _cachedLiveWallpaperRunning = false;
        private static long _lastLiveWallpaperCheckTicks = 0;

        private static bool HasProcessByNameFast(string processName) {
            var procs = System.Diagnostics.Process.GetProcessesByName(processName);
            bool found = procs.Length > 0;
            for (int i = 0; i < procs.Length; i++) {
                try { procs[i].Dispose(); } catch { }
            }
            return found;
        }

        public static bool IsLiveWallpaperEngineRunning() {
            long now = Environment.TickCount64;
            if (_lastLiveWallpaperCheckTicks != 0 && now - _lastLiveWallpaperCheckTicks < 15000) {
                return _cachedLiveWallpaperRunning;
            }
            try {
                _cachedLiveWallpaperRunning = HasProcessByNameFast("wallpaper64")
                    || HasProcessByNameFast("wallpaper32")
                    || HasProcessByNameFast("Lively");
            } catch {
                _cachedLiveWallpaperRunning = false;
            }
            _lastLiveWallpaperCheckTicks = now;
            return _cachedLiveWallpaperRunning;
        }

        public static BitmapSource? CaptureLiveWallpaper() {
            try {
                IntPtr workerW = GetWallpaperWorkerW();
                if (workerW != IntPtr.Zero) {
                    RECT rect;
                    GetWindowRect(workerW, out rect);
                    int w = rect.Right - rect.Left;
                    int h = rect.Bottom - rect.Top;
                    if (w > 100 && h > 100) {
                        using var bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        using (var g = System.Drawing.Graphics.FromImage(bmp)) {
                            IntPtr hdc = g.GetHdc();
                            try {
                                bool success = PrintWindow(workerW, hdc, 0x2); // PW_RENDERFULLCONTENT
                                if (!success) {
                                    IntPtr srcDc = GetDC(workerW);
                                    if (srcDc != IntPtr.Zero) {
                                        BitBlt(hdc, 0, 0, w, h, srcDc, 0, 0, 0x00CC0020);
                                        ReleaseDC(workerW, srcDc);
                                        success = true;
                                    }
                                }
                            } finally {
                                g.ReleaseHdc(hdc);
                            }
                        }
                        return CreateFrozenBitmapSourceFast(bmp);
                    }
                }
            } catch { }
            return null;
        }

        private static BitmapSource? _cachedStaticWallpaper;
        private static string? _cachedStaticWallpaperPath;
        private static DateTime _cachedStaticWallpaperWriteTimeUtc;

        private static BitmapSource? LoadAndCacheStaticWallpaperFile(string path) {
            var fi = new FileInfo(path);
            if (!fi.Exists || fi.Length <= 0) return null;

            DateTime lastWrite = fi.LastWriteTimeUtc;
            if (_cachedStaticWallpaper != null &&
                string.Equals(_cachedStaticWallpaperPath, path, StringComparison.OrdinalIgnoreCase) &&
                _cachedStaticWallpaperWriteTimeUtc == lastWrite) {
                return _cachedStaticWallpaper;
            }

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.DecodePixelWidth = 1280;
            bi.UriSource = new Uri(path, UriKind.Absolute);
            bi.EndInit();
            bi.Freeze();

            _cachedStaticWallpaper = bi;
            _cachedStaticWallpaperPath = path;
            _cachedStaticWallpaperWriteTimeUtc = lastWrite;
            return bi;
        }

        public static BitmapSource? GetDesktopWallpaperBitmap() {
            // 1. Try Live Wallpaper capture if active
            if (IsLiveWallpaperEngineRunning()) {
                var live = CaptureLiveWallpaper();
                if (live != null) return live;
            }

            // 2. Try Windows TranscodedWallpaper file (cached in memory unless file changed)
            try {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string transcoded = Path.Combine(appData, @"Microsoft\Windows\Themes\TranscodedWallpaper");
                var cachedOrLoaded = LoadAndCacheStaticWallpaperFile(transcoded);
                if (cachedOrLoaded != null) {
                    return cachedOrLoaded;
                }
            } catch { }

            // 3. Try Registry Wallpaper path
            try {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
                string? wpPath = key?.GetValue("Wallpaper") as string;
                if (!string.IsNullOrEmpty(wpPath)) {
                    var cachedOrLoaded = LoadAndCacheStaticWallpaperFile(wpPath);
                    if (cachedOrLoaded != null) {
                        return cachedOrLoaded;
                    }
                }
            } catch { }

            // 4. Try Live Wallpaper capture fallback
            return CaptureLiveWallpaper();
        }

        public static void EnableWindowBlur(IntPtr hwnd, uint tintColor = 0x66000000) {
            try {
                DWM_BLURBEHIND bb = new DWM_BLURBEHIND {
                    dwFlags = DWM_BB_ENABLE,
                    fEnable = true,
                    hRgnBlur = IntPtr.Zero,
                    fTransitionOnMaximized = false
                };
                DwmEnableBlurBehindWindow(hwnd, ref bb);

                AccentPolicy policy = new AccentPolicy {
                    AccentState = 4, // ACCENT_ENABLE_ACRYLICBLURBEHIND
                    AccentFlags = 2,
                    GradientColor = unchecked((int)tintColor),
                    AnimationId = 0
                };

                int size = Marshal.SizeOf(policy);
                IntPtr pPolicy = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(policy, pPolicy, false);

                WindowCompositionAttributeData data = new WindowCompositionAttributeData {
                    Attribute = 19, // WCA_ACCENT_POLICY
                    Data = pPolicy,
                    SizeOfData = size
                };

                SetWindowCompositionAttribute(hwnd, ref data);
                Marshal.FreeHGlobal(pPolicy);
            } catch {
                // Fallback
            }
        }

        public static void EnableAcrylicBlur(IntPtr hwnd, uint accentColor = 0xCC1E1E1E) {
            try {
                int backdropType = 3; // DWMSBT_TRANSIENTWINDOW = 3 (Acrylic)
                DwmSetWindowAttribute(hwnd, 38, ref backdropType, sizeof(int));
            } catch {
                // DWM acrylic attribute fallback
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        public static Rect GetMonitorWorkArea(System.Windows.Point screenPointDpi) {
            try {
                var pt = new POINT { X = (int)Math.Round(screenPointDpi.X), Y = (int)Math.Round(screenPointDpi.Y) };
                IntPtr hMon = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
                if (hMon != IntPtr.Zero) {
                    MONITORINFO mi = new MONITORINFO();
                    mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                    if (GetMonitorInfo(hMon, ref mi)) {
                        return new Rect(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Right - mi.rcWork.Left, mi.rcWork.Bottom - mi.rcWork.Top);
                    }
                }
            } catch { }
            return SystemParameters.WorkArea;
        }

        public static Rect GetMonitorWorkArea(IntPtr hwnd) {
            try {
                if (hwnd != IntPtr.Zero) {
                    IntPtr hMon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                    if (hMon != IntPtr.Zero) {
                        MONITORINFO mi = new MONITORINFO();
                        mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                        if (GetMonitorInfo(hMon, ref mi)) {
                            return new Rect(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Right - mi.rcWork.Left, mi.rcWork.Bottom - mi.rcWork.Top);
                        }
                    }
                }
            } catch { }
            return SystemParameters.WorkArea;
        }

        public static Rect GetMonitorBounds(IntPtr hwnd) {
            try {
                if (hwnd != IntPtr.Zero) {
                    IntPtr hMon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                    if (hMon != IntPtr.Zero) {
                        MONITORINFO mi = new MONITORINFO();
                        mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                        if (GetMonitorInfo(hMon, ref mi)) {
                            return new Rect(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right - mi.rcMonitor.Left, mi.rcMonitor.Bottom - mi.rcMonitor.Top);
                        }
                    }
                }
            } catch { }
            return new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
        }

        public static bool IsWindowsDarkTheme() {
            try {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null) {
                    object? sysVal = key.GetValue("SystemUsesLightTheme");
                    if (sysVal is int intSys && intSys == 0) {
                        return true;
                    }
                    object? appVal = key.GetValue("AppsUseLightTheme");
                    if (appVal is int intApp && intApp == 0) {
                        return true;
                    }
                }
            } catch {
                // Fallback
            }
            return false;
        }

        public static void DismissActiveContextMenu() {
            try {
                IntPtr menuHwnd = FindWindow("#32768", null);
                if (menuHwnd != IntPtr.Zero) {
                    SendMessage(menuHwnd, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero);
                }
                keybd_event(0x1B /* VK_ESCAPE */, 0, 0, UIntPtr.Zero);
                keybd_event(0x1B, 0, 0x0002 /* KEYEVENTF_KEYUP */, UIntPtr.Zero);
            } catch { }
        }
    }
}

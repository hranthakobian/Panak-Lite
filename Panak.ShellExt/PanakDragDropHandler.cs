using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Win32;

namespace Panak.ShellExt {
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E8-0000-0000-C000-000000000046")]
    public interface IShellExtInit {
        [PreserveSig]
        int Initialize(IntPtr pidlFolder, IntPtr pDataObj, IntPtr hKeyProgID);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E4-0000-0000-C000-000000000046")]
    public interface IContextMenu {
        [PreserveSig]
        int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

        [PreserveSig]
        int InvokeCommand(IntPtr pici);

        [PreserveSig]
        int GetCommandString(nuint idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax);
    }

    [ComVisible(true)]
    [Guid("E5A7B812-3D4C-4A2E-8910-1F2D3C4E5A6B")]
    [ClassInterface(ClassInterfaceType.None)]
    public class PanakDragDropHandler : IShellExtInit, IContextMenu {
        private const string PIPE_NAME = "PanakIpcNamedPipe_Local";
        private const string CLSID_STRING = "{E5A7B812-3D4C-4A2E-8910-1F2D3C4E5A6B}";
        private const uint CMF_DEFAULTONLY = 0x00000001;

        private const uint MIIM_STATE = 0x00000001;
        private const uint MIIM_ID = 0x00000002;
        private const uint MIIM_STRING = 0x00000040;
        private const uint MIIM_BITMAP = 0x00000080;
        private const uint MFS_ENABLED = 0x00000000;
        private const uint MF_BYPOSITION = 0x00000400;
        private const uint MF_STRING = 0x00000000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MENUITEMINFO {
            public uint cbSize;
            public uint fMask;
            public uint fType;
            public uint fState;
            public uint wID;
            public IntPtr hSubMenu;
            public IntPtr hbmpChecked;
            public IntPtr hbmpUnchecked;
            public UIntPtr dwItemData;
            public string dwTypeData;
            public uint cch;
            public IntPtr hbmpItem;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CMINVOKECOMMANDINFO {
            public uint cbSize;
            public uint fMask;
            public IntPtr hwnd;
            public IntPtr lpVerb;
            public IntPtr lpParameters;
            public IntPtr lpDirectory;
            public int nShow;
            public uint dwHotKey;
            public IntPtr hIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT {
            public int X;
            public int Y;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SHGetPathFromIDListW(IntPtr pidl, StringBuilder pszPath);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint DragQueryFileW(IntPtr hDrop, uint iFile, StringBuilder? lpszFile, uint cch);

        [DllImport("ole32.dll")]
        private static extern void ReleaseStgMedium(ref STGMEDIUM pmedium);

        [DllImport("user32.dll")]
        private static extern int GetMenuItemCount(IntPtr hMenu);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool InsertMenuItemW(IntPtr hMenu, uint uItem, bool fByPosition, ref MENUITEMINFO lpmii);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool InsertMenuW(IntPtr hMenu, uint uPosition, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint ExtractIconExW(string szFileName, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, uint nIcons);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private readonly List<string> _files = new();
        private string _targetDir = string.Empty;
        private IntPtr _hBitmap = IntPtr.Zero;

        public int Initialize(IntPtr pidlFolder, IntPtr pDataObj, IntPtr hKeyProgID) {
            _files.Clear();
            _targetDir = string.Empty;

            try {
                if (pidlFolder != IntPtr.Zero) {
                    StringBuilder sb = new StringBuilder(1024);
                    if (SHGetPathFromIDListW(pidlFolder, sb) && sb.Length > 0) {
                        _targetDir = sb.ToString();
                    }
                }

                if (string.IsNullOrEmpty(_targetDir)) {
                    _targetDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                }

                if (pDataObj != IntPtr.Zero) {
                    var dataObject = (IDataObject)Marshal.GetObjectForIUnknown(pDataObj);
                    FORMATETC formatEtc = new FORMATETC {
                        cfFormat = 15, // CF_HDROP
                        ptd = IntPtr.Zero,
                        dwAspect = DVASPECT.DVASPECT_CONTENT,
                        lindex = -1,
                        tymed = TYMED.TYMED_HGLOBAL
                    };

                    dataObject.GetData(ref formatEtc, out STGMEDIUM medium);
                    if (medium.unionmember != IntPtr.Zero) {
                        try {
                            uint count = DragQueryFileW(medium.unionmember, 0xFFFFFFFF, null, 0);
                            for (uint i = 0; i < count; i++) {
                                StringBuilder sbFile = new StringBuilder(1024);
                                if (DragQueryFileW(medium.unionmember, i, sbFile, (uint)sbFile.Capacity) > 0) {
                                    string path = sbFile.ToString();
                                    if (!string.IsNullOrEmpty(path)) {
                                        _files.Add(path);
                                    }
                                }
                            }
                        } finally {
                            ReleaseStgMedium(ref medium);
                        }
                    }
                }
            } catch {
                // Ignore initialization errors
            }

            return 0; // S_OK
        }

        public int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags) {
            if ((uFlags & CMF_DEFAULTONLY) != 0 || _files.Count == 0) {
                return 0; // S_OK, 0 items added
            }

            try {
                int menuCount = GetMenuItemCount(hmenu);
                uint targetPos = indexMenu;
                if (menuCount >= 0 && targetPos > (uint)menuCount) {
                    targetPos = (uint)menuCount;
                }

                string title = GetLocalizedMenuTitle();
                string menuText = _files.Count > 1 ? $"{title} ({_files.Count})" : title;

                IntPtr hIconBmp = GetPanakIconBitmap();

                bool inserted = false;
                if (hIconBmp != IntPtr.Zero) {
                    MENUITEMINFO mii = new MENUITEMINFO {
                        cbSize = (uint)Marshal.SizeOf<MENUITEMINFO>(),
                        fMask = MIIM_STRING | MIIM_ID | MIIM_STATE | MIIM_BITMAP,
                        fState = MFS_ENABLED,
                        wID = idCmdFirst,
                        dwTypeData = menuText,
                        hbmpItem = hIconBmp
                    };
                    inserted = InsertMenuItemW(hmenu, targetPos, true, ref mii);
                }

                if (!inserted) {
                    MENUITEMINFO miiTextOnly = new MENUITEMINFO {
                        cbSize = (uint)Marshal.SizeOf<MENUITEMINFO>(),
                        fMask = MIIM_STRING | MIIM_ID | MIIM_STATE,
                        fState = MFS_ENABLED,
                        wID = idCmdFirst,
                        dwTypeData = menuText
                    };
                    inserted = InsertMenuItemW(hmenu, targetPos, true, ref miiTextOnly);
                }

                if (!inserted) {
                    inserted = InsertMenuW(hmenu, targetPos, MF_BYPOSITION | MF_STRING, (UIntPtr)idCmdFirst, menuText);
                }

                return inserted ? 1 : 0; // 1 item added (offset 0), or 0 if failed
            } catch {
                return 0;
            }
        }

        public int InvokeCommand(IntPtr pici) {
            try {
                var ici = Marshal.PtrToStructure<CMINVOKECOMMANDINFO>(pici);
                int cmd = (int)(ici.lpVerb.ToInt64() & 0xFFFF);
                if (cmd == 0) {
                    ExecuteCollect();
                    return 0; // S_OK
                }
            } catch { }

            return unchecked((int)0x80004005); // E_FAIL
        }

        public int GetCommandString(nuint idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax) {
            return unchecked((int)0x80004001); // E_NOTIMPL
        }

        private void ExecuteCollect() {
            if (_files.Count == 0) {
                return;
            }

            string target = _targetDir;
            if (string.IsNullOrEmpty(target) || !Directory.Exists(target)) {
                target = Path.GetDirectoryName(_files[0]) ?? string.Empty;
            }
            if (string.IsNullOrEmpty(target) || !Directory.Exists(target)) {
                target = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }

            int cx = 0, cy = 0;
            if (GetCursorPos(out var pt)) {
                cx = pt.X;
                cy = pt.Y;
            }

            string filePayload = string.Join("*", _files);
            string command = $"COLLECT|{target}|{cx}|{cy}|{filePayload}";

            // 1. Try sending directly to background Panak instance via IPC pipe
            if (SendIpcCommand(command)) {
                return;
            }

            // 2. If Panak is not running, launch Panak.exe with temporary collection file
            string exePath = FindPanakExe();
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath)) {
                try {
                    string tempFile = Path.Combine(Path.GetTempPath(), $"panak_drop_{Guid.NewGuid():N}.tmp");
                    File.WriteAllLines(tempFile, _files);
                    ProcessStartInfo psi = new ProcessStartInfo {
                        FileName = exePath,
                        Arguments = $"--collect-file \"{tempFile}\" \"{target}\" {cx} {cy}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    Process.Start(psi);
                } catch { }
            }
        }

        private static bool SendIpcCommand(string command) {
            try {
                using var client = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.Out);
                client.Connect(350); // fast connection check
                byte[] data = Encoding.UTF8.GetBytes(command);
                client.Write(data, 0, data.Length);
                client.Flush();
                return true;
            } catch {
                return false;
            }
        }

        private static string GetLocalizedMenuTitle() {
            try {
                // Check settings.json for selected language
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string settingsPath = Path.Combine(appData, "Panak", "settings.json");
                if (File.Exists(settingsPath)) {
                    string json = File.ReadAllText(settingsPath);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Language", out var langProp)) {
                        string lang = langProp.GetString()?.ToLowerInvariant() ?? "";
                        return lang switch {
                            "hy" => "Հավաքել Panak-ի մեջ",
                            "en" => "Collect into Panak folder",
                            "ru" => "Собрать в папку Panak",
                            "fr" => "Rassembler dans un dossier Panak",
                            "es" => "Reunir en carpeta Panak",
                            "zh" => "收集到 Panak 文件夹",
                            "ar" => "جمع في مجلد Panak",
                            _ => "Հավաքել Panak-ի մեջ"
                        };
                    }
                }
            } catch { }

            // Default to Armenian as configured for Panak
            return "Հավաքել Panak-ի մեջ";
        }

        private IntPtr GetPanakIconBitmap() {
            if (_hBitmap != IntPtr.Zero) {
                return _hBitmap;
            }

            try {
                string exePath = FindPanakExe();
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath)) {
                    if (ExtractIconExW(exePath, 0, out _, out IntPtr hSmallIcon, 1) > 0 && hSmallIcon != IntPtr.Zero) {
                        try {
                            using (var icon = System.Drawing.Icon.FromHandle(hSmallIcon)) {
                                using (var bmp = icon.ToBitmap()) {
                                    _hBitmap = bmp.GetHbitmap();
                                }
                            }
                        } finally {
                            DestroyIcon(hSmallIcon);
                        }
                    }
                }
            } catch { }

            return _hBitmap;
        }

        private static string FindPanakExe() {
            try {
                // 1. Check Uninstall Registry Key
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Panak")) {
                    string? loc = key?.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrEmpty(loc)) {
                        string candidate = Path.Combine(loc, "Panak.exe");
                        if (File.Exists(candidate)) {
                            return candidate;
                        }
                    }
                }

                // 2. Check Standard LocalPrograms directory
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string standardPath = Path.Combine(localAppData, "Programs", "Panak", "Panak.exe");
                if (File.Exists(standardPath)) {
                    return standardPath;
                }

                // 3. Check adjacent folder (same directory as DLL)
                try {
                    string asmLoc = typeof(PanakDragDropHandler).Assembly.Location;
                    if (!string.IsNullOrEmpty(asmLoc)) {
                        string asmDir = Path.GetDirectoryName(asmLoc) ?? string.Empty;
                        string candidate = Path.Combine(asmDir, "Panak.exe");
                        if (File.Exists(candidate)) {
                            return candidate;
                        }
                    }
                } catch { }

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string localExe = Path.Combine(baseDir, "Panak.exe");
                if (File.Exists(localExe)) {
                    return localExe;
                }
            } catch { }

            return string.Empty;
        }
    }
}

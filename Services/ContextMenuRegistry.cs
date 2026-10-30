using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Panak.Services {
    public static class ContextMenuRegistry {
        private const string HKCU_CLASSES = @"Software\Classes";

        public static void RegisterAll(string exePath, string? language = null) {
            try {
                if (string.IsNullOrEmpty(exePath)) {
                    exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                }
                if (exePath.Contains("testhost", StringComparison.OrdinalIgnoreCase)) {
                    return;
                }
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) {
                    return;
                }

                if (!string.IsNullOrEmpty(language)) {
                    LocalizationService.Instance.CurrentLanguage = language;
                } else {
                    try {
                        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        string settingsPath = Path.Combine(appData, "Panak", "settings.json");
                        if (File.Exists(settingsPath)) {
                            string json = File.ReadAllText(settingsPath);
                            using var doc = System.Text.Json.JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("Language", out var langProp) && !string.IsNullOrEmpty(langProp.GetString())) {
                                LocalizationService.Instance.CurrentLanguage = langProp.GetString()!;
                            }
                        }
                    } catch { }
                }

                string createText = LocalizationService.Instance["CollectIntoPanakFolder"];
                string openText = LocalizationService.Instance["OpenInPanak"];
                string convertText = LocalizationService.Instance["ConvertToPanak"];
                string unpackText = LocalizationService.Instance["UnpackPanakFolder"];

                // 1. HKCU\Software\Classes\*\shell\PanakCreate, Directory, Folder, AllFilesystemObjects
                string[] createKeys = new[] { "*", "Directory", "Folder", "AllFilesystemObjects" };
                foreach (var ck in createKeys) {
                    using (var key = Registry.CurrentUser.CreateSubKey($@"{HKCU_CLASSES}\{ck}\shell\PanakCreate")) {
                        key.SetValue("", createText);
                        key.SetValue("MultiSelectModel", "Player");
                        key.SetValue("Icon", $"\"{exePath}\",0");
                        using var cmdKey = key.CreateSubKey("command");
                        cmdKey.SetValue("", $"\"{exePath}\" --create \"%1\"");
                    }
                }

                // 2. HKCU\Software\Classes\Directory\shell\PanakOpen and Folder\shell\PanakOpen (Open with Panak)
                string[] dirKeys = new[] { "Directory", "Folder" };
                foreach (var dk in dirKeys) {
                    using (var key = Registry.CurrentUser.CreateSubKey($@"{HKCU_CLASSES}\{dk}\shell\PanakOpen")) {
                        key.SetValue("", openText);
                        key.SetValue("Icon", $"\"{exePath}\",0");
                        using var cmdKey = key.CreateSubKey("command");
                        cmdKey.SetValue("", $"\"{exePath}\" --open \"%1\"");
                    }

                    using (var key = Registry.CurrentUser.CreateSubKey($@"{HKCU_CLASSES}\{dk}\shell\PanakConvert")) {
                        key.SetValue("", convertText);
                        key.SetValue("Icon", $"\"{exePath}\",0");
                        using var cmdKey = key.CreateSubKey("command");
                        cmdKey.SetValue("", $"\"{exePath}\" --convert \"%1\"");
                    }

                    using (var key = Registry.CurrentUser.CreateSubKey($@"{HKCU_CLASSES}\{dk}\shell\PanakUnpack")) {
                        key.SetValue("", unpackText);
                        key.SetValue("Icon", $"\"{exePath}\",0");
                        using var cmdKey = key.CreateSubKey("command");
                        cmdKey.SetValue("", $"\"{exePath}\" --unpack \"%1\"");
                    }
                }

                // 3. HKCU\Software\Classes\Directory\Background\shell\PanakNew and DesktopBackground (New Panak folder in background)
                string[] bgKeys = new[] { @"Directory\Background", "DesktopBackground" };
                foreach (var bk in bgKeys) {
                    using (var key = Registry.CurrentUser.CreateSubKey($@"{HKCU_CLASSES}\{bk}\shell\PanakNew")) {
                        key.SetValue("", createText);
                        key.SetValue("Icon", $"\"{exePath}\",0");
                        using var cmdKey = key.CreateSubKey("command");
                        cmdKey.SetValue("", $"\"{exePath}\" --new \"%V\"");
                    }
                }

                // 4. HKCU\Software\Classes\.panak association
                using (var key = Registry.CurrentUser.CreateSubKey($@"{HKCU_CLASSES}\.panak")) {
                    key.SetValue("", "PanakFolder");
                }

                using (var key = Registry.CurrentUser.CreateSubKey($@"{HKCU_CLASSES}\PanakFolder")) {
                    key.SetValue("", "Panak Android Folder");
                    using var defIcon = key.CreateSubKey("DefaultIcon");
                    defIcon.SetValue("", $"\"{exePath}\",0");
                    using var shell = key.CreateSubKey("shell");
                    using var open = shell.CreateSubKey("open");
                    using var cmd = open.CreateSubKey("command");
                    cmd.SetValue("", $"\"{exePath}\" --open \"%1\"");
                    using var unpack = shell.CreateSubKey("unpack");
                    unpack.SetValue("", unpackText);
                    unpack.SetValue("Icon", $"\"{exePath}\",0");
                    using var unpackCmd = unpack.CreateSubKey("command");
                    unpackCmd.SetValue("", $"\"{exePath}\" --unpack \"%1\"");
                }

                // 5. DragDropHandlers for right-click drag and drop (like 7-Zip & WinRAR)
                string dir = Path.GetDirectoryName(exePath) ?? string.Empty;
                string comhostPath = Path.Combine(dir, "Panak.ShellExt.comhost.dll");
                if (!File.Exists(comhostPath)) {
                    string[] candidates = new[] {
                        Path.Combine(dir, "Panak.ShellExt.comhost.dll"),
                        Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "Panak.ShellExt", "bin", "Release", "net8.0-windows", "Panak.ShellExt.comhost.dll")),
                        Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "Panak.ShellExt", "bin", "Release", "net8.0-windows", "Panak.ShellExt.comhost.dll")),
                        Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "Panak.ShellExt", "bin", "Debug", "net8.0-windows", "Panak.ShellExt.comhost.dll")),
                        Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "Panak.ShellExt", "bin", "Debug", "net8.0-windows", "Panak.ShellExt.comhost.dll")),
                    };
                    foreach (var cand in candidates) {
                        if (File.Exists(cand)) {
                            comhostPath = cand;
                            break;
                        }
                    }
                }

                if (File.Exists(comhostPath)) {
                    string clsid = "{E5A7B812-3D4C-4A2E-8910-1F2D3C4E5A6B}";
                    using (var clsidKey = Registry.CurrentUser.CreateSubKey($@"{HKCU_CLASSES}\CLSID\{clsid}")) {
                        clsidKey.SetValue("", "Panak DragDrop Handler");
                        using var inprocKey = clsidKey.CreateSubKey("InprocServer32");
                        inprocKey.SetValue("", comhostPath);
                        inprocKey.SetValue("ThreadingModel", "Apartment");
                    }

                    string[] dragDropRoots = new[] {
                        "Directory",
                        "Folder",
                        "Drive",
                        @"Directory\Background",
                        "DesktopBackground",
                        "*",
                        "AllFilesystemObjects"
                    };
                    foreach (var root in dragDropRoots) {
                        using var handlerKey = Registry.CurrentUser.CreateSubKey($@"{HKCU_CLASSES}\{root}\shellex\DragDropHandlers\Panak");
                        handlerKey.SetValue("", clsid);
                    }

                    // Register in HKCU Shell Extensions\Approved so Explorer allows the extension
                    try {
                        using var approvedKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved");
                        approvedKey?.SetValue(clsid, "Panak DragDrop Handler");
                    } catch { }

                    // Enable HKCU shell extensions policy
                    try {
                        using var policyKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer");
                        policyKey?.SetValue("EnableShellExtensionsInHKCU", 1, RegistryValueKind.DWord);
                    } catch { }

                    // Also try registering in HKLM if elevated / writeable
                    try {
                        using var hklmClsidKey = Registry.LocalMachine.CreateSubKey($@"Software\Classes\CLSID\{clsid}");
                        if (hklmClsidKey != null) {
                            hklmClsidKey.SetValue("", "Panak DragDrop Handler");
                            using var inproc = hklmClsidKey.CreateSubKey("InprocServer32");
                            inproc?.SetValue("", comhostPath);
                            inproc?.SetValue("ThreadingModel", "Apartment");
                        }
                        using var hklmApproved = Registry.LocalMachine.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved");
                        hklmApproved?.SetValue(clsid, "Panak DragDrop Handler");
                    } catch { }
                }

                ShellHelper.NotifyShellAssocChanged();
            } catch {
                // Ignore registry access error
            }
        }

        public static void UnregisterAll() {
            try {
                string clsid = "{E5A7B812-3D4C-4A2E-8910-1F2D3C4E5A6B}";
                string[] dragDropRoots = new[] {
                    "Directory",
                    "Folder",
                    "Drive",
                    @"Directory\Background",
                    "DesktopBackground",
                    "*",
                    "AllFilesystemObjects"
                };
                foreach (var root in dragDropRoots) {
                    Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\{root}\shellex\DragDropHandlers\Panak", false);
                }
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\CLSID\{clsid}", false);

                try {
                    using var approvedKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved", true);
                    approvedKey?.DeleteValue(clsid, false);
                } catch { }

                try {
                    using var hklmApproved = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved", true);
                    hklmApproved?.DeleteValue(clsid, false);
                    Registry.LocalMachine.DeleteSubKeyTree($@"Software\Classes\CLSID\{clsid}", false);
                } catch { }

                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\*\shell\PanakCreate", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\Directory\shell\PanakCreate", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\Folder\shell\PanakCreate", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\AllFilesystemObjects\shell\PanakCreate", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\Directory\shell\PanakOpen", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\Directory\shell\PanakConvert", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\Directory\shell\PanakUnpack", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\Folder\shell\PanakOpen", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\Folder\shell\PanakConvert", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\Folder\shell\PanakUnpack", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\Directory\Background\shell\PanakNew", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\DesktopBackground\shell\PanakNew", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\.panak", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"{HKCU_CLASSES}\PanakFolder", false);

                ShellHelper.NotifyShellAssocChanged();
            } catch {
                // Ignore registry cleanup error
            }
        }

        public static bool IsRegistered() {
            try {
                using var key = Registry.CurrentUser.OpenSubKey($@"{HKCU_CLASSES}\*\shell\PanakCreate");
                return key != null;
            } catch {
                return false;
            }
        }
    }
}

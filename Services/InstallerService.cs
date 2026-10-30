using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace Panak.Services {
    public static class InstallerService {
        private const string APP_NAME = "Panak";
        private const string UNINSTALL_REG_KEY = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Panak";
        private const string RUN_REG_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static string GetInstallDirectory() {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Programs", APP_NAME);
        }

        public static bool IsInstalled() {
            try {
                using var key = Registry.CurrentUser.OpenSubKey(UNINSTALL_REG_KEY);
                return key != null;
            } catch {
                return false;
            }
        }

        public static bool Install(bool createDesktopShortcut = true, bool createStartMenuShortcut = true, bool autoStart = true) {
            try {
                string targetDir = GetInstallDirectory();
                if (!Directory.Exists(targetDir)) {
                    Directory.CreateDirectory(targetDir);
                }

                int currentPid = Process.GetCurrentProcess().Id;
                foreach (var p in Process.GetProcessesByName("Panak")) {
                    if (p.Id != currentPid) {
                        try {
                            p.Kill();
                            p.WaitForExit(1500);
                        } catch {
                            // Ignore
                        }
                    }
                }

                // Copy current running application files or publish directory to targetDir
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                string sourceDir = !string.IsNullOrEmpty(currentExe) ? Path.GetDirectoryName(currentExe)! : AppDomain.CurrentDomain.BaseDirectory;

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string candidatePublish = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "publish"));
                if (Directory.Exists(candidatePublish) && File.Exists(Path.Combine(candidatePublish, "Panak.exe"))) {
                    sourceDir = candidatePublish;
                } else if (Directory.Exists(Path.Combine(baseDir, "publish")) && File.Exists(Path.Combine(baseDir, "publish", "Panak.exe"))) {
                    sourceDir = Path.Combine(baseDir, "publish");
                }

                if (!sourceDir.Equals(targetDir, StringComparison.OrdinalIgnoreCase)) {
                    foreach (var file in Directory.GetFiles(sourceDir)) {
                        string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                        try {
                            File.Copy(file, destFile, true);
                        } catch {
                            // Ignore if identical
                        }
                    }

                    // Ensure Panak.ShellExt files are also copied if located in adjacent build output
                    string[] shellExtCandidates = new[] {
                        Path.Combine(baseDir, "Panak.ShellExt.comhost.dll"),
                        Path.Combine(baseDir, "..", "..", "..", "Panak.ShellExt", "bin", "Release", "net8.0-windows", "Panak.ShellExt.comhost.dll"),
                        Path.Combine(baseDir, "..", "..", "..", "Panak.ShellExt", "bin", "Debug", "net8.0-windows", "Panak.ShellExt.comhost.dll")
                    };
                    foreach (var cand in shellExtCandidates) {
                        if (File.Exists(cand)) {
                            string candDir = Path.GetDirectoryName(cand)!;
                            foreach (var f in Directory.GetFiles(candDir, "Panak.ShellExt.*")) {
                                string df = Path.Combine(targetDir, Path.GetFileName(f));
                                try { File.Copy(f, df, true); } catch { }
                            }
                            break;
                        }
                    }
                }

                string targetExe = Path.Combine(targetDir, "Panak.exe");

                // 1. Create Shortcuts
                if (createDesktopShortcut) {
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string shortcutPath = Path.Combine(desktopPath, "Panak.lnk");
                    CreateShortcut(shortcutPath, targetExe, "--background", "Panak - Android-style Folders");
                }

                if (createStartMenuShortcut) {
                    string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                    string shortcutPath = Path.Combine(startMenu, "Panak.lnk");
                    CreateShortcut(shortcutPath, targetExe, "--background", "Panak - Android-style Folders");
                }

                // 2. Register Context Menu in Explorer
                ContextMenuRegistry.RegisterAll(targetExe);

                // 3. Auto-start with Windows
                if (autoStart) {
                    SetAutoStart(targetExe, true);
                }

                // 4. Windows "Add or Remove Programs" entry
                using (var key = Registry.CurrentUser.CreateSubKey(UNINSTALL_REG_KEY)) {
                    string uninstExe = Path.Combine(targetDir, "Uninstall.exe");
                    string displayIcon = File.Exists(uninstExe) ? $"\"{uninstExe}\",0" : $"\"{targetExe}\",0";
                    key.SetValue("DisplayName", "Panak - Material You Folders");
                    key.SetValue("DisplayIcon", displayIcon);
                    key.SetValue("DisplayVersion", "2.0.5");
                    key.SetValue("Publisher", "Panak Team");
                    key.SetValue("InstallLocation", targetDir);
                    key.SetValue("UninstallString", $"\"{targetExe}\" --uninstall");
                }

                return true;
            } catch (Exception ex) {
                Debug.WriteLine($"Install error: {ex.Message}");
                return false;
            }
        }

        public static bool Uninstall() {
            try {
                // 1. Remove Context Menu entries
                ContextMenuRegistry.UnregisterAll();

                // 2. Remove Auto-start
                SetAutoStart(string.Empty, false);

                // 3. Remove Desktop & Start Menu shortcuts
                string desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Panak.lnk");
                if (File.Exists(desktopShortcut)) {
                    File.Delete(desktopShortcut);
                }

                string startMenuShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Panak.lnk");
                if (File.Exists(startMenuShortcut)) {
                    File.Delete(startMenuShortcut);
                }

                // 4. Remove Add or Remove Programs registry entry
                Registry.CurrentUser.DeleteSubKeyTree(UNINSTALL_REG_KEY, false);

                // 5. Schedule folder cleanup on exit if installed
                string installDir = GetInstallDirectory();
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (currentExe.StartsWith(installDir, StringComparison.OrdinalIgnoreCase)) {
                    // Detached cmd process to wait and delete install directory
                    ProcessStartInfo psi = new ProcessStartInfo {
                        FileName = "cmd.exe",
                        Arguments = $"/C ping 127.0.0.1 -n 3 > NUL & rmdir /S /Q \"{installDir}\"",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    };
                    Process.Start(psi);
                }

                return true;
            } catch (Exception ex) {
                Debug.WriteLine($"Uninstall error: {ex.Message}");
                return false;
            }
        }

        public static void SetAutoStart(string exePath, bool enable) {
            try {
                if (string.IsNullOrEmpty(exePath)) {
                    string targetDir = GetInstallDirectory();
                    string candidateExe = Path.Combine(targetDir, "Panak.exe");
                    exePath = File.Exists(candidateExe)
                        ? candidateExe
                        : (Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty);
                }

                // 1. Registry HKCU Run Key
                using (var key = Registry.CurrentUser.OpenSubKey(RUN_REG_KEY, true)) {
                    if (key != null) {
                        if (enable && !string.IsNullOrEmpty(exePath)) {
                            key.SetValue(APP_NAME, $"\"{exePath}\" --background");
                        } else {
                            key.DeleteValue(APP_NAME, false);
                        }
                    }
                }

                // 2. Windows Startup Folder Shortcut (shell:startup)
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string startupShortcut = Path.Combine(startupFolder, "Panak.lnk");
                if (enable && !string.IsNullOrEmpty(exePath) && File.Exists(exePath)) {
                    CreateShortcut(startupShortcut, exePath, "--background", "Panak - Android-style Folders");
                } else if (!enable && File.Exists(startupShortcut)) {
                    try {
                        File.Delete(startupShortcut);
                    } catch { }
                }

                // 3. Keep Settings in sync
                if (SettingsService.Instance.Current.AutoStart != enable) {
                    SettingsService.Instance.Current.AutoStart = enable;
                    SettingsService.Instance.Save();
                }
            } catch {
                // Fallback
            }
        }

        public static bool IsAutoStartEnabled() {
            try {
                using var key = Registry.CurrentUser.OpenSubKey(RUN_REG_KEY);
                bool regOk = key?.GetValue(APP_NAME) != null;
                string startupShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Panak.lnk");
                return regOk || File.Exists(startupShortcut);
            } catch {
                return false;
            }
        }

        public static void EnsureAutoStartRegistered(string exePath) {
            if (SettingsService.Instance.Current.AutoStart) {
                SetAutoStart(exePath, true);
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string description) {
            try {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null) {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    if (shell != null) {
                        dynamic shortcut = shell.CreateShortcut(shortcutPath);
                        shortcut.TargetPath = targetPath;
                        shortcut.Arguments = arguments;
                        shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath) ?? string.Empty;
                        shortcut.Description = description;
                        shortcut.IconLocation = $"{targetPath},0";
                        shortcut.Save();
                    }
                }
            } catch {
                // Shortcut creation fallback
            }
        }
    }
}

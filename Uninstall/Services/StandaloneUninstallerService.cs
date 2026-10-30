using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Panak.Services {
    public static class StandaloneUninstallerService {
        public const string APP_NAME = "Panak";
        public const string UNINSTALL_REG_KEY = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Panak";
        public const string RUN_REG_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static string GetDefaultInstallDirectory() {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Programs", APP_NAME);
        }

        public static string GetInstalledDirectory() {
            try {
                using var key = Registry.CurrentUser.OpenSubKey(UNINSTALL_REG_KEY);
                string? loc = key?.GetValue("InstallLocation") as string;
                if (!string.IsNullOrEmpty(loc) && Directory.Exists(loc)) {
                    return loc;
                }
            } catch { }

            string defaultDir = GetDefaultInstallDirectory();
            if (Directory.Exists(defaultDir)) {
                return defaultDir;
            }

            try {
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                if (File.Exists(Path.Combine(currentDir, "Panak.exe"))) {
                    return currentDir;
                }
            } catch { }

            return defaultDir;
        }

        public static void TerminateRunningPanak() {
            int currentPid = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcessesByName("Panak")) {
                if (p.Id != currentPid) {
                    try {
                        p.Kill();
                        p.WaitForExit(2000);
                    } catch { }
                }
            }
        }

        public static (bool isInstalled, string? version) CheckDotNet8DesktopRuntime() {
            try {
                // 1. Check directory C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string desktopSharedDir = Path.Combine(programFiles, "dotnet", "shared", "Microsoft.WindowsDesktop.App");
                if (Directory.Exists(desktopSharedDir)) {
                    foreach (var dir in Directory.GetDirectories(desktopSharedDir)) {
                        string name = Path.GetFileName(dir);
                        if (Version.TryParse(name, out var v) && v.Major == 8) {
                            return (true, name);
                        }
                    }
                }

                // 2. Check 64-bit and WOW6432Node registry
                string[] regPaths = new[] {
                    @"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App",
                    @"SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App"
                };

                foreach (var regPath in regPaths) {
                    try {
                        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                        using var subKey = baseKey.OpenSubKey(regPath);
                        if (subKey != null) {
                            foreach (var valName in subKey.GetValueNames()) {
                                if (Version.TryParse(valName, out var v) && v.Major == 8) {
                                    return (true, valName);
                                }
                            }
                        }
                    } catch { }
                }
            } catch { }

            return (false, null);
        }

        public static async Task<(bool success, string message)> UninstallDotNet8DesktopRuntimeAsync(
            IProgress<(double percent, string status)>? progress = null) {

            return await Task.Run(() => {
                try {
                    progress?.Report((0.60, LocalizationService.Instance["UninstallingDotNet8"] ?? "Searching .NET 8.0 Desktop Runtime uninstaller..."));

                    string[] rootRegs = new[] {
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                    };

                    RegistryView[] views = new[] { RegistryView.Registry64, RegistryView.Registry32 };

                    string? uninstallCmd = null;
                    string? quietCmd = null;

                    foreach (var view in views) {
                        foreach (var rootPath in rootRegs) {
                            try {
                                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                                using var uninstallKey = baseKey.OpenSubKey(rootPath);
                                if (uninstallKey == null) continue;

                                foreach (var subKeyName in uninstallKey.GetSubKeyNames()) {
                                    using var subKey = uninstallKey.OpenSubKey(subKeyName);
                                    if (subKey == null) continue;

                                    string? dispName = subKey.GetValue("DisplayName") as string;
                                    if (string.IsNullOrEmpty(dispName)) continue;

                                    if ((dispName.Contains("Windows Desktop Runtime", StringComparison.OrdinalIgnoreCase) ||
                                         dispName.Contains(".NET Desktop Runtime", StringComparison.OrdinalIgnoreCase)) &&
                                        dispName.Contains("8.0", StringComparison.OrdinalIgnoreCase)) {

                                        quietCmd = subKey.GetValue("QuietUninstallString") as string;
                                        uninstallCmd = subKey.GetValue("UninstallString") as string;
                                        if (!string.IsNullOrEmpty(quietCmd) || !string.IsNullOrEmpty(uninstallCmd)) {
                                            break;
                                        }
                                    }
                                }
                            } catch { }

                            if (!string.IsNullOrEmpty(quietCmd) || !string.IsNullOrEmpty(uninstallCmd)) {
                                break;
                            }
                        }
                        if (!string.IsNullOrEmpty(quietCmd) || !string.IsNullOrEmpty(uninstallCmd)) {
                            break;
                        }
                    }

                    string? cmdToRun = !string.IsNullOrEmpty(quietCmd) ? quietCmd : uninstallCmd;
                    if (string.IsNullOrEmpty(cmdToRun)) {
                        return (false, "Could not find .NET 8 uninstaller command.");
                    }

                    progress?.Report((0.70, LocalizationService.Instance["UninstallingDotNet8"] ?? "Uninstalling .NET 8.0 Desktop Runtime..."));

                    string fileName;
                    string args = "";

                    cmdToRun = cmdToRun.Trim();
                    if (cmdToRun.StartsWith("\"")) {
                        int secondQuote = cmdToRun.IndexOf('\"', 1);
                        if (secondQuote > 0) {
                            fileName = cmdToRun.Substring(1, secondQuote - 1);
                            args = cmdToRun.Substring(secondQuote + 1).Trim();
                        } else {
                            fileName = cmdToRun;
                        }
                    } else {
                        int firstSpace = cmdToRun.IndexOf(' ');
                        if (firstSpace > 0) {
                            fileName = cmdToRun.Substring(0, firstSpace);
                            args = cmdToRun.Substring(firstSpace + 1).Trim();
                        } else {
                            fileName = cmdToRun;
                        }
                    }

                    if (string.IsNullOrEmpty(quietCmd)) {
                        if (!args.Contains("/quiet", StringComparison.OrdinalIgnoreCase)) {
                            args += " /quiet /norestart";
                        }
                    }

                    var psi = new ProcessStartInfo {
                        FileName = fileName,
                        Arguments = args,
                        UseShellExecute = true,
                        Verb = "runas"
                    };

                    var proc = Process.Start(psi);
                    if (proc != null) {
                        proc.WaitForExit(90000);
                        return (proc.ExitCode == 0, $"Finished with exit code {proc.ExitCode}");
                    }

                    return (true, string.Empty);
                } catch (Exception ex) {
                    Debug.WriteLine($".NET 8 uninstall error: {ex}");
                    return (false, ex.Message);
                }
            });
        }

        public static async Task<(bool success, string error)> UninstallAsync(
            string? targetDir = null,
            bool removeContextMenus = true,
            bool removeShortcuts = true,
            bool removeSettings = false,
            bool uninstallDotNet8 = false,
            IProgress<(double percent, string status)>? progress = null) {

            return await Task.Run(async () => {
                try {
                    if (FolderLockService.Instance.HasAnyLockedFolders()) {
                        string msg = LocalizationService.Instance["LockedFoldersExistWarning"]
                            ?? "Առկա են կողպված պանակներ: Ապատեղադրելու համար նախ ապակողպեք դրանք:";
                        return (false, msg);
                    }

                    progress?.Report((0.10, LocalizationService.Instance["StoppingProcesses"] ?? "Stopping Panak processes..."));
                    TerminateRunningPanak();

                    if (removeContextMenus) {
                        progress?.Report((0.25, LocalizationService.Instance["RemovingMenus"] ?? "Removing Explorer context menus..."));
                        try {
                            ContextMenuRegistry.UnregisterAll();
                        } catch { }
                    }

                    if (removeShortcuts) {
                        progress?.Report((0.40, LocalizationService.Instance["RemovingShortcuts"] ?? "Removing shortcuts..."));
                        try {
                            string desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Panak.lnk");
                            if (File.Exists(desktopShortcut)) File.Delete(desktopShortcut);
                        } catch { }

                        try {
                            string startMenuShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Panak.lnk");
                            if (File.Exists(startMenuShortcut)) File.Delete(startMenuShortcut);
                        } catch { }
                    }

                    progress?.Report((0.50, LocalizationService.Instance["CleaningRegistry"] ?? "Removing registry settings..."));
                    try {
                        using var key = Registry.CurrentUser.OpenSubKey(RUN_REG_KEY, true);
                        key?.DeleteValue(APP_NAME, false);
                    } catch { }

                    try {
                        Registry.CurrentUser.DeleteSubKeyTree(UNINSTALL_REG_KEY, false);
                    } catch { }

                    if (uninstallDotNet8) {
                        progress?.Report((0.65, LocalizationService.Instance["UninstallingDotNet8"] ?? "Uninstalling .NET 8.0..."));
                        try {
                            await UninstallDotNet8DesktopRuntimeAsync(progress);
                        } catch { }
                    }

                    if (removeSettings) {
                        progress?.Report((0.75, LocalizationService.Instance["RemovingSettingsData"] ?? "Removing user settings and data..."));
                        try {
                            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                            string panakAppData = Path.Combine(appData, "Panak");
                            if (Directory.Exists(panakAppData)) {
                                Directory.Delete(panakAppData, true);
                            }
                        } catch { }
                    }

                    progress?.Report((0.85, LocalizationService.Instance["CleaningFiles"] ?? "Cleaning program files..."));
                    string installDir = !string.IsNullOrEmpty(targetDir) ? targetDir : GetInstalledDirectory();
                    string currentExe = Environment.ProcessPath
                        ?? Process.GetCurrentProcess().MainModule?.FileName
                        ?? string.Empty;

                    if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir)) {
                        string currentBaseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                        string cleanInstallDir = installDir.TrimEnd('\\');
                        bool isRunningFromInstall = currentBaseDir.Equals(cleanInstallDir, StringComparison.OrdinalIgnoreCase) ||
                                                    (!string.IsNullOrEmpty(currentExe) && currentExe.StartsWith(cleanInstallDir, StringComparison.OrdinalIgnoreCase));

                        if (isRunningFromInstall) {
                            // Only delete main application executable and assets.
                            // DO NOT delete runtime DLLs (*.dll), configs (*.json), or Uninstall.* while the process is active,
                            // otherwise CRT/WPF uninitialization crashes with DllNotFoundException on process shutdown.
                            try {
                                foreach (var file in Directory.GetFiles(installDir, "*", SearchOption.AllDirectories)) {
                                    string fileName = Path.GetFileName(file);
                                    if (fileName.StartsWith("Panak", StringComparison.OrdinalIgnoreCase) &&
                                        !fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                                        !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) {
                                        try { File.Delete(file); } catch { }
                                    }
                                }
                            } catch { }

                            // Schedule entire directory deletion right after this uninstaller exits
                            try {
                                ProcessStartInfo psi = new ProcessStartInfo {
                                    FileName = "cmd.exe",
                                    Arguments = $"/C ping 127.0.0.1 -n 3 > NUL & rmdir /S /Q \"{installDir}\" > NUL 2>&1 & if exist \"{installDir}\" (ping 127.0.0.1 -n 2 > NUL & rmdir /S /Q \"{installDir}\" > NUL 2>&1)",
                                    WindowStyle = ProcessWindowStyle.Hidden,
                                    CreateNoWindow = true
                                };
                                Process.Start(psi);
                            } catch { }
                        } else {
                            try {
                                Directory.Delete(installDir, true);
                            } catch {
                                try {
                                    ProcessStartInfo psi = new ProcessStartInfo {
                                        FileName = "cmd.exe",
                                        Arguments = $"/C ping 127.0.0.1 -n 3 > NUL & rmdir /S /Q \"{installDir}\"",
                                        WindowStyle = ProcessWindowStyle.Hidden,
                                        CreateNoWindow = true
                                    };
                                    Process.Start(psi);
                                } catch { }
                            }
                        }
                    }

                    progress?.Report((1.0, LocalizationService.Instance["UninstalledSuccess"] ?? "Panak has been uninstalled successfully!"));
                    return (true, string.Empty);
                } catch (Exception ex) {
                    Debug.WriteLine($"Uninstall error: {ex}");
                    return (false, ex.Message);
                }
            });
        }
    }
}

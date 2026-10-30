using System;
using System.Linq;
using System.Windows;
using Panak.Services;

namespace Panak.Setup {
    public partial class App : Application {
        protected override async void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, ev) => {
                try {
                    string log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "panak_setup_crash.log");
                    System.IO.File.AppendAllText(log, $"[{DateTime.Now}] Unhandled: {ev.ExceptionObject}\n");
                    MessageBox.Show($"Setup Error: {ev.ExceptionObject}", "Panak Setup", MessageBoxButton.OK, MessageBoxImage.Error);
                } catch { }
            };

            DispatcherUnhandledException += (s, ev) => {
                try {
                    string log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "panak_setup_crash.log");
                    System.IO.File.AppendAllText(log, $"[{DateTime.Now}] Dispatcher: {ev.Exception}\n");
                    MessageBox.Show($"Setup Error: {ev.Exception}", "Panak Setup", MessageBoxButton.OK, MessageBoxImage.Error);
                } catch { }
                ev.Handled = true;
            };

            string[] args = e.Args;

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Handle silent modes if requested
            if (args.Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase) || a.Equals("--install-silent", StringComparison.OrdinalIgnoreCase))) {
                string targetDir = StandaloneInstallerService.GetInstalledDirectory();
                if (string.IsNullOrEmpty(targetDir)) {
                    targetDir = StandaloneInstallerService.GetDefaultInstallDirectory();
                }
                for (int i = 0; i < args.Length - 1; i++) {
                    if (args[i].Equals("--dir", StringComparison.OrdinalIgnoreCase)) {
                        targetDir = args[i + 1];
                    }
                }
                await StandaloneInstallerService.InstallAsync(targetDir, createDesktopShortcut: true, createStartMenuShortcut: true, autoStart: true);
                try {
                    string exe = System.IO.Path.Combine(targetDir, "Panak.exe");
                    if (System.IO.File.Exists(exe)) {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                            FileName = exe,
                            Arguments = "--background",
                            UseShellExecute = true
                        });
                    }
                } catch { }
                Shutdown();
                return;
            }

            if (args.Any(a => a.Equals("--uninstall-silent", StringComparison.OrdinalIgnoreCase))) {
                await StandaloneInstallerService.UninstallAsync();
                Shutdown();
                return;
            }

            string? initialLang = null;
            for (int i = 0; i < args.Length - 1; i++) {
                if (args[i].Equals("--lang", StringComparison.OrdinalIgnoreCase)) {
                    initialLang = args[i + 1];
                }
            }
            if (!string.IsNullOrEmpty(initialLang)) {
                LocalizationService.Instance.CurrentLanguage = initialLang;
            }

            bool isUninstallMode = args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase));
            bool isUpdateMode = args.Any(a => a.Equals("--update", StringComparison.OrdinalIgnoreCase));

            ShutdownMode = ShutdownMode.OnLastWindowClose;
            var window = new SetupWindow(isUninstallMode: isUninstallMode, isUpdateMode: isUpdateMode, initialLanguage: initialLang);
            window.Show();
        }
    }
}

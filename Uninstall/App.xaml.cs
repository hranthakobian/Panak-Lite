using System;
using System.Linq;
using System.Windows;
using Panak.Services;

namespace Panak.Uninstall {
    public partial class App : Application {
        protected override async void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, ev) => {
                try {
                    if (ev.ExceptionObject is DllNotFoundException) return;
                    string log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "panak_uninstall_crash.log");
                    System.IO.File.AppendAllText(log, $"[{DateTime.Now}] Unhandled: {ev.ExceptionObject}\n");
                    MessageBox.Show($"Uninstaller Error: {ev.ExceptionObject}", "Panak Uninstaller", MessageBoxButton.OK, MessageBoxImage.Error);
                } catch { }
            };

            DispatcherUnhandledException += (s, ev) => {
                try {
                    if (ev.Exception is DllNotFoundException) {
                        ev.Handled = true;
                        return;
                    }
                    string log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "panak_uninstall_crash.log");
                    System.IO.File.AppendAllText(log, $"[{DateTime.Now}] Dispatcher: {ev.Exception}\n");
                    MessageBox.Show($"Uninstaller Error: {ev.Exception}", "Panak Uninstaller", MessageBoxButton.OK, MessageBoxImage.Error);
                } catch { }
                ev.Handled = true;
            };

            string[] args = e.Args;

            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try {
                ShellHelper.SetCurrentProcessExplicitAppUserModelID("Panak.Uninstaller");
            } catch { }

            if (args.Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase) ||
                              a.Equals("--quiet", StringComparison.OrdinalIgnoreCase))) {
                bool removeSettings = args.Any(a => a.Equals("--remove-settings", StringComparison.OrdinalIgnoreCase));
                bool removeDotNet = args.Any(a => a.Equals("--remove-dotnet", StringComparison.OrdinalIgnoreCase));
                await StandaloneUninstallerService.UninstallAsync(
                    removeContextMenus: true,
                    removeShortcuts: true,
                    removeSettings: removeSettings,
                    uninstallDotNet8: removeDotNet);
                Shutdown();
                return;
            }

            ShutdownMode = ShutdownMode.OnLastWindowClose;
            var window = new UninstallerWindow();
            window.Show();
        }
    }
}

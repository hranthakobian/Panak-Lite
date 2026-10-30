using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Panak.Services;

namespace Panak.Views {
    public partial class CrashReportWindow : Window {
        private const string SupportEmail = "dev@hakobian.am";
        private readonly string _errorDetails;

        public CrashReportWindow(string errorDetails) {
            InitializeComponent();
            _errorDetails = errorDetails;
            ErrorDetailsBox.Text = _errorDetails;
            LoadThemeAndPalette();
            ApplyLocalization();
        }

        public CrashReportWindow(Exception ex) : this(FormatException(ex)) { }

        private void LoadThemeAndPalette() {
            try {
                var settings = SettingsService.Instance.Current ?? new Models.AppSettings();
                string colorHex = settings.ThemeColor;
                if (string.IsNullOrEmpty(colorHex)) colorHex = ShellHelper.DefaultPastelColorHex;
                string themeMode = settings.ThemeMode;

                bool isDark = themeMode == "Dark" || (themeMode != "Light" && ShellHelper.IsWindowsDarkTheme());
                double hue = ShellHelper.GetHueFromHex(colorHex);

                System.Windows.Media.Color toWpf(System.Drawing.Color c) => System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B);

                System.Windows.Media.Color wpfSurface, wpfContainer, wpfCard, wpfPrimary, wpfOnPrimary, wpfOnSurface, wpfOnSurfaceVariant;

                if (isDark) {
                    wpfSurface = toWpf(ShellHelper.ColorFromHsl(hue, 0.16, 0.11));
                    wpfContainer = toWpf(ShellHelper.ColorFromHsl(hue, 0.20, 0.18));
                    wpfCard = toWpf(ShellHelper.ColorFromHsl(hue, 0.18, 0.14));
                    wpfPrimary = toWpf(ShellHelper.ColorFromHsl(hue, 0.70, 0.72));
                    wpfOnPrimary = System.Windows.Media.Color.FromRgb(0x1C, 0x1B, 0x1F);
                    wpfOnSurface = System.Windows.Media.Color.FromRgb(0xE6, 0xE1, 0xE5);
                    wpfOnSurfaceVariant = System.Windows.Media.Color.FromRgb(0xCA, 0xC4, 0xD0);
                } else {
                    var (surfaceColor, containerColor, primaryColor) = ShellHelper.GetMaterialYouTonalPalette(hue);
                    var cardColor = ShellHelper.ColorFromHsl(hue, 0.60, 0.94);
                    var onSurfaceColor = ShellHelper.ColorFromHsl(hue, 0.40, 0.16);
                    var onSurfaceVariantColor = ShellHelper.ColorFromHsl(hue, 0.28, 0.42);

                    wpfSurface = toWpf(surfaceColor);
                    wpfContainer = toWpf(containerColor);
                    wpfCard = toWpf(cardColor);
                    wpfPrimary = toWpf(primaryColor);
                    wpfOnPrimary = System.Windows.Media.Color.FromRgb(0xFE, 0xF4, 0xEB);
                    wpfOnSurface = toWpf(onSurfaceColor);
                    wpfOnSurfaceVariant = toWpf(onSurfaceVariantColor);
                }

                void SetBrush(string key, System.Windows.Media.Color col) {
                    var brush = new System.Windows.Media.SolidColorBrush(col);
                    brush.Freeze();
                    Resources[key] = brush;
                }

                SetBrush("M3SurfaceBrush", wpfSurface);
                SetBrush("M3SurfaceContainerBrush", wpfContainer);
                SetBrush("M3CardBrush", wpfCard);
                SetBrush("M3PrimaryBrush", wpfPrimary);
                SetBrush("M3OnPrimaryBrush", wpfOnPrimary);
                SetBrush("M3OnSurfaceBrush", wpfOnSurface);
                SetBrush("M3OnSurfaceVariantBrush", wpfOnSurfaceVariant);

                if (CrashShapeBadgePath != null) {
                    string shape = settings.IconShape;
                    var geom = shape switch {
                        "Circle" => FindResource("GeomCircle") as System.Windows.Media.Geometry,
                        "Clover" => FindResource("GeomClover") as System.Windows.Media.Geometry,
                        "Scallop" => FindResource("GeomScallop") as System.Windows.Media.Geometry,
                        "Arch" => FindResource("GeomArch") as System.Windows.Media.Geometry,
                        _ => FindResource("GeomSquircle") as System.Windows.Media.Geometry
                    };
                    if (geom != null) {
                        CrashShapeBadgePath.Data = geom;
                    }
                }
            } catch { }
        }

        private void ApplyLocalization() {
            try {
                var loc = LocalizationService.Instance;
                string lang = loc.CurrentLanguage;
                bool isArm = lang == "hy" || string.IsNullOrEmpty(lang);

                if (isArm) {
                    CrashTitleText.Text = "Panak - Սխալի մասին հաղորդում";
                    CrashSubtitleText.Text = "Ծրագրի աշխատանքի ընթացքում տեղի է ունեցել անսպասելի սխալ: Կարող եք ուղարկել լոգը մշակողին՝ խնդիրը շտկելու համար:";
                    CopyBtnText.Text = "Պատճենել";
                    OpenLogBtnText.Text = "Բացել ֆայլը";
                    RestartBtnText.Text = "Վերագործարկել";
                    SendEmailBtnText.Text = "Ուղարկել նամակ";
                } else {
                    CrashTitleText.Text = "Panak - Crash Report";
                    CrashSubtitleText.Text = "An unexpected error occurred in Panak. You can send this report to the developer to help resolve it:";
                    CopyBtnText.Text = "Copy";
                    OpenLogBtnText.Text = "Open Log";
                    RestartBtnText.Text = "Restart";
                    SendEmailBtnText.Text = "Send Email";
                }
            } catch { }
        }

        public static string FormatException(Exception? ex) {
            var sb = new StringBuilder();
            sb.AppendLine("=== PANAK CRASH REPORT ===");
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"App Version: 1.0.0");
            sb.AppendLine($"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
            sb.AppendLine($".NET Runtime: {Environment.Version}");
            sb.AppendLine($"Process Architecture: {RuntimeInformation.ProcessArchitecture}");
            sb.AppendLine("--------------------------------------------------");

            if (ex == null) {
                sb.AppendLine("No exception object details provided.");
            } else {
                int level = 0;
                Exception? curr = ex;
                while (curr != null) {
                    if (level > 0) sb.AppendLine($"\n--- Inner Exception [{level}] ---");
                    sb.AppendLine($"Type: {curr.GetType().FullName}");
                    sb.AppendLine($"Message: {curr.Message}");
                    sb.AppendLine("StackTrace:");
                    sb.AppendLine(curr.StackTrace ?? "  (No stack trace available)");
                    curr = curr.InnerException;
                    level++;
                }
            }
            sb.AppendLine("--------------------------------------------------");
            return sb.ToString();
        }

        public static void ShowReport(Exception ex) {
            ShowReport(FormatException(ex));
        }

        public static void ShowReport(string errorDetails) {
            string logPath = Path.Combine(Path.GetTempPath(), "panak_crash.log");
            try {
                File.AppendAllText(logPath, $"[{DateTime.Now}]\n{errorDetails}\n\n");
            } catch { }

            try {
                if (Application.Current != null && Application.Current.Dispatcher != null && !Application.Current.Dispatcher.HasShutdownStarted) {
                    if (Application.Current.Dispatcher.CheckAccess()) {
                        var win = new CrashReportWindow(errorDetails);
                        win.ShowDialog();
                        return;
                    } else {
                        Application.Current.Dispatcher.Invoke(() => {
                            var win = new CrashReportWindow(errorDetails);
                            win.ShowDialog();
                        });
                        return;
                    }
                }
            } catch { }

            // Dedicated STA thread fallback if WPF main dispatcher is broken or terminated
            try {
                var thread = new Thread(() => {
                    try {
                        var win = new CrashReportWindow(errorDetails);
                        win.ShowDialog();
                    } catch { }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = false;
                thread.Start();
                thread.Join();
            } catch { }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ButtonState == MouseButtonState.Pressed) {
                DragMove();
            }
        }

        private void CopyBtn_Click(object sender, RoutedEventArgs e) {
            try {
                Clipboard.SetText(_errorDetails);
                ShowStatus("✓ Պատճենված է սեղմատախտակին (Copied to clipboard)");
            } catch {
                ShowStatus("Սեղմատախտակը հասանելի չէ");
            }
        }

        private void OpenLogFileBtn_Click(object sender, RoutedEventArgs e) {
            try {
                string logPath = Path.Combine(Path.GetTempPath(), "panak_crash.log");
                if (!File.Exists(logPath)) {
                    File.WriteAllText(logPath, _errorDetails);
                }
                Process.Start(new ProcessStartInfo("notepad.exe", $"\"{logPath}\"") { UseShellExecute = true });
            } catch (Exception ex) {
                ShowStatus($"Ֆայլը բացել չհաջողվեց: {ex.Message}");
            }
        }

        private void SendEmailBtn_Click(object sender, RoutedEventArgs e) {
            try {
                // Ensure full error details are on clipboard so user can easily paste if body gets cut off
                try {
                    Clipboard.SetText(_errorDetails);
                } catch { }

                string subject = Uri.EscapeDataString($"Panak Crash Report v1.0.0 - {DateTime.Now:yyyy-MM-dd HH:mm}");
                string body = _errorDetails;
                if (body.Length > 1600) {
                    body = body.Substring(0, 1600) + "\n\n[... Truncated for email client. Complete log copied to clipboard! ...]";
                }
                string mailto = $"mailto:{SupportEmail}?subject={subject}&body={Uri.EscapeDataString(body)}";

                Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });
                ShowStatus("✓ Էլ․ փոստի ծրագիրը բացված է, լոգը պատճենված է սեղմատախտակին։");
            } catch (Exception ex) {
                ShowStatus($"Էլ․ փոստի հավելվածը բացել չհաջողվեց: {ex.Message}");
            }
        }

        private void RestartBtn_Click(object sender, RoutedEventArgs e) {
            try {
                string? exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) {
                    exePath = Process.GetCurrentProcess().MainModule?.FileName;
                }

                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath)) {
                    Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                }
            } catch { }

            try {
                Process.GetCurrentProcess().Kill();
            } catch {
                Close();
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        private void ShowStatus(string msg) {
            if (StatusMessageText != null) {
                StatusMessageText.Text = msg;
                StatusMessageText.Visibility = Visibility.Visible;
            }
        }
    }
}

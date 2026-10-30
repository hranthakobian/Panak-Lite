using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace Panak.Services {
    public static class ShellContextMenu {
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHELLEXECUTEINFO {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpVerb;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpFile;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpParameters;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpDirectory;
            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpClass;
            public IntPtr hkeyClass;
            public uint dwHotKey;
            public IntPtr hIcon;
            public IntPtr hProcess;
        }

        private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;
        private const int SW_SHOW = 5;

        public static void ShowProperties(string filePath) {
            try {
                SHELLEXECUTEINFO info = new SHELLEXECUTEINFO();
                info.cbSize = Marshal.SizeOf(info);
                info.lpVerb = "properties";
                info.lpFile = filePath;
                info.nShow = SW_SHOW;
                info.fMask = SEE_MASK_INVOKEIDLIST;
                ShellExecuteEx(ref info);
            } catch {
                // Fallback
            }
        }

        public static void OpenFile(string filePath) {
            try {
                Process.Start(new ProcessStartInfo {
                    FileName = filePath,
                    UseShellExecute = true
                });
            } catch (Exception ex) {
                MessageBox.Show(ex.Message, "Panak", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public static void ShowInExplorer(string filePath) {
            try {
                if (File.Exists(filePath) || Directory.Exists(filePath)) {
                    Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
            } catch {
                // Fallback
            }
        }

        public static void DeleteFile(string filePath) {
            try {
                if (File.Exists(filePath)) {
                    File.Delete(filePath);
                } else if (Directory.Exists(filePath)) {
                    Directory.Delete(filePath, true);
                }
            } catch (Exception ex) {
                MessageBox.Show(ex.Message, "Panak", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}

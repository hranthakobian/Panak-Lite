using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Panak.Services {
    public class FolderLockInfo {
        public string FolderPath { get; set; } = string.Empty;
        public bool IsLocked { get; set; } = false;
        public string LockType { get; set; } = "Password"; // "Password", "PIN", "Pattern"
        public string CredentialHash { get; set; } = string.Empty;
        public string CredentialSalt { get; set; } = string.Empty;
        public bool ThirdPartyProtectionEnabled { get; set; } = true;
        public DateTime LockedAt { get; set; } = DateTime.UtcNow;
    }

    public class SecurityStore {
        public Dictionary<string, FolderLockInfo> FolderLocks { get; set; } = new();
        public bool IsSettingsLocked { get; set; } = false;
        public string SettingsLockType { get; set; } = "Password";
        public string SettingsCredentialHash { get; set; } = string.Empty;
        public string SettingsCredentialSalt { get; set; } = string.Empty;
    }

    public sealed class FolderLockService {
        private static readonly Lazy<FolderLockService> _instance = new(() => new FolderLockService());
        public static FolderLockService Instance => _instance.Value;

        private readonly object _lock = new();
        private SecurityStore _store = new();
        private readonly string _securityFilePath;
        private readonly HashSet<string> _temporarilyUnlockedFolders = new(StringComparer.OrdinalIgnoreCase);

        public event Action? SecurityStateChanged;

        public FolderLockService() {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "Panak");
            try {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            } catch { }
            _securityFilePath = Path.Combine(dir, "security.json");
            LoadStore();
        }

        private void LoadStore() {
            lock (_lock) {
                try {
                    if (File.Exists(_securityFilePath)) {
                        string json = File.ReadAllText(_securityFilePath);
                        var loaded = JsonSerializer.Deserialize<SecurityStore>(json);
                        if (loaded != null) {
                            _store = loaded;
                        }
                    }
                } catch {
                    _store = new SecurityStore();
                }
            }
        }

        public void SaveStore() {
            lock (_lock) {
                try {
                    string json = JsonSerializer.Serialize(_store, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_securityFilePath, json);
                } catch { }
            }
            SecurityStateChanged?.Invoke();
        }

        // --- Folder Locking (Individual Per-Folder) ---
        public bool IsFolderLocked(string folderPath) {
            return false; // Disabled in Panak Lite
        }

        public FolderLockInfo? GetFolderLockInfo(string folderPath) {
            if (string.IsNullOrEmpty(folderPath)) return null;
            string key = NormalizePath(folderPath);
            lock (_lock) {
                if (_store.FolderLocks.TryGetValue(key, out var info)) {
                    return info;
                }
                return null;
            }
        }

        public void SetFolderLock(string folderPath, string lockType, string credential, List<string>? authorizedBiometrics = null) {
            if (string.IsNullOrEmpty(folderPath)) return;
            string key = NormalizePath(folderPath);

            var (salt, hash) = HashCredential(credential);

            lock (_lock) {
                var info = new FolderLockInfo {
                    FolderPath = folderPath,
                    IsLocked = true,
                    LockType = lockType,
                    CredentialSalt = salt,
                    CredentialHash = hash,
                    ThirdPartyProtectionEnabled = true,
                    LockedAt = DateTime.UtcNow
                };

                _store.FolderLocks[key] = info;
                _temporarilyUnlockedFolders.Remove(key);
                SaveStore();

                // Persist folder marker and apply protection
                WriteFolderLockMarker(folderPath, info);
                ApplyFolderProtection(folderPath, true);
            }
        }

        public bool RemoveFolderLock(string folderPath, string credential, string? biometricId = null) {
            if (string.IsNullOrEmpty(folderPath)) return false;
            string key = NormalizePath(folderPath);

            lock (_lock) {
                if (!_store.FolderLocks.TryGetValue(key, out var info) || !info.IsLocked) {
                    return true;
                }

                if (!VerifyCredential(info, credential, biometricId)) {
                    return false;
                }

                info.IsLocked = false;
                _store.FolderLocks.Remove(key);
                _temporarilyUnlockedFolders.Remove(key);
                SaveStore();

                // Remove marker and lift protection
                DeleteFolderLockMarker(folderPath);
                ApplyFolderProtection(folderPath, false);
                return true;
            }
        }

        public bool UnlockFolderSession(string folderPath, string credential, string? biometricId = null) {
            if (string.IsNullOrEmpty(folderPath)) return false;
            string key = NormalizePath(folderPath);

            lock (_lock) {
                if (!_store.FolderLocks.TryGetValue(key, out var info) || !info.IsLocked) {
                    _temporarilyUnlockedFolders.Add(key);
                    return true;
                }

                if (VerifyCredential(info, credential, biometricId)) {
                    _temporarilyUnlockedFolders.Add(key);
                    return true;
                }
                return false;
            }
        }

        public void RelockFolderSession(string folderPath) {
            if (string.IsNullOrEmpty(folderPath)) return;
            string key = NormalizePath(folderPath);
            lock (_lock) {
                _temporarilyUnlockedFolders.Remove(key);
            }
        }

        public bool VerifyCredential(FolderLockInfo info, string credential, string? biometricId = null) {
            if (info == null) return false;
            return CheckHash(credential, info.CredentialSalt, info.CredentialHash);
        }

        // --- Settings Lock ---
        public bool IsSettingsLocked {
            get {
                lock (_lock) return _store.IsSettingsLocked;
            }
            set {
                lock (_lock) {
                    _store.IsSettingsLocked = value;
                    SaveStore();
                }
            }
        }

        public string SettingsLockType {
            get {
                lock (_lock) return _store.SettingsLockType;
            }
            set {
                lock (_lock) {
                    _store.SettingsLockType = value;
                    SaveStore();
                }
            }
        }

        public void SetSettingsLock(string lockType, string credential, List<string>? authorizedBiometrics = null) {
            var (salt, hash) = HashCredential(credential);
            lock (_lock) {
                _store.IsSettingsLocked = true;
                _store.SettingsLockType = lockType;
                _store.SettingsCredentialSalt = salt;
                _store.SettingsCredentialHash = hash;
                SaveStore();
            }
        }

        public bool VerifySettingsUnlock(string credential, string? biometricId = null) {
            lock (_lock) {
                if (!_store.IsSettingsLocked) return true;
                return CheckHash(credential, _store.SettingsCredentialSalt, _store.SettingsCredentialHash);
            }
        }

        public bool RemoveSettingsLock(string credential, string? biometricId = null) {
            lock (_lock) {
                if (!_store.IsSettingsLocked) return true;
                if (!VerifySettingsUnlock(credential, biometricId)) return false;
                _store.IsSettingsLocked = false;
                SaveStore();
                return true;
            }
        }

        // --- System Uninstaller & Security Inspection ---
        public bool HasAnyLockedFolders() {
            lock (_lock) {
                return _store.FolderLocks.Values.Any(f => f.IsLocked && Directory.Exists(f.FolderPath));
            }
        }

        public List<string> GetLockedFolderPaths() {
            lock (_lock) {
                return _store.FolderLocks.Values
                    .Where(f => f.IsLocked && Directory.Exists(f.FolderPath))
                    .Select(f => f.FolderPath)
                    .ToList();
            }
        }

        public void ClearAllLocksForTesting() {
            lock (_lock) {
                _store.FolderLocks.Clear();
                _temporarilyUnlockedFolders.Clear();
                SaveStore();
            }
        }

        // --- Windows Third-Party Offline Protection & CLSID Marker ---
        private void WriteFolderLockMarker(string folderPath, FolderLockInfo info) {
            try {
                if (!Directory.Exists(folderPath)) return;
                string markerPath = Path.Combine(folderPath, "panak_lock.json");
                string json = JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(markerPath, json);
                File.SetAttributes(markerPath, FileAttributes.Hidden | FileAttributes.System);
            } catch { }
        }

        private void DeleteFolderLockMarker(string folderPath) {
            try {
                string markerPath = Path.Combine(folderPath, "panak_lock.json");
                if (File.Exists(markerPath)) {
                    File.SetAttributes(markerPath, FileAttributes.Normal);
                    File.Delete(markerPath);
                }
            } catch { }
        }

        public void ApplyFolderProtection(string folderPath, bool isLocked) {
            try {
                if (!Directory.Exists(folderPath)) return;
                string desktopIni = Path.Combine(folderPath, "desktop.ini");
                if (isLocked) {
                    // Disguise folder or lock properties in desktop.ini
                    if (File.Exists(desktopIni)) {
                        File.SetAttributes(desktopIni, FileAttributes.Normal);
                        string content = File.ReadAllText(desktopIni);
                        if (!content.Contains("PanakLocked=1")) {
                            content += "\r\n[PanakSecurity]\r\nPanakLocked=1\r\n";
                            File.WriteAllText(desktopIni, content);
                        }
                        File.SetAttributes(desktopIni, FileAttributes.Hidden | FileAttributes.System);
                    }
                } else {
                    if (File.Exists(desktopIni)) {
                        File.SetAttributes(desktopIni, FileAttributes.Normal);
                        string content = File.ReadAllText(desktopIni);
                        content = content.Replace("\r\n[PanakSecurity]\r\nPanakLocked=1\r\n", "");
                        content = content.Replace("[PanakSecurity]\r\nPanakLocked=1\r\n", "");
                        File.WriteAllText(desktopIni, content);
                        File.SetAttributes(desktopIni, FileAttributes.Hidden | FileAttributes.System);
                    }
                }
            } catch { }
        }

        // --- Cryptography & Helpers ---
        private static string NormalizePath(string path) {
            try {
                return Path.GetFullPath(path).TrimEnd('\\', '/').ToLowerInvariant();
            } catch {
                return path.TrimEnd('\\', '/').ToLowerInvariant();
            }
        }

        private static (string Salt, string Hash) HashCredential(string credential) {
            if (string.IsNullOrEmpty(credential)) {
                return (string.Empty, string.Empty);
            }
            byte[] saltBytes = RandomNumberGenerator.GetBytes(16);
            string salt = Convert.ToBase64String(saltBytes);
            string hash = ComputeHash(credential, saltBytes);
            return (salt, hash);
        }

        private static bool CheckHash(string credential, string salt, string expectedHash) {
            if (string.IsNullOrEmpty(credential) && string.IsNullOrEmpty(expectedHash)) return true;
            if (string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(expectedHash)) return false;
            try {
                byte[] saltBytes = Convert.FromBase64String(salt);
                string computed = ComputeHash(credential, saltBytes);
                return CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(computed),
                    Encoding.UTF8.GetBytes(expectedHash));
            } catch {
                return false;
            }
        }

        private static string ComputeHash(string credential, byte[] salt) {
            using var pbkdf2 = new Rfc2898DeriveBytes(credential, salt, 50000, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(32);
            return Convert.ToBase64String(hash);
        }
    }
}

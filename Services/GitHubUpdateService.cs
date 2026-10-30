using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Panak.Models;

namespace Panak.Services {
    public enum UpdateStatus {
        Idle,
        Checking,
        UpToDate,
        UpdateAvailable,
        Downloading,
        ReadyToInstall,
        Error
    }

    public class GitHubUpdateService : IDisposable {
        public const string TargetRepository = "hranthakobian/Panak";
        public const string TargetRepositoryUrl = "https://github.com/hranthakobian/Panak";

        private static GitHubUpdateService? _instance;
        public static GitHubUpdateService Instance => _instance ??= new GitHubUpdateService();

        public static string NormalizeRepo(string? repo) {
            if (string.IsNullOrWhiteSpace(repo)) {
                return TargetRepository;
            }
            repo = repo.Trim().Replace('\\', '/');
            if (repo.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)) {
                repo = repo.Substring("https://github.com/".Length);
            } else if (repo.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase)) {
                repo = repo.Substring("http://github.com/".Length);
            } else if (repo.StartsWith("github.com/", StringComparison.OrdinalIgnoreCase)) {
                repo = repo.Substring("github.com/".Length);
            }
            repo = repo.Trim('/');
            if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) {
                repo = repo.Substring(0, repo.Length - 4);
            }
            var parts = repo.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) {
                return $"{parts[0]}/{parts[1]}";
            }
            return TargetRepository;
        }

        private readonly HttpClient _httpClient;
        private System.Threading.Timer? _periodicTimer;
        private bool _isDisposed = false;

        public UpdateStatus Status { get; private set; } = UpdateStatus.Idle;
        public string CurrentVersionString { get; }
        public Version CurrentVersion { get; }
        public string? LatestVersionString { get; private set; }
        public Version? LatestVersion { get; private set; }
        public string? ReleaseTitle { get; private set; }
        public string? ReleaseNotes { get; private set; }
        public DateTime? PublishedAt { get; private set; }
        public string? DownloadUrl { get; private set; }
        public double DownloadProgress { get; private set; } = 0.0;
        public string? ErrorMessage { get; private set; }
        public string? DownloadedFilePath { get; private set; }

        public event Action<UpdateStatus, string?>? StatusChanged;
        public event Action<double>? DownloadProgressChanged;
        public event Action<string, string>? UpdateAvailableEvent;

        public GitHubUpdateService() {
            var asm = Assembly.GetExecutingAssembly();
            var ver = asm.GetName().Version;
            CurrentVersion = ver != null ? new Version(ver.Major, Math.Max(0, ver.Minor), Math.Max(0, ver.Build)) : new Version(1, 9, 0);
            CurrentVersionString = $"{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Panak-Updater", CurrentVersionString));
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public void Start() {
            if (_periodicTimer != null) {
                return;
            }

            // Initial check 10 seconds after launch, then every 60 minutes
            _periodicTimer = new System.Threading.Timer(_ => {
                if (SettingsService.Instance.Current.AutoCheckUpdates) {
                    _ = CheckForUpdatesAsync(manual: false);
                }
            }, null, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(60));
        }

        public async Task<bool> CheckForUpdatesAsync(bool manual = false) {
            if (Status == UpdateStatus.Checking || Status == UpdateStatus.Downloading) {
                return false;
            }

            SetStatus(UpdateStatus.Checking);
            ErrorMessage = null;

            string rawRepo = SettingsService.Instance.Current.GitHubRepo;
            string repo = string.IsNullOrWhiteSpace(rawRepo) ? TargetRepository : NormalizeRepo(rawRepo);

            try {
                string url = $"https://api.github.com/repos/{repo}/releases/latest";
                using var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode) {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (ProcessReleaseElement(root, repo)) {
                        return true;
                    }
                    SetStatus(UpdateStatus.UpToDate);
                    return false;
                }

                // If /releases/latest returned 404 (e.g. no official releases published yet, or only pre-releases)
                // Fallback 1: Check /releases array (catches pre-releases or recent releases)
                try {
                    string relUrl = $"https://api.github.com/repos/{repo}/releases";
                    using var relResp = await _httpClient.GetAsync(relUrl);
                    if (relResp.IsSuccessStatusCode) {
                        string relJson = await relResp.Content.ReadAsStringAsync();
                        using var relDoc = JsonDocument.Parse(relJson);
                        if (relDoc.RootElement.ValueKind == JsonValueKind.Array) {
                            foreach (var relItem in relDoc.RootElement.EnumerateArray()) {
                                if (ProcessReleaseElement(relItem, repo)) {
                                    return true;
                                }
                            }
                        }
                    }
                } catch { }

                // Fallback 2: Check /tags
                try {
                    string tagUrl = $"https://api.github.com/repos/{repo}/tags";
                    using var tagResp = await _httpClient.GetAsync(tagUrl);
                    if (tagResp.IsSuccessStatusCode) {
                        string tagJson = await tagResp.Content.ReadAsStringAsync();
                        using var tagDoc = JsonDocument.Parse(tagJson);
                        if (tagDoc.RootElement.ValueKind == JsonValueKind.Array) {
                            foreach (var tagItem in tagDoc.RootElement.EnumerateArray()) {
                                string rawName = tagItem.TryGetProperty("name", out var np) ? np.GetString() ?? "" : "";
                                if (TryParseVersion(rawName, out var parsedVer) && parsedVer > CurrentVersion) {
                                    string cleanVer = $"{parsedVer.Major}.{parsedVer.Minor}.{parsedVer.Build}";
                                    string candidateUrl = $"https://github.com/{repo}/releases/download/{rawName}/Panak-Installer-v{cleanVer}.zip";
                                    using var headReq = new HttpRequestMessage(HttpMethod.Head, candidateUrl);
                                    using var headResp = await _httpClient.SendAsync(headReq);
                                    if (headResp.IsSuccessStatusCode) {
                                        LatestVersion = parsedVer;
                                        LatestVersionString = cleanVer;
                                        ReleaseTitle = $"Panak v{cleanVer}";
                                        ReleaseNotes = $"Tag {rawName} published on GitHub.";
                                        DownloadUrl = candidateUrl;
                                        SaveLastCheckTime();
                                        SetStatus(UpdateStatus.UpdateAvailable);
                                        Application.Current?.Dispatcher.BeginInvoke(() => {
                                            UpdateAvailableEvent?.Invoke(LatestVersionString ?? "New", ReleaseNotes ?? "");
                                        });
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                } catch { }

                // Fallback 3: GitHub web redirect check (bypasses GitHub API 60 req/hr rate limits!)
                try {
                    using var handler = new HttpClientHandler { AllowAutoRedirect = false };
                    using var noRedirectClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
                    noRedirectClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Panak-Updater", CurrentVersionString));
                    using var redirectResp = await noRedirectClient.GetAsync($"https://github.com/{repo}/releases/latest");
                    if (redirectResp.StatusCode == System.Net.HttpStatusCode.Found ||
                        redirectResp.StatusCode == System.Net.HttpStatusCode.MovedPermanently ||
                        redirectResp.StatusCode == System.Net.HttpStatusCode.SeeOther) {
                        var location = redirectResp.Headers.Location;
                        if (location != null) {
                            string locStr = location.ToString();
                            int tagIndex = locStr.IndexOf("/releases/tag/", StringComparison.OrdinalIgnoreCase);
                            if (tagIndex >= 0) {
                                string tagName = locStr.Substring(tagIndex + "/releases/tag/".Length).Trim('/');
                                if (TryParseVersion(tagName, out var parsedVer) && parsedVer > CurrentVersion) {
                                    string cleanVer = $"{parsedVer.Major}.{parsedVer.Minor}.{parsedVer.Build}";
                                    string candidateUrl = $"https://github.com/{repo}/releases/download/{tagName}/Panak-Installer-v{cleanVer}.zip";
                                    LatestVersion = parsedVer;
                                    LatestVersionString = cleanVer;
                                    ReleaseTitle = $"Panak v{cleanVer}";
                                    ReleaseNotes = $"Latest release {tagName} detected on GitHub.";
                                    DownloadUrl = candidateUrl;
                                    SaveLastCheckTime();
                                    SetStatus(UpdateStatus.UpdateAvailable);
                                    Application.Current?.Dispatcher.BeginInvoke(() => {
                                        UpdateAvailableEvent?.Invoke(LatestVersionString ?? "New", ReleaseNotes ?? "");
                                    });
                                    return true;
                                }
                            }
                        }
                    }
                } catch { }

                SetStatus(UpdateStatus.UpToDate, "No new updates found.");
                SaveLastCheckTime();
                return false;
            } catch (Exception ex) {
                ErrorMessage = ex.Message;
                SetStatus(UpdateStatus.Error, ex.Message);
                return false;
            }
        }

        private bool ProcessReleaseElement(JsonElement root, string repo) {
            string rawTag = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
            ReleaseTitle = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : rawTag;
            ReleaseNotes = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : "";

            if (root.TryGetProperty("published_at", out var pubProp) && pubProp.TryGetDateTime(out var pubDate)) {
                PublishedAt = pubDate;
            }

            // Look for release assets (installer exe or zip)
            DownloadUrl = null;
            if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array) {
                // Priority 1: .exe installer or Panak-Installer zip
                foreach (var asset in assetsProp.EnumerateArray()) {
                    string assetName = asset.TryGetProperty("name", out var aName) ? aName.GetString() ?? "" : "";
                    string assetUrl = asset.TryGetProperty("browser_download_url", out var aUrl) ? aUrl.GetString() ?? "" : "";

                    if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                        (assetName.StartsWith("Panak-Installer", StringComparison.OrdinalIgnoreCase) && assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))) {
                        DownloadUrl = assetUrl;
                        break;
                    }
                }

                // Priority 2: Any other .zip asset if no specific installer asset found
                if (string.IsNullOrEmpty(DownloadUrl)) {
                    foreach (var asset in assetsProp.EnumerateArray()) {
                        string assetName = asset.TryGetProperty("name", out var aName) ? aName.GetString() ?? "" : "";
                        string assetUrl = asset.TryGetProperty("browser_download_url", out var aUrl) ? aUrl.GetString() ?? "" : "";

                        if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
                            DownloadUrl = assetUrl;
                            break;
                        }
                    }
                }
            }

            if (TryParseVersion(rawTag, out var parsedVersion)) {
                LatestVersion = parsedVersion;
                LatestVersionString = parsedVersion.Revision > 0
                    ? $"{parsedVersion.Major}.{parsedVersion.Minor}.{parsedVersion.Build}.{parsedVersion.Revision}"
                    : $"{parsedVersion.Major}.{parsedVersion.Minor}.{parsedVersion.Build}";
            } else {
                LatestVersion = new Version(1, 0, 0);
                LatestVersionString = rawTag;
            }

            SaveLastCheckTime();

            if (LatestVersion > CurrentVersion) {
                SetStatus(UpdateStatus.UpdateAvailable);
                Application.Current?.Dispatcher.BeginInvoke(() => {
                    UpdateAvailableEvent?.Invoke(LatestVersionString ?? "New", ReleaseNotes ?? "");
                });
                return true;
            }
            return false;
        }

        public static bool TryParseVersion(string verStr, out Version version) {
            if (string.IsNullOrWhiteSpace(verStr)) {
                version = new Version(1, 0, 0);
                return false;
            }

            // Matches "1.7.1", "panak1.7.1", "v1.7.1.0", "Panak-v1.7.1", etc.
            var match = System.Text.RegularExpressions.Regex.Match(verStr, @"(\d+)\.(\d+)(?:\.(\d+))?(?:\.(\d+))?");
            if (match.Success) {
                int major = int.Parse(match.Groups[1].Value);
                int minor = int.Parse(match.Groups[2].Value);
                if (match.Groups[4].Success) {
                    int build = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
                    int rev = int.Parse(match.Groups[4].Value);
                    version = new Version(major, minor, build, rev);
                } else if (match.Groups[3].Success) {
                    int build = int.Parse(match.Groups[3].Value);
                    version = new Version(major, minor, build);
                } else {
                    version = new Version(major, minor);
                }
                return true;
            }

            if (Version.TryParse(verStr.TrimStart('v', 'V').Trim(), out var v)) {
                version = v;
                return true;
            }

            version = new Version(1, 0, 0);
            return false;
        }

        public async Task<bool> DownloadUpdateAsync(Action<double>? progressCallback = null) {
            if (string.IsNullOrEmpty(DownloadUrl)) {
                SetStatus(UpdateStatus.Error, "No download link available for this release.");
                return false;
            }

            SetStatus(UpdateStatus.Downloading);
            DownloadProgress = 0.0;
            DownloadProgressChanged?.Invoke(0.0);

            try {
                string tempDir = Path.Combine(Path.GetTempPath(), "PanakUpdate");
                if (!Directory.Exists(tempDir)) {
                    Directory.CreateDirectory(tempDir);
                }

                string ext = Path.GetExtension(DownloadUrl);
                if (string.IsNullOrEmpty(ext) || ext.Length > 5) {
                    ext = ".exe";
                }
                string destFile = Path.Combine(tempDir, $"Panak_Update_{LatestVersionString ?? "latest"}{ext}");

                using (var response = await _httpClient.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead)) {
                    response.EnsureSuccessStatusCode();

                    long? totalBytes = response.Content.Headers.ContentLength;
                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    using var fileStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                    byte[] buffer = new byte[8192];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        if (totalBytes.HasValue && totalBytes.Value > 0) {
                            double pct = (double)totalRead / totalBytes.Value * 100.0;
                            DownloadProgress = Math.Round(pct, 1);
                            progressCallback?.Invoke(DownloadProgress);
                            DownloadProgressChanged?.Invoke(DownloadProgress);
                        }
                    }
                }

                var fileInfo = new FileInfo(destFile);
                if (!fileInfo.Exists || fileInfo.Length == 0) {
                    SetStatus(UpdateStatus.Error, "Downloaded update package is empty or corrupted.");
                    return false;
                }

                DownloadedFilePath = destFile;
                SetStatus(UpdateStatus.ReadyToInstall);
                return true;
            } catch (Exception ex) {
                ErrorMessage = ex.Message;
                SetStatus(UpdateStatus.Error, ex.Message);
                return false;
            }
        }

        public async Task<bool> DownloadAndApplyUpdateAsync(Action<double>? progressCallback = null) {
            bool downloaded = await DownloadUpdateAsync(progressCallback);
            if (!downloaded) {
                return false;
            }
            return ApplyDownloadedUpdate();
        }

        public bool ApplyDownloadedUpdate(string? filePath = null) {
            filePath ??= DownloadedFilePath;
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) {
                SetStatus(UpdateStatus.Error, "Downloaded update file not found.");
                return false;
            }

            try {
                string currentLang = LocalizationService.Instance.CurrentLanguage;
                if (filePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) {
                    ProcessStartInfo psi = new ProcessStartInfo {
                        FileName = filePath,
                        Arguments = $"--update --lang {currentLang}",
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                    Application.Current?.Dispatcher.BeginInvoke(() => {
                        Application.Current.Shutdown();
                    });
                    return true;
                } else if (filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) {
                    string tempDir = Path.GetDirectoryName(filePath)!;
                    string extractDir = Path.Combine(tempDir, $"Extracted_{DateTime.UtcNow.Ticks}");
                    Directory.CreateDirectory(extractDir);
                    ZipFile.ExtractToDirectory(filePath, extractDir);

                    string[] exeCandidates = Directory.GetFiles(extractDir, "*.exe", SearchOption.AllDirectories);
                    string? targetExe = exeCandidates.FirstOrDefault(e => Path.GetFileName(e).Equals("Setup.exe", StringComparison.OrdinalIgnoreCase))
                                     ?? exeCandidates.FirstOrDefault(e => Path.GetFileName(e).StartsWith("Panak", StringComparison.OrdinalIgnoreCase))
                                     ?? exeCandidates.FirstOrDefault();

                    if (!string.IsNullOrEmpty(targetExe)) {
                        ProcessStartInfo psi = new ProcessStartInfo {
                            FileName = targetExe,
                            Arguments = $"--update --lang {currentLang}",
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                        Application.Current?.Dispatcher.BeginInvoke(() => {
                            Application.Current.Shutdown();
                        });
                        return true;
                    } else {
                        SetStatus(UpdateStatus.Error, "Installer executable not found in update package.");
                        return false;
                    }
                } else {
                    SetStatus(UpdateStatus.Error, "Unsupported update package format.");
                    return false;
                }
            } catch (Exception ex) {
                SetStatus(UpdateStatus.Error, $"Failed to launch update: {ex.Message}");
                return false;
            }
        }

        private void SetStatus(UpdateStatus newStatus, string? message = null) {
            Status = newStatus;
            if (message != null) {
                ErrorMessage = message;
            }
            Application.Current?.Dispatcher.BeginInvoke(() => {
                StatusChanged?.Invoke(newStatus, message ?? ErrorMessage);
            });
        }

        private static void SaveLastCheckTime() {
            try {
                SettingsService.Instance.Current.LastUpdateCheck = DateTime.UtcNow;
                SettingsService.Instance.SaveSettings();
            } catch { }
        }

        public void Dispose() {
            if (_isDisposed) {
                return;
            }
            _isDisposed = true;
            _periodicTimer?.Dispose();
            _periodicTimer = null;
            _httpClient?.Dispose();
        }
    }
}

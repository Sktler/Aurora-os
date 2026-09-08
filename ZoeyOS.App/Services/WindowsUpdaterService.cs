using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
{
    public sealed class UpdateCheckResult
    {
        public bool UpdateAvailable { get; set; }
        public bool IsError { get; set; }
        public string CurrentVersion { get; set; } = "";
        public string LatestVersion { get; set; } = "";
        public string? ReleaseUrl { get; set; }
        public string? DownloadUrl { get; set; }
        public string? ChecksumUrl { get; set; }
        public string? ReleaseNotes { get; set; }
        public string? Error { get; set; }
    }

    public sealed class UpdateDownloadResult
    {
        public bool Succeeded { get; set; }
        public string? PackagePath { get; set; }
        public string? StagingPath { get; set; }
        public string? LatestVersion { get; set; }
        public string? Error { get; set; }
    }

    public sealed class WindowsUpdaterService
    {
        private const string DefaultUserAgent = "AuroraWindowsUpdater";
        private static readonly HttpClient HttpClient = new();

        public string RepositoryOwner { get; }
        public string RepositoryName { get; }

        public WindowsUpdaterService(string repositoryOwner = "Sktler", string repositoryName = "Aurora-os")
        {
            RepositoryOwner = string.IsNullOrWhiteSpace(repositoryOwner) ? "Sktler" : repositoryOwner.Trim();
            RepositoryName = string.IsNullOrWhiteSpace(repositoryName) ? "Aurora-os" : repositoryName.Trim();
        }

        public static string DefaultUpdateRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aurora",
            "updates");

        public static Version CurrentVersion =>
            Assembly.GetEntryAssembly()?.GetName().Version ??
            typeof(App).Assembly.GetName().Version ??
            new Version(0, 0, 0);

        public static string CurrentVersionString => CurrentVersion.ToString();

        public static string NormalizeVersionString(string? versionText)
        {
            if (string.IsNullOrWhiteSpace(versionText))
                return "0.0.0";

            var trimmed = versionText.Trim();
            if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(1);

            var match = Regex.Match(trimmed, @"\d+(?:\.\d+)+");
            return match.Success ? match.Value : trimmed;
        }

        public static bool TryParseVersion(string? versionText, out Version version)
        {
            var normalized = NormalizeVersionString(versionText);
            return Version.TryParse(normalized, out version);
        }

        public static bool IsVersionGreater(string? currentVersionText, string? candidateVersionText)
        {
            if (!TryParseVersion(currentVersionText, out var currentVersion))
                currentVersion = new Version(0, 0, 0);

            if (!TryParseVersion(candidateVersionText, out var candidateVersion))
                return false;

            return candidateVersion > currentVersion;
        }

        public static UpdateCheckResult ParseReleasePayload(string jsonPayload, string currentVersionText)
        {
            if (string.IsNullOrWhiteSpace(jsonPayload))
            {
                return new UpdateCheckResult
                {
                    CurrentVersion = currentVersionText,
                    LatestVersion = currentVersionText,
                    IsError = true,
                    Error = "GitHub release response was empty."
                };
            }

            try
            {
                using var document = JsonDocument.Parse(jsonPayload);
                var root = document.RootElement;

                var tagName = root.TryGetProperty("tag_name", out var tagNode) ? tagNode.GetString() : null;
                var name = root.TryGetProperty("name", out var nameNode) ? nameNode.GetString() : null;
                var releaseNotes = root.TryGetProperty("body", out var bodyNode) ? bodyNode.GetString() : null;
                var releaseUrl = root.TryGetProperty("html_url", out var htmlNode) ? htmlNode.GetString() : null;
                var releaseVersionText = string.IsNullOrWhiteSpace(tagName) ? name : tagName;

                var latestVersionText = NormalizeVersionString(string.IsNullOrWhiteSpace(releaseVersionText) ? currentVersionText : releaseVersionText);
                var downloadUrl = SelectPreferredAssetUrl(root);
                var checksumUrl = SelectPreferredChecksumUrl(root);
                var updateAvailable = IsVersionGreater(currentVersionText, latestVersionText);

                return new UpdateCheckResult
                {
                    CurrentVersion = currentVersionText,
                    LatestVersion = latestVersionText,
                    ReleaseUrl = releaseUrl,
                    DownloadUrl = downloadUrl,
                    ChecksumUrl = checksumUrl,
                    ReleaseNotes = releaseNotes,
                    UpdateAvailable = updateAvailable,
                    Error = updateAvailable ? null : "Aurora is already up to date."
                };
            }
            catch (Exception ex)
            {
                return new UpdateCheckResult
                {
                    CurrentVersion = currentVersionText,
                    LatestVersion = currentVersionText,
                    IsError = true,
                    Error = ex.Message
                };
            }
        }

        public static string? SelectPreferredAssetUrl(JsonElement root)
        {
            if (!root.TryGetProperty("assets", out var assetsElement) || assetsElement.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var asset in assetsElement.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameElement) || !asset.TryGetProperty("browser_download_url", out var urlElement))
                    continue;

                var assetName = nameElement.GetString() ?? "";
                var assetUrl = urlElement.GetString();
                if (!IsHttpsUrl(assetUrl))
                    continue;

                var lowered = assetName.ToLowerInvariant();
                if (lowered.Contains("sha256") || lowered.Contains("checksum") || lowered.Contains("signature") || lowered.Contains("manifest"))
                    continue;

                if (IsWindowsPackageCandidate(lowered))
                    return assetUrl;
            }

            return null;
        }

        private static bool IsHttpsUrl(string? value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                   uri.Scheme == Uri.UriSchemeHttps;
        }

        public static bool IsWindowsPackageCandidate(string assetNameLower)
        {
            return assetNameLower.Contains(".zip") ||
                   assetNameLower.Contains(".msi") ||
                   assetNameLower.Contains(".exe") ||
                   assetNameLower.Contains("windows") ||
                   assetNameLower.Contains("portable");
        }

        public static string? SelectPreferredChecksumUrl(JsonElement root)
        {
            if (!root.TryGetProperty("assets", out var assetsElement) || assetsElement.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var asset in assetsElement.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameElement) ||
                    !asset.TryGetProperty("browser_download_url", out var urlElement))
                    continue;

                var name = nameElement.GetString() ?? "";
                var url = urlElement.GetString();
                var lowered = name.ToLowerInvariant();
                if (IsHttpsUrl(url) &&
                    (lowered.EndsWith(".sha256") || lowered.Contains("checksum")))
                    return url;
            }

            return null;
        }

        public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var latestReleaseUrl = $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest";
                using var request = new HttpRequestMessage(HttpMethod.Get, latestReleaseUrl);
                request.Headers.Add("User-Agent", DefaultUserAgent);

                using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    return new UpdateCheckResult
                    {
                        CurrentVersion = CurrentVersionString,
                        LatestVersion = CurrentVersionString,
                        IsError = true,
                        Error = $"GitHub responded with {(int)response.StatusCode} ({response.ReasonPhrase})."
                    };
                }

                return ParseReleasePayload(payload, CurrentVersionString);
            }
            catch (Exception ex)
            {
                return new UpdateCheckResult
                {
                    CurrentVersion = CurrentVersionString,
                    LatestVersion = CurrentVersionString,
                    IsError = true,
                    Error = ex.Message
                };
            }
        }

        public async Task<UpdateDownloadResult> DownloadAndPrepareUpdateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var check = await CheckForUpdateAsync(cancellationToken).ConfigureAwait(false);
                if (check.IsError || !check.UpdateAvailable || string.IsNullOrWhiteSpace(check.DownloadUrl))
                {
                    return new UpdateDownloadResult
                    {
                        Succeeded = false,
                        LatestVersion = check.LatestVersion,
                        Error = check.Error ?? "No update package was available."
                    };
                }

                var packageDir = DefaultUpdateRoot;
                Directory.CreateDirectory(packageDir);
                var packageName = BuildPackageFileName(check.LatestVersion, check.DownloadUrl);
                var packagePath = Path.Combine(packageDir, packageName);

                using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, check.DownloadUrl);
                downloadRequest.Headers.Add("User-Agent", DefaultUserAgent);

                using var downloadResponse = await HttpClient.SendAsync(downloadRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (!downloadResponse.IsSuccessStatusCode)
                {
                    return new UpdateDownloadResult
                    {
                        Succeeded = false,
                        LatestVersion = check.LatestVersion,
                        Error = $"Download failed with {(int)downloadResponse.StatusCode} ({downloadResponse.ReasonPhrase})."
                    };
                }

                await using (var destination = File.Create(packagePath))
                {
                    await downloadResponse.Content.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                }

                if (!string.IsNullOrWhiteSpace(check.ChecksumUrl))
                {
                    using var checksumRequest = new HttpRequestMessage(HttpMethod.Get, check.ChecksumUrl);
                    checksumRequest.Headers.Add("User-Agent", DefaultUserAgent);
                    using var checksumResponse = await HttpClient.SendAsync(
                        checksumRequest,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken).ConfigureAwait(false);
                    if (!checksumResponse.IsSuccessStatusCode)
                    {
                        File.Delete(packagePath);
                        return new UpdateDownloadResult
                        {
                            Succeeded = false,
                            LatestVersion = check.LatestVersion,
                            Error = $"Checksum download failed with {(int)checksumResponse.StatusCode} ({checksumResponse.ReasonPhrase})."
                        };
                    }

                    var checksumText = await checksumResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    if (!await VerifySha256Async(packagePath, checksumText, cancellationToken).ConfigureAwait(false))
                    {
                        File.Delete(packagePath);
                        return new UpdateDownloadResult
                        {
                            Succeeded = false,
                            LatestVersion = check.LatestVersion,
                            Error = "Downloaded update failed SHA-256 validation."
                        };
                    }
                }

                var stagingPath = PrepareUpdateFolder(packagePath);

                return new UpdateDownloadResult
                {
                    Succeeded = true,
                    PackagePath = packagePath,
                    StagingPath = stagingPath,
                    LatestVersion = check.LatestVersion
                };
            }
            catch (Exception ex)
            {
                return new UpdateDownloadResult
                {
                    Succeeded = false,
                    LatestVersion = CurrentVersionString,
                    Error = ex.Message
                };
            }
        }

        public static string BuildPackageFileName(string latestVersion, string? downloadUrl)
        {
            var sanitizedVersion = NormalizeVersionString(latestVersion).Replace(".", "-");
            var extension = ".zip";
            if (!string.IsNullOrWhiteSpace(downloadUrl))
            {
                var lower = downloadUrl.ToLowerInvariant();
                if (lower.EndsWith(".msi", StringComparison.Ordinal))
                    extension = ".msi";
                else if (lower.EndsWith(".exe", StringComparison.Ordinal))
                    extension = ".exe";
            }

            return $"Aurora-{sanitizedVersion}{extension}";
        }

        public static string PrepareUpdateFolder(string packagePath)
        {
            if (File.Exists(packagePath) && packagePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return StageZipPackage(packagePath, Path.GetFileNameWithoutExtension(packagePath));

            return Path.Combine(DefaultUpdateRoot, "staged");
        }

        public static string StageZipPackage(string packagePath, string version)
        {
            var stageRoot = Path.Combine(DefaultUpdateRoot, "staged");
            var safeVersion = NormalizeVersionString(version).Replace(".", "-");
            var temporaryPath = Path.Combine(stageRoot, $"{safeVersion}.tmp");
            var finalPath = Path.Combine(stageRoot, safeVersion);

            Directory.CreateDirectory(stageRoot);
            if (Directory.Exists(temporaryPath))
                Directory.Delete(temporaryPath, recursive: true);
            if (Directory.Exists(finalPath))
                Directory.Delete(finalPath, recursive: true);

            ZipFile.ExtractToDirectory(packagePath, temporaryPath);
            Directory.Move(temporaryPath, finalPath);
            return finalPath;
        }

        public static async Task<bool> VerifySha256Async(
            string packagePath,
            string checksumText,
            CancellationToken cancellationToken = default)
        {
            var expectedHash = checksumText
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(expectedHash) || expectedHash.Length != 64)
                return false;

            await using var stream = File.OpenRead(packagePath);
            using var sha256 = SHA256.Create();
            var actualBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
            var actualHash = Convert.ToHexString(actualBytes);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        public static string CreateRestartScript(string stagingPath)
        {
            var updatesRoot = DefaultUpdateRoot;
            Directory.CreateDirectory(updatesRoot);

            var scriptPath = Path.Combine(updatesRoot, "apply-update.cmd");
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var exePath = Path.Combine(appDirectory, "Aurora.exe");

            var script = new StringBuilder();
            script.AppendLine("@echo off");
            script.AppendLine("setlocal");
            script.AppendLine("echo Waiting for Aurora to close before applying the update...");
            script.AppendLine("ping 127.0.0.1 -n 3 > nul");
            script.AppendLine($"if exist \"{exePath}\" taskkill /F /IM Aurora.exe > nul 2>&1");
            script.AppendLine("ping 127.0.0.1 -n 3 > nul");
            script.AppendLine($"robocopy \"{stagingPath}\" \"{appDirectory}\" /E /NFL /NDL /NJH /NJS > nul");
            script.AppendLine("set COPY_RESULT=%ERRORLEVEL%");
            script.AppendLine("if %COPY_RESULT% GTR 7 (");
            script.AppendLine("  echo Aurora update failed with robocopy code %COPY_RESULT%.");
            script.AppendLine("  exit /b %COPY_RESULT%");
            script.AppendLine(")");
            script.AppendLine($"start \"\" \"{exePath}\"");
            script.AppendLine("exit /b 0");

            File.WriteAllText(scriptPath, script.ToString());
            return scriptPath;
        }

        public static void LaunchRestartScript(string scriptPath)
        {
            if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
                return;

            ProcessStartInfo startInfo = new(scriptPath)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? Environment.CurrentDirectory
            };

            System.Diagnostics.Process.Start(startInfo);
        }
    }
}

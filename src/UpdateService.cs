using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WindowMinimize;

/// <summary>
/// A newer release published on GitHub, together with the asset matching this build.
/// </summary>
internal sealed record UpdateInfo(
    string Version,
    string Tag,
    string ReleaseUrl,
    string AssetName,
    string DownloadUrl,
    string? ChecksumsUrl
);

/// <summary>
/// Checks the GitHub release feed and installs the asset that matches the running full or lite
/// build. The updater is a detached PowerShell helper that waits for this process to exit.
/// </summary>
internal static class UpdateService {
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/invefa/WindowMinimize/releases/latest";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient() {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"WindowMinimize/{AppInfo.Version}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    /// <summary>Return the newer release, or null when this build is current.</summary>
    internal static async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken) {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
        using HttpResponseMessage response = await Http.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken
        );
        if (!response.IsSuccessStatusCode) {
            throw new InvalidOperationException(
                $"GitHub returned HTTP {(int)response.StatusCode} for the release feed"
            );
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument document = await JsonDocument.ParseAsync(
            stream, cancellationToken: cancellationToken
        );
        JsonElement root = document.RootElement;

        if (root.TryGetProperty("prerelease", out JsonElement prerelease)
            && prerelease.ValueKind == JsonValueKind.True) {
            return null;
        }

        string tag = GetString(root, "tag_name").Trim();
        Version? latest = ParseVersion(tag);
        Version? current = ParseVersion(AppInfo.Version);
        if (latest is null || current is null || latest <= current) return null;

        bool wantLite = IsLiteBuild();
        string? assetName = null;
        string? downloadUrl = null;
        string? checksumsUrl = null;

        if (root.TryGetProperty("assets", out JsonElement assets)
            && assets.ValueKind == JsonValueKind.Array) {
            foreach (JsonElement asset in assets.EnumerateArray()) {
                string name = GetString(asset, "name");
                if (name.Contains("SHA256SUMS", StringComparison.OrdinalIgnoreCase)) {
                    checksumsUrl = GetString(asset, "browser_download_url");
                    continue;
                }

                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    || !name.Contains("win-x64", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                bool assetLite = name.Contains("lite", StringComparison.OrdinalIgnoreCase);
                if (assetLite != wantLite) continue;
                assetName = name;
                downloadUrl = GetString(asset, "browser_download_url");
                break;
            }
        }

        if (assetName is null || downloadUrl is null) {
            throw new InvalidOperationException(
                $"release {tag} has no matching win-x64 asset"
            );
        }

        return new UpdateInfo(
            latest.ToString(3), tag, GetString(root, "html_url"),
            assetName, downloadUrl, checksumsUrl
        );
    }

    /// <summary>
    /// Download the release asset and verify its SHA-256 checksum when one is published.
    /// </summary>
    internal static async Task<string> DownloadAsync(
        UpdateInfo update, CancellationToken cancellationToken
    ) {
        string directory = UpdateDirectory();
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, update.AssetName);
        string temporary = path + ".part";

        using HttpResponseMessage response = await Http.GetAsync(
            update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken
        );
        response.EnsureSuccessStatusCode();
        await using (Stream input = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var output = new FileStream(
            temporary, FileMode.Create, FileAccess.Write, FileShare.None
        )) {
            await input.CopyToAsync(output, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(update.ChecksumsUrl)) {
            string expected = await FindChecksumAsync(
                update.ChecksumsUrl!, update.AssetName, cancellationToken
            );
            if (expected.Length > 0) {
                string actual = await ComputeSha256Async(temporary, cancellationToken);
                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) {
                    File.Delete(temporary);
                    throw new InvalidOperationException(
                        $"checksum mismatch for {update.AssetName}"
                    );
                }
            }
        }

        File.Move(temporary, path, overwrite: true);
        return path;
    }

    /// <summary>
    /// Launch a detached helper that waits for this process, replaces the exe and restarts it.
    /// </summary>
    internal static bool StartUpdater(string downloadedPath, out string error) {
        error = string.Empty;
        string? target = Environment.ProcessPath;
        if (string.IsNullOrEmpty(target) || !File.Exists(target)) {
            error = "the running executable path is not available";
            return false;
        }

        string updaterDirectory = Path.Combine(UpdateDirectory(), "updater");
        Directory.CreateDirectory(updaterDirectory);
        string scriptPath = Path.Combine(updaterDirectory, "apply.ps1");
        string script = """
            param([int]$waitFor, [string]$target, [string]$newFile)
            $deadline = (Get-Date).AddSeconds(60)
            while ((Get-Date) -lt $deadline) {
                if (-not (Get-Process -Id $waitFor -ErrorAction SilentlyContinue)) { break }
                Start-Sleep -Milliseconds 200
            }
            $log = Join-Path $env:LOCALAPPDATA 'WindowMinimize\update.log'
            try {
                Copy-Item -LiteralPath $newFile -Destination $target -Force
                Add-Content -LiteralPath $log -Value "updated $target"
                Start-Process -FilePath $target
            } catch {
                Add-Content -LiteralPath $log -Value ("update failed: " + $_)
            } finally {
                Remove-Item -LiteralPath $newFile -Force -ErrorAction SilentlyContinue
            }
            """;
        try {
            File.WriteAllText(scriptPath, script, new UTF8Encoding(false));
            string powershell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "System32", "WindowsPowerShell", "v1.0", "powershell.exe"
            );
            var startInfo = new ProcessStartInfo(powershell) {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = updaterDirectory,
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add("-waitFor");
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
            startInfo.ArgumentList.Add("-target");
            startInfo.ArgumentList.Add(target);
            startInfo.ArgumentList.Add("-newFile");
            startInfo.ArgumentList.Add(downloadedPath);
            Process.Start(startInfo);
            return true;
        } catch (Exception ex) {
            error = ex.Message;
            return false;
        }
    }

    private static async Task<string> FindChecksumAsync(
        string url, string assetName, CancellationToken cancellationToken
    ) {
        string text = await Http.GetStringAsync(url, cancellationToken);
        foreach (string line in text.Split('\n')) {
            string[] parts = line.Trim().Split(
                (char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries
            );
            if (parts.Length != 2) continue;
            string name = parts[1].Trim().TrimStart('*').Trim();
            if (name.Equals(assetName, StringComparison.OrdinalIgnoreCase)) {
                return parts[0].Trim();
            }
        }

        return string.Empty;
    }

    private static async Task<string> ComputeSha256Async(
        string path, CancellationToken cancellationToken
    ) {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read
        );
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static string UpdateDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowMinimize", "updates"
    );

    private static bool IsLiteBuild() {
        string name = typeof(UpdateService).Assembly.GetName().Name ?? string.Empty;
        string process = Path.GetFileNameWithoutExtension(
            Environment.ProcessPath ?? string.Empty
        );
        return name.Contains("lite", StringComparison.OrdinalIgnoreCase)
            || process.Contains("lite", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static Version? ParseVersion(string value) {
        string clean = value.Trim().TrimStart('v', 'V');
        return Version.TryParse(clean, out Version? version) ? version : null;
    }
}

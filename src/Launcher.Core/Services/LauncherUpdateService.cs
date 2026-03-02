using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Launcher.Core.Services;

/// <summary>
/// Checks for a newer Launcher.exe on GitHub Releases and applies the update via a swap script.
/// </summary>
public static class LauncherUpdateService
{
    private const string GitHubOwner = "kixrababyy";
    private const string GitHubRepo  = "ToyBattles-Launcher";

    private static string ApiUrl      => $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
    private static string DownloadUrl => $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest/download/Launcher.exe";

    /// <summary>
    /// Fetches the latest GitHub Release tag and compares it to the running assembly version.
    /// </summary>
    /// <returns>
    /// <c>needsUpdate = true</c> when the remote version is strictly greater than the current one.
    /// Returns <c>false</c> on any network or parse failure so startup is never blocked.
    /// </returns>
    public static async Task<(bool needsUpdate, Version? remoteVersion)> CheckAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            // GitHub API requires a User-Agent header
            client.DefaultRequestHeaders.Add("User-Agent", "ToyBattlesLauncher");

            var json = await client.GetStringAsync(ApiUrl);
            using var doc = JsonDocument.Parse(json);

            // tag_name is typically "v1.2.3" — strip the leading 'v' if present
            var tag = doc.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "";

            var current = Assembly.GetEntryAssembly()?.GetName().Version;
            if (current == null || !Version.TryParse(tag, out var remote))
                return (false, null);

            LogService.Log($"Launcher version check — current: {current}, remote: {remote}");
            return (remote > current, remote);
        }
        catch (Exception ex)
        {
            LogService.Log($"Launcher version check failed (non-fatal): {ex.Message}");
            return (false, null);
        }
    }

    /// <summary>
    /// Downloads the new <c>Launcher.exe</c> from the latest GitHub Release, writes a swap batch script to %TEMP%,
    /// and starts the script. The caller must call <c>Application.Current.Shutdown()</c>
    /// immediately after this returns so the bat can replace the running exe.
    /// </summary>
    public static async Task DownloadAndApplyAsync(
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        // Current exe path — this is what will be replaced
        var currentExe = Process.GetCurrentProcess().MainModule?.FileName
                         ?? Path.Combine(AppContext.BaseDirectory, "Launcher.exe");

        var tempExe = Path.Combine(Path.GetTempPath(), "Launcher_new.exe");
        var batPath = Path.Combine(Path.GetTempPath(), "LauncherUpdate.bat");

        // Download new exe to temp location
        var downloader = new DownloadService();
        var ok = await downloader.DownloadFileAsync(DownloadUrl, tempExe, progress, ct);

        if (!ok)
            throw new Exception("Failed to download launcher update after multiple retries.");

        // Write swap bat — waits 2 s for the current process to fully exit,
        // moves the new exe over the old one, relaunches, then self-deletes.
        var bat = $"""
            @echo off
            timeout /t 2 /nobreak > nul
            move /y "{tempExe}" "{currentExe}"
            start "" "{currentExe}"
            del "%~f0"
            """;

        await File.WriteAllTextAsync(batPath, bat, ct);

        // Start bat minimised so there is no visible console flash
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c start /min \"\" \"{batPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        });

        LogService.Log($"Launcher update script started. Replacing with {tempExe}.");
    }
}

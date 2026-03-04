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
    public  static string DownloadUrl => $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest/download/Launcher.exe";
    public  static string ReleasesUrl => $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest";

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

            // Ignore dev/debug builds explicitly set to 1.0.0.0 or 0.0.0.0
            if (current.Major == 1 && current.Minor == 0 && current.Build == 0 && current.Revision <= 0 ||
                current.Major == 0 && current.Minor == 0 && current.Build == 0 && current.Revision <= 0)
            {
                LogService.Log("Launcher version check skipped — running a dev/debug build.");
                return (false, null);
            }

            // Normalize missing revision numbers (e.g. 1.0.7 parsed as 1.0.7.-1)
            var normalizedRemote = new Version(remote.Major, remote.Minor, Math.Max(0, remote.Build), Math.Max(0, remote.Revision));
            var normalizedCurrent = new Version(current.Major, current.Minor, Math.Max(0, current.Build), Math.Max(0, current.Revision));

            LogService.Log($"Launcher version check — current: {normalizedCurrent}, remote: {normalizedRemote}");
            return (normalizedRemote > normalizedCurrent, remote);
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

        // Write swap bat — waits for the Launcher process to fully exit (retry loop),
        // moves the new exe over the old one, relaunches, then self-deletes.
        var exeName = Path.GetFileNameWithoutExtension(currentExe); // e.g. "Launcher"
        var bat = $"""
            @echo off
            :wait
            tasklist /fi "imagename eq {exeName}.exe" 2>nul | find /i "{exeName}.exe" >nul
            if not errorlevel 1 (
                timeout /t 1 /nobreak > nul
                goto wait
            )
            timeout /t 1 /nobreak > nul
            :retry
            move /y "{tempExe}" "{currentExe}" >nul 2>&1
            if errorlevel 1 (
                timeout /t 2 /nobreak > nul
                goto retry
            )
            start "" "{currentExe}"
            del "%~f0"
            """;

        await File.WriteAllTextAsync(batPath, bat, ct);

        // Run the swap bat directly via the shell — simpler and more reliable than
        // nesting inside "cmd /c start /min", which can fail with "not enough memory"
        Process.Start(new ProcessStartInfo
        {
            FileName = batPath,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Minimized
        });

        LogService.Log($"Launcher update script started. Replacing with {tempExe}.");
    }
}

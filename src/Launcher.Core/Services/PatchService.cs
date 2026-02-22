using System.Diagnostics;
using Launcher.Core.Config;
using Launcher.Core.Models;

namespace Launcher.Core.Services;

/// <summary>
/// Orchestrates the update/patch pipeline:
/// 1. Fetch remote patch.ini
/// 2. Compare remote vs installed version
/// 3. Download .cab patch files
/// 4. Extract with expand.exe to staging dir
/// 5. Swap .new files into GameRoot
/// 6. Update local state
/// </summary>
public class PatchService
{
    private readonly DownloadService _downloadService = new();

    /// <summary>
    /// Check remote version and determine if an update is needed.
    /// Returns the remote PatchConfig, or null on failure.
    /// </summary>
    public async Task<PatchConfig?> CheckForUpdateAsync(
        string updateBaseUrl,
        CancellationToken ct = default)
    {
        var remoteUrl = $"{updateBaseUrl}/microvolts/patch.ini";
        LogService.Log($"Checking for updates from: {remoteUrl}");

        var content = await _downloadService.DownloadStringAsync(remoteUrl, ct);
        if (content == null)
        {
            LogService.LogError($"Error downloading string from {remoteUrl}");
            return null;
        }

        return PatchConfig.LoadFromContent(content);
    }

    /// <summary>
    /// Determine if the installed version is behind the remote version.
    /// </summary>
    public static bool NeedsUpdate(GameVersion installed, GameVersion remote)
    {
        if (installed.IsEmpty) return true;
        return remote > installed;
    }

    /// <summary>
    /// Find the single-step upgrade path from installed to latest,
    /// matching the existing behavior of direct version-to-version patches.
    /// </summary>
    public static List<(GameVersion From, GameVersion To)> GetUpgradePath(
        GameVersion installed, PatchConfig remotePatch)
    {
        var steps = new List<(GameVersion From, GameVersion To)>();

        // The existing launcher patches one step at a time:
        // installed → next available version → ... → latest
        // Looking at the log, it goes ENG_2.0.4.1 → ENG_2.0.4.2 → ENG_2.0.4.3
        // The remote patch.ini lists all known versions.
        // Strategy: sort all versions, find installed, step through each until latest.

        var allVersions = remotePatch.AllVersions
            .Where(v => !v.IsEmpty)
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        var current = installed;
        var latest = remotePatch.LatestVersion;

        // Find all versions strictly greater than installed, up to and including latest
        var upgradeVersions = allVersions
            .Where(v => v > current && v <= latest)
            .OrderBy(v => v)
            .ToList();

        foreach (var target in upgradeVersions)
        {
            steps.Add((current, target));
            current = target;
        }

        // If no intermediate steps found but we still need to update,
        // try a direct jump
        if (steps.Count == 0 && NeedsUpdate(installed, latest))
        {
            steps.Add((installed, latest));
        }

        return steps;
    }

    /// <summary>
    /// Execute the full patch pipeline.
    /// </summary>
    public async Task<bool> ApplyUpdateAsync(
        string gameRootPath,
        string updateBaseUrl,
        GameVersion installedVersion,
        PatchConfig remotePatch,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var steps = GetUpgradePath(installedVersion, remotePatch);
        if (steps.Count == 0)
        {
            LogService.Log("No upgrade steps needed.");
            return true;
        }

        LogService.Log($"New version available, applying step-by-step patch from {installedVersion} to {remotePatch.LatestVersion}");

        foreach (var (from, to) in steps)
        {
            LogService.Log($"Starting step-by-step patch from {from} to {to}");

            var success = await ApplySinglePatchAsync(gameRootPath, updateBaseUrl, from, to, progress, ct);
            if (!success)
            {
                LogService.LogError($"Failed to apply patch from {from} to {to}");
                return false;
            }

            LogService.Log($"Successfully applied patch from {from} to {to}");
        }

        // Re-download the remote patch.ini to the local game root
        await DownloadPatchIniToLocal(gameRootPath, updateBaseUrl, ct);

        return true;
    }

    private async Task<bool> ApplySinglePatchAsync(
        string gameRootPath,
        string updateBaseUrl,
        GameVersion from,
        GameVersion to,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        // URL pattern: {base}/microvolts/{toVersion}/microvolts-{fromVersion}-{toVersion}.cab
        var cabUrl = $"{updateBaseUrl}/microvolts/{to}/microvolts-{from}-{to}.cab";

        // Create temp staging directory
        var stagingDir = Path.Combine(
            Path.GetTempPath(),
            $"LauncherPatch_{Guid.NewGuid()}");
        Directory.CreateDirectory(stagingDir);

        try
        {
            var cabPath = Path.Combine(stagingDir, "patch.cab");

            // 1. Download the .cab file
            progress?.Report(new DownloadProgress { StatusText = $"Downloading patch {from} → {to}..." });
            var downloaded = await _downloadService.DownloadFileAsync(cabUrl, cabPath, progress, ct);
            if (!downloaded)
                return false;

            LogService.Log($"CAB file downloaded to {cabPath}");

            // 2. Verify download exists and has content
            var fileInfo = new FileInfo(cabPath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                LogService.LogError("Downloaded CAB file is empty or missing");
                return false;
            }

            // 3. Extract with expand.exe
            progress?.Report(new DownloadProgress { StatusText = "Extracting patch files..." });
            var extracted = await ExtractCabAsync(cabPath, stagingDir, ct);
            if (!extracted)
                return false;

            // 4. Apply extracted files (.new suffix → real file)
            progress?.Report(new DownloadProgress { StatusText = "Applying patch..." });
            ApplyPatchFiles(stagingDir, gameRootPath);

            return true;
        }
        finally
        {
            // Clean up staging directory
            try { Directory.Delete(stagingDir, recursive: true); }
            catch { /* best effort */ }
        }
    }

    private static async Task<bool> ExtractCabAsync(string cabPath, string extractDir, CancellationToken ct)
    {
        LogService.Log("Starting expand.exe...");
        LogService.Log("Using extraction timeout of 300 seconds for CAB file");

        var psi = new ProcessStartInfo
        {
            FileName = "expand.exe",
            Arguments = $"\"{cabPath}\" -F:* \"{extractDir}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        LogService.Log($"expand.exe exited with code {process.ExitCode}");
        LogService.Log($"expand.exe output: {output}");

        return process.ExitCode == 0;
    }

    private static void ApplyPatchFiles(string stagingDir, string gameRootPath)
    {
        // Find all .new files in the staging directory and copy them
        // to the corresponding location in gameRoot (stripping .new extension)
        var newFiles = Directory.GetFiles(stagingDir, "*.new", SearchOption.AllDirectories);

        foreach (var newFile in newFiles)
        {
            // Get relative path from staging dir
            var relativePath = Path.GetRelativePath(stagingDir, newFile);
            // Strip .new extension
            relativePath = relativePath[..^4]; // remove ".new"

            var targetPath = Path.Combine(gameRootPath, relativePath);
            var targetDir = Path.GetDirectoryName(targetPath);

            if (!string.IsNullOrEmpty(targetDir))
                Directory.CreateDirectory(targetDir);

            // Safe overwrite: rename existing, copy new, then delete old
            var backupPath = targetPath + ".bak";
            try
            {
                if (File.Exists(targetPath))
                    File.Move(targetPath, backupPath, overwrite: true);

                File.Copy(newFile, targetPath, overwrite: true);

                // Remove backup on success
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
            }
            catch
            {
                // Roll back on failure
                if (File.Exists(backupPath) && !File.Exists(targetPath))
                    File.Move(backupPath, targetPath);
                throw;
            }
        }
    }

    private async Task DownloadPatchIniToLocal(string gameRootPath, string updateBaseUrl, CancellationToken ct)
    {
        var remoteUrl = $"{updateBaseUrl}/microvolts/patch.ini";
        var localPath = Path.Combine(gameRootPath, "patch.ini");
        await _downloadService.DownloadFileAsync(remoteUrl, localPath, ct: ct);
    }
}

using System.Diagnostics;
using System.Xml.Linq;
using Launcher.Core.Config;
using Launcher.Core.Models;

namespace Launcher.Core.Services;

/// <summary>
/// Orchestrates the update/patch pipeline:
/// 1. Fetch remote patch.ini
/// 2. Compare remote vs installed version
/// 3. Download .cab + .xml patch files
/// 4. Extract with expand.exe to staging dir
/// 5. Verify Adler32 checksums from XML
/// 6. Swap files into GameRoot (skipping protected files)
/// 7. Update local state
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
    /// Find the step-by-step upgrade path from installed to latest.
    /// </summary>
    public static List<(GameVersion From, GameVersion To)> GetUpgradePath(
        GameVersion installed, PatchConfig remotePatch)
    {
        var steps = new List<(GameVersion From, GameVersion To)>();

        var allVersions = remotePatch.AllVersions
            .Where(v => !v.IsEmpty)
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        var current = installed;
        var latest = remotePatch.LatestVersion;

        var upgradeVersions = allVersions
            .Where(v => v > current && v <= latest)
            .OrderBy(v => v)
            .ToList();

        foreach (var target in upgradeVersions)
        {
            steps.Add((current, target));
            current = target;
        }

        // If no intermediate steps found but we still need to update, try a direct jump
        if (steps.Count == 0 && NeedsUpdate(installed, latest))
            steps.Add((installed, latest));

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
        CancellationToken ct = default,
        Action<GameVersion>? onStepApplied = null)
    {
        var steps = GetUpgradePath(installedVersion, remotePatch);
        if (steps.Count == 0)
        {
            LogService.Log("No upgrade steps needed.");
            return true;
        }

        LogService.Log($"Applying step-by-step patch from {installedVersion} to {remotePatch.LatestVersion}");

        foreach (var (from, to) in steps)
        {
            LogService.Log($"Starting patch step: {from} → {to}");

            var success = await ApplySinglePatchAsync(gameRootPath, updateBaseUrl, from, to, progress, ct);
            if (!success)
            {
                LogService.LogError($"Failed to apply patch from {from} to {to}");
                return false;
            }

            LogService.Log($"Successfully applied patch: {from} → {to}");
            // Save progress immediately — if a later step fails, the next launch resumes from here
            onStepApplied?.Invoke(to);
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
        var cabUrl = $"{updateBaseUrl}/microvolts/{to}/microvolts-{from}-{to}.cab";
        var xmlUrl = cabUrl.Replace(".cab", ".xml");

        var stagingDir = Path.Combine(Path.GetTempPath(), $"LauncherPatch_{Guid.NewGuid()}");
        Directory.CreateDirectory(stagingDir);

        try
        {
            var cabPath = Path.Combine(stagingDir, "patch.cab");

            // 1. Download the .cab file
            progress?.Report(new DownloadProgress { StatusText = $"Downloading patch {from} → {to}..." });
            var downloaded = await _downloadService.DownloadFileAsync(cabUrl, cabPath, progress, ct);
            if (!downloaded)
                return false;

            LogService.Log($"CAB downloaded to {cabPath}");

            var fileInfo = new FileInfo(cabPath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                LogService.LogError("Downloaded CAB file is empty or missing");
                return false;
            }

            // 2. Download the .xml for Adler32 checksum verification (optional — server may not have it)
            Dictionary<string, string> checksums = new();
            var xmlContent = await _downloadService.DownloadStringAsync(xmlUrl, ct);
            if (xmlContent != null)
            {
                checksums = ParseXmlChecksums(xmlContent);
                LogService.Log($"Loaded {checksums.Count} checksums from XML.");
            }
            else
            {
                LogService.Log("XML not available — skipping checksum verification.");
            }

            // 3. Extract with expand.exe
            progress?.Report(new DownloadProgress { StatusText = "Extracting patch files..." });
            var extracted = await ExtractCabAsync(cabPath, stagingDir, ct);
            if (!extracted)
                return false;

            // 4. Apply extracted files with checksum verification
            progress?.Report(new DownloadProgress { StatusText = "Applying patch..." });
            await ApplyPatchFilesAsync(stagingDir, gameRootPath, checksums);

            return true;
        }
        finally
        {
            try { Directory.Delete(stagingDir, recursive: true); }
            catch { /* best effort */ }
        }
    }

    private static async Task<bool> ExtractCabAsync(string cabPath, string extractDir, CancellationToken ct)
    {
        LogService.Log("Starting expand.exe...");

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

        if (process.ExitCode != 0)
        {
            // Fallback: try treating the CAB as a ZIP
            LogService.Log("expand.exe failed — trying ZIP extraction fallback...");
            return TryExtractAsZip(cabPath, extractDir);
        }

        return true;
    }

    private static bool TryExtractAsZip(string cabPath, string extractDir)
    {
        try
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(cabPath, extractDir, overwriteFiles: true);
            LogService.Log("ZIP fallback extraction succeeded.");
            return true;
        }
        catch (Exception ex)
        {
            LogService.LogError("ZIP fallback extraction also failed", ex);
            return false;
        }
    }

    /// <summary>
    /// Apply all extracted files to the game root, with Adler32 verification and protected-file skipping.
    /// Matches the reference launcher's ReplaceFilesAsync logic.
    /// </summary>
    private static async Task ApplyPatchFilesAsync(
        string stagingDir,
        string gameRootPath,
        Dictionary<string, string> checksums)
    {
        // Kill game process if running (prevents file lock)
        foreach (var proc in Process.GetProcessesByName("MicroVolts"))
        {
            try { proc.Kill(); proc.WaitForExit(5000); }
            catch { /* best effort */ }
        }
        await Task.Delay(500);

        var allFiles = Directory.GetFiles(stagingDir, "*", SearchOption.AllDirectories);

        foreach (var file in allFiles)
        {
            var relativePath = Path.GetRelativePath(stagingDir, file);

            // Skip protected/meta files (matches reference launcher behaviour)
            if (Path.GetFileName(file).Equals("patch.cab", StringComparison.OrdinalIgnoreCase)) continue;
            if (relativePath.Equals(@"data\config\ENG\option.ini", StringComparison.OrdinalIgnoreCase)) continue;
            if (Path.GetFileName(file).Equals("launcher.txt", StringComparison.OrdinalIgnoreCase)) continue;

            // Verify Adler32 checksum if the XML provided one for this file
            var fileName = Path.GetFileName(file);
            if (checksums.TryGetValue(fileName, out var expectedChecksum))
            {
                var fileData = await File.ReadAllBytesAsync(file);
                var computed = Adler32(fileData).ToString("x8");
                if (computed != expectedChecksum)
                {
                    LogService.LogError($"Checksum mismatch for {fileName}: expected {expectedChecksum}, got {computed}");
                    throw new Exception($"Checksum error for {fileName} — patch may be corrupt");
                }
            }

            // Strip the last extension (removes .new suffix → real filename)
            var targetName = Path.GetFileNameWithoutExtension(Path.GetFileName(relativePath));
            var targetDir = Path.GetDirectoryName(relativePath);
            var targetPath = targetDir != null
                ? Path.Combine(gameRootPath, targetDir, targetName)
                : Path.Combine(gameRootPath, targetName);

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            var backupPath = targetPath + ".bak";
            try
            {
                if (File.Exists(backupPath))
                    File.Delete(backupPath); // Ensure no orphaned backups crash the File.Move

                if (File.Exists(targetPath))
                    File.Move(targetPath, backupPath, overwrite: true);

                using var src = File.OpenRead(file);
                using var dst = File.Create(targetPath);
                await src.CopyToAsync(dst);

                if (File.Exists(backupPath))
                    File.Delete(backupPath);
            }
            catch (IOException ex)
            {
                LogService.LogError($"Failed to replace file {targetPath}", ex);
                // Roll back on failure
                if (File.Exists(backupPath) && !File.Exists(targetPath))
                    File.Move(backupPath, targetPath);
            }
        }
    }

    private static Dictionary<string, string> ParseXmlChecksums(string xmlContent)
    {
        try
        {
            var doc = XDocument.Parse(xmlContent);
            return doc.Descendants("File")
                .Select(f => new { Name = f.Attribute("Name")?.Value, CheckSum = f.Attribute("CheckSum")?.Value })
                .Where(f => f.Name != null && f.CheckSum != null)
                .ToDictionary(f => f.Name!, f => f.CheckSum!);
        }
        catch (Exception ex)
        {
            LogService.LogError("Failed to parse XML checksums", ex);
            return new Dictionary<string, string>();
        }
    }

    /// <summary>Adler32 checksum — matches the reference launcher's implementation.</summary>
    public static uint Adler32(byte[] data)
    {
        const uint MOD_ADLER = 65521;
        uint a = 1, b = 0;
        foreach (byte bt in data)
        {
            a = (a + bt) % MOD_ADLER;
            b = (b + a) % MOD_ADLER;
        }
        return (b << 16) | a;
    }

    private async Task DownloadPatchIniToLocal(string gameRootPath, string updateBaseUrl, CancellationToken ct)
    {
        var remoteUrl = $"{updateBaseUrl}/microvolts/patch.ini";
        var localPath = Path.Combine(gameRootPath, "patch.ini");
        await _downloadService.DownloadFileAsync(remoteUrl, localPath, ct: ct);
    }
}

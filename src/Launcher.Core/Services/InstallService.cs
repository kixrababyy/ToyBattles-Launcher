using System.IO.Compression;
using Launcher.Core.Config;

namespace Launcher.Core.Services;

/// <summary>
/// Handles full game installation for first-time users.
/// Downloads the complete game archive from the FullFileAddress and extracts it.
/// </summary>
public class InstallService
{
    private readonly DownloadService _downloadService = new();

    /// <summary>
    /// Known archive filenames to try when the FullFileAddress is a directory URL.
    /// </summary>
    private static readonly string[] KnownArchiveNames =
    [
        "MicroVolts.zip",
        "microvolts.zip",
        "Full.zip",
        "full.zip",
        "game.zip",
        "MicroVolts.cab",
        "Full.cab",
        "full.cab"
    ];

    /// <summary>
    /// Fetch the updateinfo.ini from a known bootstrap URL to get the FullFileAddress.
    /// This is used when no local updateinfo.ini exists yet (fresh install).
    /// </summary>
    public async Task<UpdateInfoConfig?> FetchUpdateInfoAsync(
        string bootstrapUrl,
        CancellationToken ct = default)
    {
        var content = await _downloadService.DownloadStringAsync(bootstrapUrl, ct);
        if (content == null) return null;
        return UpdateInfoConfig.LoadFromContent(content);
    }

    /// <summary>
    /// Download and install the full game to the specified directory.
    /// </summary>
    /// <param name="installDir">Where to extract the game files (the game root).</param>
    /// <param name="fullFileAddress">Base URL or direct URL for the full game archive.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The game root path on success, null on failure.</returns>
    public async Task<string?> InstallFullGameAsync(
        string installDir,
        string fullFileAddress,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        LogService.LogSection("FULL GAME INSTALL");
        LogService.Log($"Target folder : {installDir}");
        LogService.Log($"FullFileAddress: {fullFileAddress}");

        Directory.CreateDirectory(installDir);

        // Phase 1: Resolve the actual download URL
        LogService.LogStep("Resolving archive URL");
        var archiveUrl = await ResolveArchiveUrlAsync(fullFileAddress, ct);
        if (archiveUrl == null)
        {
            LogService.LogError("Could not resolve full game archive URL — all probes failed.");
            return null;
        }

        LogService.Log($"Resolved URL  : {archiveUrl}");

        // Determine file extension for extraction method
        var extension = Path.GetExtension(new Uri(archiveUrl).AbsolutePath).ToLowerInvariant();
        var archivePath = Path.Combine(Path.GetTempPath(), $"ToyBattles_FullInstall_{Guid.NewGuid()}{extension}");

        try
        {
            // Phase 2: Download
            LogService.LogStep("Downloading archive");
            progress?.Report(new DownloadProgress { StatusText = "Downloading game files..." });

            var downloaded = await _downloadService.DownloadFileAsync(archiveUrl, archivePath, progress, ct);
            if (!downloaded)
            {
                LogService.LogError("Download failed — see download attempts above.");
                return null;
            }

            var fileInfo = new FileInfo(archivePath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                LogService.LogError("Downloaded archive is empty or missing.");
                return null;
            }

            LogService.Log($"Archive size  : {DownloadService.FormatBytes(fileInfo.Length)}");

            // Phase 3: Extract
            LogService.LogStep($"Extracting ({extension.TrimStart('.')?.ToUpper()})");
            progress?.Report(new DownloadProgress { StatusText = "Extracting game files...", ProgressPercent = 0 });

            bool extracted;
            if (extension is ".cab")
                extracted = await ExtractCabAsync(archivePath, installDir, ct);
            else
                extracted = await ExtractZipAsync(archivePath, installDir, progress, ct);

            if (!extracted)
            {
                LogService.LogError("Extraction failed.");
                return null;
            }

            // Phase 4: Verify game root
            LogService.LogStep("Verifying game files");
            var gameRoot = FindGameRootInDir(installDir);
            if (gameRoot == null)
            {
                LogService.LogError($"Game executable not found after extraction in: {installDir}");
                return null;
            }

            LogService.Log($"Game root     : {gameRoot}");
            LogService.Log("INSTALL COMPLETE");
            progress?.Report(new DownloadProgress { StatusText = "Installation complete!", ProgressPercent = 100 });

            return gameRoot;
        }
        finally
        {
            try { if (File.Exists(archivePath)) File.Delete(archivePath); }
            catch { /* best effort */ }
        }
    }

    /// <summary>
    /// Resolve the archive URL. If the address already ends in a known extension, use it directly.
    /// Otherwise probe candidate URLs (suffix + directory variants) with plain GET to find the file.
    /// </summary>
    private async Task<string?> ResolveArchiveUrlAsync(string fullFileAddress, CancellationToken ct)
    {
        var trimmed = fullFileAddress.TrimEnd('/');

        // Already a direct file URL — use as-is
        var lastSegment = new Uri(trimmed).Segments.LastOrDefault()?.TrimEnd('/') ?? "";
        if (lastSegment.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
            lastSegment.EndsWith(".cab", StringComparison.OrdinalIgnoreCase) ||
            lastSegment.EndsWith(".7z",  StringComparison.OrdinalIgnoreCase))
        {
            LogService.Log($"URL has extension — using directly: {trimmed}");
            return trimmed;
        }

        // Build candidate list:
        //  1. Suffix variants         → e.g. ".../Full.zip"
        //  2. Known subdirectory path → ".../microvolts/Full/Full.zip" under the parent dir
        //     (the CDN serves the archive at <base>/microvolts/Full/Full.zip where
        //      <base> is one level up from FullFileAddress)
        //  3. Directory variants       → e.g. ".../Full/Full.zip"
        var candidates = new List<string>();
        foreach (var ext in new[] { ".zip", ".cab", ".7z" })
            candidates.Add(trimmed + ext);

        var lastSlash = trimmed.LastIndexOf('/');
        if (lastSlash > 0)
        {
            var parentUrl = trimmed[..lastSlash];
            candidates.Add($"{parentUrl}/microvolts/Full/Full.zip");
            candidates.Add($"{parentUrl}/microvolts/full/full.zip");
        }

        foreach (var name in KnownArchiveNames)
            candidates.Add($"{trimmed}/{name}");

        LogService.Log($"Probing {candidates.Count} candidate URLs (plain GET, headers only)...");

        using var probeClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        probeClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        foreach (var testUrl in candidates)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, testUrl);
                using var response = await probeClient.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, ct);

                var code = (int)response.StatusCode;
                if (response.IsSuccessStatusCode)
                {
                    LogService.Log($"  [ OK {code}] {testUrl}");
                    return testUrl;
                }

                LogService.Log($"  [{code}    ] {testUrl}");
            }
            catch (Exception ex)
            {
                LogService.Log($"  [ERR   ] {testUrl} — {ex.Message}");
            }
        }

        // Nothing found — fall back to bare URL (may redirect or give a clearer error)
        LogService.LogWarning($"All probes failed. Falling back to bare URL: {trimmed}");
        return trimmed;
    }

    private static async Task<bool> ExtractZipAsync(
        string zipPath,
        string extractDir,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        try
        {
            LogService.Log($"Extracting ZIP to {extractDir}...");

            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var totalEntries = archive.Entries.Count;
                var processed = 0;

                foreach (var entry in archive.Entries)
                {
                    ct.ThrowIfCancellationRequested();

                    var destPath = Path.Combine(extractDir, entry.FullName);

                    // Skip directory entries
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destPath);
                        continue;
                    }

                    // Ensure parent directory exists
                    var dir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    entry.ExtractToFile(destPath, overwrite: true);
                    processed++;

                    if (progress != null && totalEntries > 0)
                    {
                        var pct = (double)processed / totalEntries * 100.0;
                        progress.Report(new DownloadProgress
                        {
                            StatusText = $"Extracting... {processed:N0}/{totalEntries:N0} files",
                            ProgressPercent = pct
                        });
                    }
                }
            }, ct);

            LogService.Log("ZIP extraction complete.");
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogService.LogError("ZIP extraction failed", ex);
            return false;
        }
    }

    private static async Task<bool> ExtractCabAsync(
        string cabPath, string extractDir, CancellationToken ct)
    {
        try
        {
            LogService.Log($"Extracting CAB to {extractDir}...");

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "expand.exe",
                Arguments = $"\"{cabPath}\" -F:* \"{extractDir}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new System.Diagnostics.Process { StartInfo = psi };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            LogService.Log($"expand.exe exited with code {process.ExitCode}");
            LogService.Log($"expand.exe output: {output}");

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            LogService.LogError("CAB extraction failed", ex);
            return false;
        }
    }

    /// <summary>
    /// After extraction, find the actual game root (the directory containing Bin/MicroVolts.exe).
    /// The archive might extract directly into installDir, or into a subdirectory.
    /// </summary>
    private static string? FindGameRootInDir(string searchDir)
    {
        // Check the directory itself
        if (LaunchService.ValidateGameRoot(searchDir))
            return searchDir;

        // Check subdirectories up to 2 levels deep (archive might have a root folder)
        try
        {
            foreach (var subDir in Directory.GetDirectories(searchDir))
            {
                if (LaunchService.ValidateGameRoot(subDir))
                    return subDir;
                foreach (var nested in Directory.GetDirectories(subDir))
                {
                    if (LaunchService.ValidateGameRoot(nested))
                        return nested;
                }
            }
        }
        catch { /* best effort */ }

        return null;
    }
}

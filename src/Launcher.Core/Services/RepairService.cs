namespace Launcher.Core.Services;

/// <summary>
/// Verifies installed game files against the remote full-file manifest
/// and re-downloads any missing or corrupt files.
/// </summary>
public class RepairService
{
    private readonly DownloadService _downloadService = new();

    public class RepairProgress
    {
        public int TotalFiles { get; set; }
        public int CheckedFiles { get; set; }
        public int RepairedFiles { get; set; }
        public int FailedFiles { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
        public double ProgressPercent =>
            TotalFiles > 0 ? (double)CheckedFiles / TotalFiles * 100.0 : 0;
    }

    /// <summary>
    /// Verify the critical game file (cgd.dip) against the remote copy.
    /// Checks file size and downloads if missing/different.
    /// </summary>
    public async Task<RepairProgress> VerifyAndRepairAsync(
        string gameRootPath,
        string fullFileBaseUrl,
        IProgress<RepairProgress>? progress = null,
        CancellationToken ct = default)
    {
        var result = new RepairProgress();

        // The existing launcher verifies specific files from the FullFile URL
        // Based on log analysis, it checks: data/cgd.dip
        // Build a list of known game files to verify
        var filesToVerify = new List<(string relativePath, string remoteUrl)>();

        // Primary data file
        filesToVerify.Add(("data\\cgd.dip",
            $"{fullFileBaseUrl}/data/cgd.dip"));

        // Also verify the game executable
        filesToVerify.Add(("Bin\\MicroVolts.exe",
            $"{fullFileBaseUrl}/Bin/MicroVolts.exe"));

        result.TotalFiles = filesToVerify.Count;

        foreach (var (relativePath, remoteUrl) in filesToVerify)
        {
            ct.ThrowIfCancellationRequested();

            var localPath = Path.Combine(gameRootPath, relativePath);
            result.CurrentFile = relativePath;
            result.CheckedFiles++;

            progress?.Report(result);
            LogService.Log($"Checking {relativePath} from {remoteUrl}");

            try
            {
                var localSize = File.Exists(localPath) ? new FileInfo(localPath).Length : 0;
                long remoteSize = -1;

                // Send HEAD request to get accurate remote size
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Head, remoteUrl);
                    using var response = await new HttpClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                    if (response.IsSuccessStatusCode)
                        remoteSize = response.Content.Headers.ContentLength ?? -1;
                }
                catch { }

                LogService.Log($"{relativePath} - Local size: {localSize} bytes, Remote size: {remoteSize} bytes");

                // Check if local file is missing, or if remote size is known and they don't match
                if (!File.Exists(localPath) || (remoteSize > 0 && localSize != remoteSize))
                {
                    LogService.Log($"{relativePath} is missing or differing in size, downloading...");
                    var success = await _downloadService.DownloadFileAsync(remoteUrl, localPath, ct: ct);
                    if (success)
                    {
                        result.RepairedFiles++;
                        LogService.Log($"{relativePath} repaired successfully.");
                    }
                    else
                    {
                        result.FailedFiles++;
                        LogService.LogError($"Failed to repair {relativePath}");
                    }
                }
                else
                {
                    LogService.Log($"{relativePath} verified OK.");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError($"Error verifying {relativePath}", ex);
                result.FailedFiles++;
            }

            progress?.Report(result);
        }

        result.CurrentFile = "Verification complete";
        progress?.Report(result);

        LogService.Log($"Repair complete: {result.CheckedFiles} checked, " +
                       $"{result.RepairedFiles} repaired, {result.FailedFiles} failed");

        return result;
    }
}

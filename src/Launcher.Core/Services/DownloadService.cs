namespace Launcher.Core.Services;

/// <summary>
/// Progress information for a file download.
/// </summary>
public class DownloadProgress
{
    public long BytesReceived { get; set; }
    public long TotalBytes { get; set; }
    public double SpeedBytesPerSecond { get; set; }
    public TimeSpan EstimatedTimeRemaining { get; set; }
    public double ProgressPercent =>
        TotalBytes > 0 ? (double)BytesReceived / TotalBytes * 100.0 : 0;
    public string StatusText { get; set; } = string.Empty;
}

/// <summary>
/// Downloads files with progress reporting, speed/ETA calculation, and retry logic.
/// </summary>
public class DownloadService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(10)
    };

    private const int MaxRetries = 6;
    private const int BufferSize = 81920; // 80KB chunks

    /// <summary>
    /// Download a file from <paramref name="url"/> to <paramref name="destPath"/>
    /// with progress reporting.
    /// </summary>
    public async Task<bool> DownloadFileAsync(
        string url,
        string destPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                LogService.Log($"Starting download (attempt {attempt}/{MaxRetries}): {url}");
                using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                LogService.Log($"Total file size: {totalBytes} bytes");

                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);

                var buffer = new byte[BufferSize];
                long totalRead = 0;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    totalRead += bytesRead;

                    if (progress != null)
                    {
                        var elapsed = sw.Elapsed;
                        var speed = elapsed.TotalSeconds > 0 ? totalRead / elapsed.TotalSeconds : 0;
                        var remaining = speed > 0 && totalBytes > 0
                            ? TimeSpan.FromSeconds((totalBytes - totalRead) / speed)
                            : TimeSpan.Zero;

                        progress.Report(new DownloadProgress
                        {
                            BytesReceived = totalRead,
                            TotalBytes = totalBytes,
                            SpeedBytesPerSecond = speed,
                            EstimatedTimeRemaining = remaining,
                            StatusText = $"Downloading... {FormatBytes(totalRead)} / {FormatBytes(totalBytes)}"
                        });
                    }
                }

                LogService.Log($"Download completed: {destPath}");
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                LogService.LogError($"Download attempt {attempt} failed for {url}", ex);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
            catch (Exception ex)
            {
                LogService.LogError($"Download failed after {MaxRetries} attempts for {url}", ex);
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Download a string from a URL (e.g., remote patch.ini content).
    /// </summary>
    public async Task<string?> DownloadStringAsync(string url, CancellationToken ct = default)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await HttpClient.GetStringAsync(url, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                LogService.LogError($"Download string attempt {attempt} failed for {url}", ex);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
            catch (Exception ex)
            {
                LogService.LogError($"Download string failed for {url}", ex);
                return null;
            }
        }

        return null;
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0) return "Unknown";
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return $"{size:F1} {units[unitIndex]}";
    }

    public static string FormatSpeed(double bytesPerSecond)
    {
        return $"{FormatBytes((long)bytesPerSecond)}/s";
    }
}

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
    private double? _progressPercentOverride;
    public double ProgressPercent
    {
        get => _progressPercentOverride
               ?? (TotalBytes > 0 ? (double)BytesReceived / TotalBytes * 100.0 : 0);
        set => _progressPercentOverride = value;
    }
    public string StatusText { get; set; } = string.Empty;
}

/// <summary>
/// Downloads files with progress reporting, speed/ETA calculation, and retry logic.
/// </summary>
public class DownloadService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(10),
        DefaultRequestHeaders =
        {
            UserAgent = { System.Net.Http.Headers.ProductInfoHeaderValue.Parse(
                "Mozilla/5.0") }
        }
    };

    /// <summary>
    /// Optional download speed cap in bytes/sec. 0 = unlimited.
    /// Set via Settings → Max download speed (MB/s).
    /// </summary>
    public static long MaxBytesPerSecond { get; set; } = 0;

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
                if (attempt == 1)
                    LogService.Log($"Downloading: {url}");
                else
                    LogService.LogWarning($"Retry {attempt}/{MaxRetries}: {url}");

                using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                LogService.Log($"File size: {(totalBytes >= 0 ? FormatBytes(totalBytes) : "unknown")}");

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

                    // Throttle if a speed cap is configured
                    if (MaxBytesPerSecond > 0)
                    {
                        var expectedMs = (long)(totalRead * 1000.0 / MaxBytesPerSecond);
                        var delayMs = (int)(expectedMs - (long)sw.Elapsed.TotalMilliseconds);
                        if (delayMs > 0)
                            await Task.Delay(delayMs, ct);
                    }

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

                LogService.Log($"Download complete → {destPath}");
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                var wait = (int)Math.Pow(2, attempt);
                LogService.LogError($"Attempt {attempt} failed — retrying in {wait}s", ex);
                await Task.Delay(TimeSpan.FromSeconds(wait), ct);
            }
            catch (Exception ex)
            {
                LogService.LogError($"All {MaxRetries} download attempts failed for {url}", ex);
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

    /// <summary>
    /// Download raw bytes from a URL (e.g. for checksum comparison).
    /// </summary>
    public async Task<byte[]?> DownloadBytesAsync(string url, CancellationToken ct = default)
    {
        try
        {
            return await HttpClient.GetByteArrayAsync(url, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogService.LogError($"Failed to download bytes from {url}", ex);
            return null;
        }
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

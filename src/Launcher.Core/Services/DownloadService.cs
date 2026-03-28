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
    private static readonly HttpClient HttpClient = new(
        new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            // TLS: accept any certificate AND allow TLS 1.2 + 1.3 so the negotiation
            // can fall back when one version's cipher suite fails on a user's system.
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12
                                    | System.Security.Authentication.SslProtocols.Tls13,
            },
        })
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

        var tmpPath = destPath + ".tmp";

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                ct.ThrowIfCancellationRequested();

                long existingSize = 0;
                if (File.Exists(tmpPath))
                    existingSize = new FileInfo(tmpPath).Length;

                if (attempt == 1)
                    LogService.Log($"Downloading: {url}{(existingSize > 0 ? $" (Resuming from {FormatBytes(existingSize)})" : "")}");
                else
                    LogService.LogWarning($"Retry {attempt}/{MaxRetries}: {url}");

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (existingSize > 0)
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingSize, null);

                using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    LogService.LogError(
                        $"Download failed immediately — HTTP {(int)response.StatusCode} {response.ReasonPhrase} for {url}", null!);
                    // Do not retry predictable permanent errors
                    return false;
                }
                
                // If we get 416 Range Not Satisfiable, our tmp file is likely larger than the remote file or invalid. Delete and retry.
                if (response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    LogService.LogWarning("Server refused range request. Restarting download from scratch.");
                    File.Delete(tmpPath);
                    existingSize = 0;
                    continue; // Loop again, next time it won't send the range header
                }

                response.EnsureSuccessStatusCode();

                // 206 Partial Content means the server honored our range request
                var isPartial = response.StatusCode == System.Net.HttpStatusCode.PartialContent;
                if (!isPartial && existingSize > 0)
                {
                    // Server ignored the range request and is sending the full file. We must wipe the tmp file.
                    LogService.LogWarning("Server ignored range request (returned 200 instead of 206). Restarting download from scratch.");
                    File.Delete(tmpPath);
                    existingSize = 0;
                }

                var contentLength = response.Content.Headers.ContentLength ?? -1;
                var totalBytes = contentLength >= 0 ? existingSize + contentLength : -1;

                if (attempt == 1 || isPartial)
                    LogService.Log($"File size: {(totalBytes >= 0 ? FormatBytes(totalBytes) : "unknown")}");

                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                var fileMode = isPartial && existingSize > 0 ? FileMode.Append : FileMode.Create;
                await using var fileStream = new FileStream(tmpPath, fileMode, FileAccess.Write, FileShare.None, BufferSize, true);

                var buffer = new byte[BufferSize];
                long totalRead = existingSize;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    totalRead += bytesRead;

                    // Throttle if a speed cap is configured
                    if (MaxBytesPerSecond > 0)
                    {
                        var expectedMs = (long)((totalRead - existingSize) * 1000.0 / MaxBytesPerSecond);
                        var delayMs = (int)(expectedMs - (long)sw.Elapsed.TotalMilliseconds);
                        if (delayMs > 0)
                            await Task.Delay(delayMs, ct);
                    }

                    if (progress != null)
                    {
                        var elapsed = sw.Elapsed;
                        var speed = elapsed.TotalSeconds > 0 ? (totalRead - existingSize) / elapsed.TotalSeconds : 0;
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

                // Explicitly dispose the streams so the file is unlocked before we rename it
                await fileStream.DisposeAsync();
                await contentStream.DisposeAsync();

                // Download completed fully. Atomic rename.
                if (File.Exists(destPath)) File.Delete(destPath); // Clean move
                File.Move(tmpPath, destPath);

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
            }
        }

        // .NET HttpClient failed — fall back to Windows BITS which uses WinHTTP
        // (a completely separate TLS stack that handles cipher/cert issues differently).
        progress?.Report(new DownloadProgress { StatusText = "Trying system downloader (BITS)..." });
        return await TryBitsDownloadAsync(url, destPath, progress, ct);
    }

    /// <summary>
    /// Last-resort download via Windows Background Intelligent Transfer Service.
    /// Uses WinHTTP instead of .NET Schannel — resolves TLS cipher incompatibilities
    /// that cause "The decryption operation failed" on certain networks/configurations.
    /// </summary>
    private static async Task<bool> TryBitsDownloadAsync(
        string url, string destPath,
        IProgress<DownloadProgress>? progress, CancellationToken ct)
    {
        try
        {
            LogService.Log($"BITS fallback: {url}");
            try { if (File.Exists(destPath)) File.Delete(destPath); } catch { }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = $"-NonInteractive -WindowStyle Hidden -Command " +
                                  $"\"Start-BitsTransfer -Source '{url}' -Destination '{destPath}'\"",
                UseShellExecute = false,
                CreateNoWindow  = true,
                RedirectStandardError = true,
            };

            using var proc = new System.Diagnostics.Process { StartInfo = psi };
            proc.Start();

            // Poll growing file size for a live progress display while BITS downloads
            var done = false;
            _ = Task.Run(async () =>
            {
                while (!done)
                {
                    await Task.Delay(500).ConfigureAwait(false);
                    if (progress == null || !File.Exists(destPath)) continue;
                    try
                    {
                        var received = new FileInfo(destPath).Length;
                        progress.Report(new DownloadProgress
                        {
                            BytesReceived = received,
                            StatusText    = $"Downloading (system)... {FormatBytes(received)}",
                        });
                    }
                    catch { }
                }
            }, CancellationToken.None);

            try   { await proc.WaitForExitAsync(ct); }
            catch (OperationCanceledException) { done = true; try { proc.Kill(); } catch { } throw; }
            done = true;

            if (proc.ExitCode != 0)
            {
                var stderr = await proc.StandardError.ReadToEndAsync();
                LogService.LogError($"BITS fallback failed (exit {proc.ExitCode}): {stderr}");
                return false;
            }

            var ok = File.Exists(destPath) && new FileInfo(destPath).Length > 0;
            if (ok) LogService.Log($"BITS download complete → {destPath}");
            return ok;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            LogService.LogError("BITS fallback failed", ex);
            return false;
        }
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
                using var response = await HttpClient.GetAsync(url, ct);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    LogService.Log($"Download string skipped — HTTP {(int)response.StatusCode} for {url}");
                    return null; // Don't retry permanent errors
                }
                
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(ct);
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

using System.Diagnostics;

namespace Launcher.Core.Services;

/// <summary>
/// Writes timestamped log entries to daily log files under
/// %LOCALAPPDATA%\ToyBattlesLauncher\logs\.
/// </summary>
public class LogService
{
    private static readonly object Lock = new();
    private static string? _logDir;

    public static string LogDirectory
    {
        get
        {
            _logDir ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ToyBattlesLauncher", "logs");
            Directory.CreateDirectory(_logDir);
            return _logDir;
        }
    }

    private static string CurrentLogPath =>
        Path.Combine(LogDirectory, $"launcher_{DateTime.Now:yyyy-MM-dd}.log");

    public static void Log(string message) => Write($"[INFO]  {message}");

    public static void LogWarning(string message) => Write($"[WARN]  {message}");

    public static void LogError(string message, Exception? ex = null)
    {
        if (ex == null)
        {
            Write($"[ERROR] {message}");
            return;
        }

        // Build a chain of all inner exception messages so nothing is hidden
        var parts = new System.Collections.Generic.List<string>();
        var current = ex;
        while (current != null)
        {
            parts.Add(current.Message);
            current = current.InnerException;
        }
        var chain = string.Join(" → ", parts);
        Write($"[ERROR] {message}: {chain}");
    }

    /// <summary>
    /// Writes a bold section header — use at the start of major operations
    /// (install, update, repair, launch) so log sections are easy to find.
    /// </summary>
    public static void LogSection(string title)
    {
        var bar = new string('═', 55);
        Write($"\n{bar}\n  {title}\n{bar}");
    }

    /// <summary>
    /// Writes a minor sub-step divider inside a section.
    /// </summary>
    public static void LogStep(string step)
    {
        Write($"── {step} ──────────────────────────────────────");
    }

    private static void Write(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}";
        lock (Lock)
        {
            try
            {
                File.AppendAllText(CurrentLogPath, line + Environment.NewLine);
            }
            catch
            {
                // Swallow I/O errors in logging — never crash the app from logging.
            }
        }
    }

    public static void OpenLogsFolder()
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = LogDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log($"Failed to open logs folder: {ex.Message}");
        }
    }
}

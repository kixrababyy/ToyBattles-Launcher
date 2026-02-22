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

    public static void Log(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {message}";
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

    public static void LogError(string message, Exception? ex = null)
    {
        var msg = ex != null ? $"{message}: {ex.Message}" : message;
        Log($"ERROR: {msg}");
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

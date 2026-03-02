using System.Text.Json;

namespace Launcher.Core.Models;

/// <summary>
/// Persisted local state: install path, installed version, last check time.
/// Saved as JSON to %LOCALAPPDATA%\ToyBattlesLauncher\state.json.
/// </summary>
public class LocalState
{
    public string? GameRootPath { get; set; }
    public string? InstalledVersion { get; set; }
    public DateTime? LastCheckUtc { get; set; }
    public string? LaunchArguments { get; set; }
    public string? CustomUpdateUrl { get; set; }
    public bool KeepLauncherOpen { get; set; } = false;
    public bool CheckUpdatesOnStartup { get; set; } = true;
    public int MaxDownloadSpeedMBps { get; set; } = 0; // 0 = unlimited
    public string? ServerIp { get; set; }
    public string? ServerProfile { get; set; }

    /// <summary>Cumulative playtime across all sessions, in seconds.</summary>
    public long TotalPlaytimeSeconds { get; set; } = 0;

    /// <summary>
    /// Set when the game is launched. Cleared when the session is recorded.
    /// If still set on next launch (e.g. launcher closed with game), elapsed time is recovered.
    /// </summary>
    public DateTime? PendingSessionStartUtc { get; set; } = null;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string DefaultStatePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ToyBattlesLauncher",
            "state.json");

    public GameVersion GetInstalledVersion()
    {
        if (string.IsNullOrEmpty(InstalledVersion))
            return GameVersion.Empty;

        return GameVersion.TryParse(InstalledVersion) ?? GameVersion.Empty;
    }

    public void SetInstalledVersion(GameVersion version)
    {
        InstalledVersion = version.ToString();
        LastCheckUtc = DateTime.UtcNow;
    }

    public static LocalState Load(string? path = null)
    {
        path ??= DefaultStatePath;

        if (!File.Exists(path))
            return new LocalState();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<LocalState>(json, JsonOptions) ?? new LocalState();
        }
        catch
        {
            return new LocalState();
        }
    }

    public void Save(string? path = null)
    {
        path ??= DefaultStatePath;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(path, json);
    }
}

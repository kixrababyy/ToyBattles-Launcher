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

    /// <summary>Remembers the installed game root for each server profile.</summary>
    public Dictionary<string, string?> ServerGameRoots { get; set; } = new();

    /// <summary>Cumulative playtime across all sessions, in seconds.</summary>
    public long TotalPlaytimeSeconds { get; set; } = 0;

    /// <summary>
    /// Set when the game is launched. Cleared when the session is recorded.
    /// If still set on next launch (e.g. launcher closed with game), elapsed time is recovered.
    /// </summary>
    public DateTime? PendingSessionStartUtc { get; set; } = null;

    /// <summary>
    /// Launcher version the user chose to permanently skip (e.g. "1.0.3").
    /// If the remote version matches this, the update prompt is suppressed.
    /// </summary>
    public string? SkippedLauncherVersion { get; set; } = null;

    /// <summary>
    /// Launcher version we started downloading and swapping to.
    /// If we launch again with the same old version and this is set to the same remote,
    /// the bat swap failed — show a manual download message instead of looping.
    /// </summary>
    public string? LastAttemptedUpdateVersion { get; set; } = null;

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
            var instance = JsonSerializer.Deserialize<LocalState>(json, JsonOptions) ?? new LocalState();

            // Migrate: if old single GameRootPath exists but no per-server entry, seed it
            if (!string.IsNullOrEmpty(instance.GameRootPath))
            {
                var activeServer = !string.IsNullOrEmpty(instance.ServerProfile)
                    ? instance.ServerProfile : "Main Build";
                if (!instance.ServerGameRoots.ContainsKey(activeServer))
                    instance.ServerGameRoots[activeServer] = instance.GameRootPath;
            }

            return instance;
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

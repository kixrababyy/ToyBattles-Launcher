using Launcher.Core.Models;

namespace Launcher.Core.Config;

/// <summary>
/// Parsed representation of patch.ini.
/// [patch]
/// version = ENG_2.0.4.3       ← latest version
/// version1 = ENG_2.0.4.2      ← known versions (unordered)
/// ...
/// exe = bin/MicroVolts.exe
/// </summary>
public class PatchConfig
{
    /// <summary>The latest (current) version on the server.</summary>
    public GameVersion LatestVersion { get; set; } = GameVersion.Empty;

    /// <summary>All known versions including latest, in the order they appear in the file.</summary>
    public List<GameVersion> AllVersions { get; set; } = new();

    /// <summary>Relative path to the game executable (e.g. bin/MicroVolts.exe).</summary>
    public string GameExePath { get; set; } = string.Empty;

    public static PatchConfig Load(string filePath)
    {
        var content = File.ReadAllText(filePath);
        return LoadFromContent(content);
    }

    public static PatchConfig LoadFromContent(string content)
    {
        var ini = IniParser.Parse(content);
        var config = new PatchConfig();

        if (!ini.TryGetValue("patch", out var patchSection))
            return config;

        // Parse "version" (the latest) and all "versionN" entries
        if (patchSection.TryGetValue("version", out var latestStr))
        {
            config.LatestVersion = GameVersion.Parse(latestStr);
            config.AllVersions.Add(config.LatestVersion);
        }

        // Collect version1..versionN
        for (int i = 1; i <= 1000; i++)
        {
            if (patchSection.TryGetValue($"version{i}", out var vStr))
            {
                var v = GameVersion.TryParse(vStr);
                if (v != null)
                    config.AllVersions.Add(v);
            }
            else
            {
                break;
            }
        }

        if (patchSection.TryGetValue("exe", out var exe))
            config.GameExePath = exe.Replace('/', '\\');

        return config;
    }
}

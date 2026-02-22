using Launcher.Core.Models;

namespace Launcher.Core.Config;

/// <summary>
/// Parsed representation of patchLauncher.ini.
/// [patch]
/// version = ENG_2.0.1.2
/// </summary>
public class PatchLauncherConfig
{
    public GameVersion LauncherVersion { get; set; } = GameVersion.Empty;

    public static PatchLauncherConfig Load(string filePath)
    {
        var ini = IniParser.ParseFile(filePath);
        var config = new PatchLauncherConfig();

        if (ini.TryGetValue("patch", out var patchSection))
        {
            if (patchSection.TryGetValue("version", out var vStr))
                config.LauncherVersion = GameVersion.Parse(vStr);
        }

        return config;
    }
}

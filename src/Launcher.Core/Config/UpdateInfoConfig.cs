namespace Launcher.Core.Config;

/// <summary>
/// Parsed representation of updateinfo.ini.
/// [update] addr = ... 
/// [FullFile] addr = ...
/// </summary>
public class UpdateInfoConfig
{
    /// <summary>Base URL for incremental patch downloads (e.g. http://cdn.toybattles.net/ENG).</summary>
    public string UpdateAddress { get; set; } = string.Empty;

    /// <summary>Base URL for full file downloads.</summary>
    public string FullFileAddress { get; set; } = string.Empty;

    public static UpdateInfoConfig Load(string filePath)
    {
        var ini = IniParser.ParseFile(filePath);
        var config = new UpdateInfoConfig();

        if (ini.TryGetValue("update", out var updateSection))
        {
            if (updateSection.TryGetValue("addr", out var addr))
                config.UpdateAddress = addr.TrimEnd('/');
        }

        if (ini.TryGetValue("FullFile", out var fullSection))
        {
            if (fullSection.TryGetValue("addr", out var addr))
                config.FullFileAddress = addr.TrimEnd('/');
        }

        return config;
    }

    public static UpdateInfoConfig LoadFromContent(string content)
    {
        var ini = IniParser.Parse(content);
        var config = new UpdateInfoConfig();

        if (ini.TryGetValue("update", out var updateSection))
        {
            if (updateSection.TryGetValue("addr", out var addr))
                config.UpdateAddress = addr.TrimEnd('/');
        }

        if (ini.TryGetValue("FullFile", out var fullSection))
        {
            if (fullSection.TryGetValue("addr", out var addr))
                config.FullFileAddress = addr.TrimEnd('/');
        }

        return config;
    }
}

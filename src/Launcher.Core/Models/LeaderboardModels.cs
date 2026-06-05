using System.Text.Json.Serialization;

namespace Launcher.Core.Models;

public class PlayerRank
{
    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("nickname")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; }
}

public class ClanRank
{
    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("clan_name")]
    public string Name { get; set; } = string.Empty;
}

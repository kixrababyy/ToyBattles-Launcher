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

    [JsonPropertyName("clan_front_icon")]
    public int FrontId { get; set; }

    [JsonPropertyName("clan_back_icon")]
    public int BackId { get; set; }

    [JsonIgnore]
    public string IconUrl => $"http://57.129.76.24:8000/api/clan-icon?frontId={FrontId}&backId={BackId}";
}

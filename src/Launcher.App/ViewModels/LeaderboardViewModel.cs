using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Launcher.Core.Models;

namespace Launcher.App.ViewModels;

public class LeaderboardViewModel : ViewModelBase
{
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

    public ObservableCollection<PlayerRank> TopPlayers { get; } = new();
    public ObservableCollection<ClanRank> TopClans { get; } = new();

    public LeaderboardViewModel()
    {
        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            // Fetch Top Players
            var players = await _httpClient.GetFromJsonAsync<PlayerRank[]>("http://57.129.76.24:8000/api/top-players");
            if (players != null)
            {
                TopPlayers.Clear();
                foreach (var p in players)
                {
                    TopPlayers.Add(p);
                }
            }

            // Fetch Top Clans
            var clans = await _httpClient.GetFromJsonAsync<ClanRank[]>("http://57.129.76.24:8000/api/top-clans");
            if (clans != null)
            {
                TopClans.Clear();
                foreach (var c in clans)
                {
                    TopClans.Add(c);
                }
            }
        }
        catch (Exception ex)
        {
            Launcher.Core.Services.LogService.LogError("Failed to fetch leaderboard data", ex);
        }
    }
}

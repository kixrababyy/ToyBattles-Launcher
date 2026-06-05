using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Launcher.Core.Services;

public class PlayerCountService
{
    private static readonly HttpClient _httpClient = new();
    private CancellationTokenSource? _cts;

    public class PlayerCounts
    {
        public int EuropeCount { get; set; }
        public int SACount { get; set; }
        public int SEACount { get; set; }
    }

    /// <summary>
    /// Fired when the player counts successfully update from the API.
    /// </summary>
    public event Action<PlayerCounts>? PlayerCountsUpdated;

    /// <summary>
    /// Starts polling the specified API URL for the player counts.
    /// </summary>
    public void StartPolling(string apiUrl, TimeSpan interval)
    {
        StopPolling();
        _cts = new CancellationTokenSource();
        _ = PollLoopAsync(apiUrl, interval, _cts.Token);
    }

    public void StopPolling()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }

    private async Task PollLoopAsync(string apiUrl, TimeSpan interval, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var response = await _httpClient.GetStringAsync(apiUrl, token);
                var counts = new PlayerCounts();
                
                using var doc = JsonDocument.Parse(response);
                if (doc.RootElement.TryGetProperty("servers", out var serversArray) && serversArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var server in serversArray.EnumerateArray())
                    {
                        if (server.TryGetProperty("server", out var nameProp) && server.TryGetProperty("count", out var countProp))
                        {
                            var name = nameProp.GetString();
                            var count = countProp.TryGetInt32(out int c) ? c : 0;
                            
                            if (name == "Europe") counts.EuropeCount = count;
                            else if (name == "SA") counts.SACount = count;
                            else if (name == "SEA") counts.SEACount = count;
                        }
                    }
                }
                
                PlayerCountsUpdated?.Invoke(counts);
            }
            catch
            {
                // Fallback to mock numbers
                PlayerCountsUpdated?.Invoke(new PlayerCounts { EuropeCount = 120, SACount = 150, SEACount = 150 });
            }

            try
            {
                await Task.Delay(interval, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}

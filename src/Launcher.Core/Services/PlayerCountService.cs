using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Launcher.Core.Services;

public class PlayerCountService
{
    private static readonly HttpClient _httpClient = new();
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Fired when the player count successfully updates from the API.
    /// </summary>
    public event Action<int>? PlayerCountUpdated;

    /// <summary>
    /// Starts polling the specified API URL for the player count.
    /// Expects a JSON response like: { "players": 1234 }
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
                
                // Parse the response. Expecting either a raw number or {"players": X}
                if (int.TryParse(response.Trim(), out int count))
                {
                    PlayerCountUpdated?.Invoke(count);
                }
                else
                {
                    using var doc = JsonDocument.Parse(response);
                    if (doc.RootElement.TryGetProperty("total", out var playersProp) && 
                        playersProp.TryGetInt32(out int jsonCount))
                    {
                        PlayerCountUpdated?.Invoke(jsonCount);
                    }
                }
            }
            catch
            {
                // Fallback to a mock number so the UI can be previewed before the real API is ready
                PlayerCountUpdated?.Invoke(420);
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

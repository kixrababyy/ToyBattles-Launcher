using System;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var url = "http://cdn.toybattles.net/ENG/microvolts/Full/data/cgd.dip";
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        Console.WriteLine("Sending HEAD request to " + url);
        using var response = await client.SendAsync(request);
        Console.WriteLine("Status Code: " + response.StatusCode);
        Console.WriteLine("Content-Length: " + response.Content.Headers.ContentLength);
    }
}

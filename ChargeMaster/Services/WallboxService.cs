using System.Net.Http.Json;
using ChargeMaster.Models;

namespace ChargeMaster.Services;

public class WallboxService(HttpClient httpClient) : IWallboxService
{
    public async Task<WallboxStatus?> GetStatusAsync()
    {
        try
        {
            // The /status endpoint is standard for these wallboxes to return JSON
            return await httpClient.GetFromJsonAsync<WallboxStatus>("status");
        }
        catch (HttpRequestException)
        {
            // Log error via Serilog if necessary
            return null;
        }
    }

    public async Task<DateTime?> GetTimeAsync()
    {
        var status = await GetStatusAsync();
        return status?.CurrentTime;
    }

    public async Task<bool> SetTimeAsync(DateTime dateTime)
    {
        try
        {
            var payload = new
            {
                offset = -60,
                tzName = "Europe/Stockholm",
                datetimeUTC = dateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            var response = await httpClient.PostAsJsonAsync("/servlet/rest/chargebox/time", payload);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}
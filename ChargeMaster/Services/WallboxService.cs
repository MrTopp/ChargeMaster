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
        catch (HttpRequestException ex)
        {
            // Log error via Serilog if necessary
            return null;
        }
    }
}
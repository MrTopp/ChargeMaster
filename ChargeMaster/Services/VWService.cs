using System.Text.Json;

using ChargeMaster.Models;

using Serilog;

namespace ChargeMaster.Services;

public class VWService(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public long AccessCounter
    {
        get => ++field;
        set => field = value;
    } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public async Task<VWStatusResponse?> GetStatus()
    {
        try
        {
            var json = await httpClient.GetStringAsync("/status");
            return JsonSerializer.Deserialize<VWStatusResponse>(json, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "Error fetching VW status");
            return null;
        }
    }

    public async Task<VWVehiclesResponse?> GetVehiclesAsync()
    {
        try
        {
            var json = await httpClient.GetStringAsync("/vehicles");
            return JsonSerializer.Deserialize<VWVehiclesResponse>(json, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "Error fetching VW vehicles");
            return null;
        }
    }

    public Task<bool> StartChargingAsync() => PostAsync("/start_charging");

    public Task<bool> StopChargingAsync() => PostAsync("/stop_charging");

    public Task<bool> StartClimatizationAsync() => PostAsync("/start_climatization");

    public Task<bool> StopClimatizationAsync() => PostAsync("/stop_climatization");

    private async Task<bool> PostAsync(string relativeUrl)
    {
        try
        {
            using var content = new StringContent(string.Empty);
            var response = await httpClient.PostAsync(relativeUrl, content);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "Error posting VW command");
            return false;
        }
    }

    
}

using System.Text.Json;

using Serilog;

namespace ChargeMaster.Services.VolksWagen;

/// <summary>
/// Interacting with Volkswagen vehicle services
/// </summary>
/// <remarks>This service encapsulates communication with Volkswagen vehicle APIs and handles error logging and
/// exception management for all operations. Methods may throw exceptions if the underlying service is unavailable or
/// returns an error response. This class is not thread-safe; if used concurrently, callers should ensure appropriate
/// synchronization.</remarks>
/// <param name="httpClient">The HTTP client used to send requests to the Volkswagen service endpoints. Must be configured with the appropriate
/// base address and authentication if required.</param>
/// <param name="logger">The logger used to record diagnostic and operational information for the service.</param>
public class VWService(HttpClient httpClient, ILogger<VWService> logger)
{
    private ILogger<VWService> Logger { get; } = logger;
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
        catch (Exception ex)
        {
            Logger.LogInformation(ex, "GetStatus: Exception");
            throw new CarConnectionException("GetStatus: Failed to fetch VW status");
        }
    }

    public async Task<VWVehiclesResponse?> GetVehiclesAsync()
    {
        try
        {
            var json = await httpClient.GetStringAsync("/vehicles");
            return JsonSerializer.Deserialize<VWVehiclesResponse>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetVehiclesAsync: Error fetching VW vehicles");
            throw new CarConnectionException("GetVehiclesAsync: Failed to fetch VW vehicles");
        }
    }

    public virtual Task<bool> StartChargingAsync() => PostAsync("/start_charging");

    public virtual Task<bool> StopChargingAsync() => PostAsync("/stop_charging");

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
        catch (Exception ex)
        {
            Log.Error(ex, "PostAsync: Error posting VW command to {url}", relativeUrl);
            throw new CarConnectionException($"PostAsync: Failed to post command to {relativeUrl}");
        }
    }
}

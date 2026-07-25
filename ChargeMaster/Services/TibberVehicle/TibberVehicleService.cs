using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ChargeMaster.Services.TibberVehicle;

/// <summary>
/// Begränsad version av TibberVehicleStatus för realtidsuppdateringar.
/// Innehåller endast de fält som är relevanta för statusuppdateringar.
/// </summary>
public class TibberVehicleStatusLimited
{
    public double? ChargingSettingsTargetLevel { get; set; }
    public double? BatteryLevel { get; set; }
    public double? ChargingPower { get; set; }
    public bool? IsCharging { get; set; }
    public string? ChargingStatus { get; set; }
    public string? ConnectorStatus { get; set; }
    public double? TimeToFullyCharged { get; set; }
    public double? RangeRemaining { get; set; }

    public TibberVehicleStatusLimited(TibberVehicleStatus status)
    {
        ChargingSettingsTargetLevel = status.ChargingSettingsTargetLevel;
        BatteryLevel = status.BatteryLevel;
        ChargingPower = status.ChargingPower;
        IsCharging = status.IsCharging;
        ChargingStatus = status.ChargingStatus;
        ConnectorStatus = status.ConnectorStatus;
        TimeToFullyCharged = status.TimeToFullyCharged;
        RangeRemaining = status.RangeRemaining;
    }
}

public class TibberVehicleStatusEventArgs : EventArgs
{
    /// <summary>
    /// Gets the vehicle status data.
    /// </summary>
    public TibberVehicleStatusLimited? Status { get; }

    public DateTime Timestamp { get; } = DateTime.Now;

    public TibberVehicleStatusEventArgs(TibberVehicleStatus status)
    {
        Status = new TibberVehicleStatusLimited(status);
    }
}

/// <summary>
/// Interaktion med Tibber-fordon via Tibber Data API.
/// </summary>
/// <remarks>
/// Den här tjänsten kapslar in kommunikation med Tibber Data API för fordonsstatus.
/// Den använder OAuth2 access token från Tibber-autentisering för att göra API-anrop.
/// </remarks>
public class TibberVehicleService(
    IOptions<TibberVehicleOptions> options,
    TibberOAuthService oauthService,
    HttpClient httpClient,
    ILogger<TibberVehicleService> logger) : IAsyncDisposable
{
    /// <summary>
    /// Event raised when vehicle data is successfully retrieved.
    /// </summary>
    public event EventHandler<TibberVehicleStatusEventArgs>? VehicleStatusRetrieved;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TibberVehicleOptions _options = options.Value;

    /// <summary>
    /// Hämtar aktuell status för fordonet från Tibber Data API.
    /// </summary>
    /// <returns>Fordonets status, eller null om svaret saknar statusdata eller autentisering misslyckades.</returns>
    public async Task<TibberVehicleStatus?> GetStatusAsync()
    {
        try
        {
            // Få OAuth2 access token
            var accessToken = await oauthService.GetValidAccessTokenAsync();
            if (accessToken == null)
            {
                // Error message already logged by GetValidAccessTokenAsync
                return null;
            }

            logger.LogInformation("Getting vehicle status from Tibber Data API");
            logger.LogInformation("Home ID: {HomeId}, Device ID: {DeviceId}", _options.HomeId, _options.DeviceId);

            var url = $"https://data-api.tibber.com/v1/homes/{_options.HomeId}/devices/{_options.DeviceId}";
            logger.LogInformation("Endpoint URL: {Url}", url);

            var request = new HttpRequestMessage(HttpMethod.Get, url);

            // Use OAuth2 access token
            request.Headers.Add("Authorization", $"Bearer {accessToken}");
            logger.LogInformation("Using OAuth2 access token for authentication");

            var response = await httpClient.SendAsync(request);

            logger.LogInformation("Vehicle status response status: {Status}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError("Kunde inte hämta fordonsstatus från Tibber");
                logger.LogError("HTTP Status: {Status}", response.StatusCode);
                logger.LogError("Error response: {ErrorContent}", errorContent);

                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            logger.LogInformation("Vehicle status response JSON: {Json}", json);

            var deviceResponse = JsonSerializer.Deserialize<TibberVehicleStatusResponse>(json, JsonOptions);

            if (deviceResponse != null)
            {
                var status = TibberVehicleStatus.FromDevice(new TibberVehicleDevice
                {
                    Id = deviceResponse.Id,
                    ExternalId = deviceResponse.ExternalId,
                    Info = deviceResponse.Info,
                    Status = deviceResponse.Status,
                    Attributes = deviceResponse.Attributes,
                    Capabilities = deviceResponse.Capabilities
                });

                VehicleStatusRetrieved?.Invoke(this, new TibberVehicleStatusEventArgs(status));
                return status;
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Kunde inte hämta fordonsstatus från Tibber");
            return null;
        }
    }
    
    private async Task<bool> SendCommandAsync(string commandPath)
    {
        try
        {
            var accessToken = await oauthService.GetValidAccessTokenAsync();
            if (accessToken == null)
            {
                logger.LogError("Kunde inte få tillgång till OAuth2 access token för Tibber Data API");
                return false;
            }

            var url = $"https://data-api.tibber.com/v1/homes/{_options.HomeId}/devices/{_options.DeviceId}{commandPath}";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Authorization", $"Bearer {accessToken}");

            var response = await httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid skickning av kommando till Tibber: {Command}", commandPath);
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        httpClient?.Dispose();
        await Task.CompletedTask;
    }
}

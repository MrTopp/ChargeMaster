using System.Text.Json;

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

    public TibberVehicleStatusLimited(TibberVehicleStatus status)
    {
        ChargingSettingsTargetLevel = status.ChargingSettingsTargetLevel;
        BatteryLevel = status.BatteryLevel;
        ChargingPower = status.ChargingPower;
        IsCharging = status.IsCharging;
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
/// Interaktion med Tibber-fordon via OAuth2-autentisering.
/// </summary>
/// <remarks>
/// Den här tjänsten kapslar in kommunikation med Tibber Vehicle API och hanterar OAuth2-tokens.
/// Metoder kan kasta undantag om autentisering misslyckas eller den underliggande tjänsten är otillgänglig.
/// </remarks>
public class TibberVehicleService(
    TibberOAuthService oauthService,
    HttpClient httpClient,
    ILogger<TibberVehicleService> logger) : IAsyncDisposable
{
    /// <summary>
    /// Event raised when vehicle data is successfully retrieved.
    /// </summary>
    public event EventHandler<TibberVehicleStatusEventArgs>? VehicleStatusRetrieved;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Hämtar aktuell status för fordonet från Tibber.
    /// </summary>
    /// <returns>Fordonets status, eller null om svaret saknar statusdata eller autentisering misslyckades.</returns>
    public async Task<TibberVehicleStatus?> GetStatusAsync()
    {
        try
        {
            var accessToken = await oauthService.GetValidAccessTokenAsync();
            if (accessToken == null)
            {
                logger.LogError("Kunde inte få tillgång till access token för Tibber");
                return null;
            }

            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.tibber.com/v4-3/vehicles/status");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    logger.LogWarning("Obehörig åtkomst till Tibber API, försöker uppdatera tokens");
                    var refreshed = await oauthService.RefreshTokenAsync();
                    if (!refreshed)
                    {
                        logger.LogError("Kunde inte uppdatera Tibber-tokens. Användaren behöver omautentisera.");
                    }
                }
                else
                {
                    logger.LogError("Fel vid hämtning av fordonsstatus från Tibber. Status: {Status}", 
                        response.StatusCode);
                }
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<TibberVehicleStatusResponse>(json, JsonOptions);

            if (responseData?.Status != null)
            {
                VehicleStatusRetrieved?.Invoke(this, new TibberVehicleStatusEventArgs(responseData.Status));
                return responseData.Status;
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av fordonsstatus från Tibber");
            return null;
        }
    }

    /// <summary>
    /// Hämtar en lista över registrerade fordon från Tibber.
    /// </summary>
    /// <returns>Svar med fordonsdata, eller null vid kommunikationsfel.</returns>
    public async Task<TibberVehiclesResponse?> GetVehiclesAsync()
    {
        try
        {
            var accessToken = await oauthService.GetValidAccessTokenAsync();
            if (accessToken == null)
            {
                logger.LogError("Kunde inte få tillgång till access token för Tibber");
                return null;
            }

            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.tibber.com/v4-3/vehicles");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Fel vid hämtning av fordonsdata från Tibber. Status: {Status}", 
                    response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TibberVehiclesResponse>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av fordonsdata från Tibber");
            return null;
        }
    }

    /// <summary>
    /// Startar laddning av fordonet.
    /// </summary>
    public async Task<bool> StartChargingAsync()
    {
        return await SendCommandAsync("/start-charging");
    }

    /// <summary>
    /// Stoppar laddning av fordonet.
    /// </summary>
    public async Task<bool> StopChargingAsync()
    {
        return await SendCommandAsync("/stop-charging");
    }

    /// <summary>
    /// Startar klimatisering av fordonet.
    /// </summary>
    public async Task<bool> StartClimatizationAsync()
    {
        return await SendCommandAsync("/start-climatization");
    }

    /// <summary>
    /// Stoppar klimatisering av fordonet.
    /// </summary>
    public async Task<bool> StopClimatizationAsync()
    {
        return await SendCommandAsync("/stop-climatization");
    }

    private async Task<bool> SendCommandAsync(string commandPath)
    {
        try
        {
            var accessToken = await oauthService.GetValidAccessTokenAsync();
            if (accessToken == null)
            {
                logger.LogError("Kunde inte få tillgång till access token för Tibber");
                return false;
            }

            var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.tibber.com/v4-3/vehicles{commandPath}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

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

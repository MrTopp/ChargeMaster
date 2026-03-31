using System.Text.Json;

namespace ChargeMaster.Services.VolksWagen;

/// <summary>
/// Begränsad version av VWStatus. Innehåller endast de fält som är relevanta för
/// statusuppdateringar i realtid. Minskar mängden känslig information
/// som UI har tillgång till och även storleken på skickad information.
/// </summary>
public class VWStatusLimited
{
    public double? ChargingSettingsTargetLevel { get; set; }
    public double? BatteryLevel { get; set; }

    public VWStatusLimited(VWStatus status)
    {
        ChargingSettingsTargetLevel = status.ChargingSettingsTargetLevel;
        BatteryLevel = status.BatteryLevel;
    }
}

public class VWStatusEventArgs : EventArgs
{
    /// <summary>
    /// Gets the vehicle status data.
    /// </summary>
    public VWStatusLimited? VWStatusLimited { get; }

    public DateTime Timestamp { get; } = DateTime.Now;

    /// <summary>
    /// Initializes a new instance of the <see cref="VWStatusEventArgs"/> class.
    /// </summary>
    /// <param name="status">The vehicle status data.</param>
    public VWStatusEventArgs(VWStatus status)
    {
        VWStatusLimited = new VWStatusLimited(status);
    }
}

/// <summary>
/// Interaktion med Volkswagen-tjänster
/// </summary>
/// <remarks>Den här tjänsten kapslar in kommunikation med Volkswagen-API:er och hanterar fellogning och
/// undantagshantering för alla operationer. Metoder kan kasta undantag om den underliggande tjänsten är otillgänglig eller
/// returnerar ett felsvar. Den här klassen är inte trådsäker; om den används samtidigt bör anropare se till att göra lämplig
/// synkronisering.
/// </remarks>
/// <param name="httpClient">HTTP-klienten som används för att skicka förfrågningar till Volkswagen-tjänstens slutpunkter. Måste konfigureras med lämplig
/// basadress och autentisering om det krävs.</param>
/// <param name="logger">Loggern som används för att registrera diagnostisk och operativ information för tjänsten.</param>
public class VWService(HttpClient httpClient, ILogger<VWService> logger)
{
    /// <summary>
    /// Event raised when vehicle data is successfully retrieved.
    /// </summary>
    public event EventHandler<VWStatusEventArgs>? VWStatusRetrieved;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Hämtar aktuell status för fordonet från Volkswagen-tjänsten.
    /// </summary>
    /// <returns>Fordonets status, eller null om svaret saknar statusdata.</returns>
    /// <exception cref="CarConnectionException">Kastas om kommunikation med tjänsten misslyckas.</exception>
    public async Task<VWStatus?> GetStatusAsync()
    {
        try
        {
            var json = await httpClient.GetStringAsync("/status");
            var response = JsonSerializer.Deserialize<VWStatusResponse>(json, JsonOptions);
            if (response?.Status != null)
            {
                VWStatusRetrieved?.Invoke(this, new VWStatusEventArgs(response.Status));
            }

            return response?.Status;
        }
        catch (TaskCanceledException ex)
        {
            logger.LogInformation("GetStatusAsync: Förfrågan avbröts {message}", ex.Message);
            throw new CarConnectionException("GetStatusAsync: Kunde inte hämta VW-status", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetStatusAsync: Fel vid hämtning av VW-status");
            throw new CarConnectionException("GetStatusAsync: Kunde inte hämta VW-status", ex);
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
            logger.LogError(ex, "GetVehiclesAsync: Fel vid hämtning av VW-fordon");
            throw new CarConnectionException("GetVehiclesAsync: Kunde inte hämta VW-fordon", ex);
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
            logger.LogError(ex, "PostAsync: Fel vid skickning av VW-kommando till {url}", relativeUrl);
            throw new CarConnectionException($"PostAsync: Kunde inte skicka kommando till {relativeUrl}", ex);
        }
    }
}

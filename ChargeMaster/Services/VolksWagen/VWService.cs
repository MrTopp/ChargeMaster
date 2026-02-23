using System.Text.Json;

namespace ChargeMaster.Services.VolksWagen;

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
            logger.LogInformation(ex, "GetStatus: Undantag");
            throw new CarConnectionException("GetStatus: Kunde inte hämta VW-status");
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
            throw new CarConnectionException("GetVehiclesAsync: Kunde inte hämta VW-fordon");
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
            throw new CarConnectionException($"PostAsync: Kunde inte skicka kommando till {relativeUrl}");
        }
    }
}

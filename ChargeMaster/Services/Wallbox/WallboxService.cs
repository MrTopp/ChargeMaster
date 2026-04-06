using System.Text.Json;

namespace ChargeMaster.Services.Wallbox;

/// <summary>
/// Åtkomst till Garo wallbox via HTTP-gränssnitt
/// </summary>
/// <param name="httpClient">HTTP-klient konfigurerad med wallbox-basadress.</param>
/// <param name="logger">Logger för diagnostisk information.</param>
public class WallboxService(HttpClient httpClient, ILogger<WallboxService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private long AccessCounter
    {
        get => ++field;
        set => field = value;
    } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Hämtar aktuell status från wallboxen.
    /// </summary>
    /// <returns>Wallbox-status, eller <c>null</c> vid kommunikationsfel.</returns>
    public async Task<WallboxStatus?> GetStatusAsync()
    {
        try
        {
            var url = $"servlet/rest/chargebox/status?_={AccessCounter}";
            var json = await httpClient.GetStringAsync(url);
            var retval = JsonSerializer.Deserialize<WallboxStatus>(json, JsonOptions);
            return retval;
        }
        catch (TaskCanceledException)
        {
            // Händer ibland, får vi leva med
            logger.LogError("Timeout vid hämtning av wallbox-status");
            return null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Fel vid hämtning av wallbox-status");
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Ogiltigt JSON-svar vid hämtning av wallbox-status");
            return null;
        }
    }
    
    /// <summary>
    /// Ställ in wallbox-tid
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public async Task<bool> SetTimeAsync(DateTime dateTime)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
            var offsetMinutes = tz.IsDaylightSavingTime(dateTime) ? -60 : 0;

            var payload = new
            {
                offset = offsetMinutes,
                tzName = "Europe/Stockholm",
                datetimeUTC = dateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            var response
                = await httpClient.PostAsJsonAsync("/servlet/rest/chargebox/time", payload);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    /// <summary>
    /// Ställer in driftläge på wallboxen.
    /// </summary>
    /// <param name="mode">Önskat driftläge.</param>
    /// <returns><c>true</c> om anropet lyckades, annars <c>false</c>.</returns>
    public async Task<bool> SetModeAsync(WallboxMode mode)
    {
        try
        {
            string modeString = mode switch
            {
                WallboxMode.Available => "ALWAYS_ON",
                WallboxMode.NotAvailable => "ALWAYS_OFF",
                WallboxMode.TimerControlled => "SCHEMA",
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };

            // Wallboxen förväntar vanligtvis nyttolasten på /servlet/rest/chargebox/mode
            logger.LogInformation("Ställer in wallbox-läge till {Mode}", modeString);
            var response
                = await httpClient.PostAsync($"/servlet/rest/chargebox/mode/{modeString}", null);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    /// <summary>
    /// Hämtar mätarinformation från wallboxens externa mätare.
    /// </summary>
    /// <returns>Mätarinformation, eller <c>null</c> vid kommunikationsfel.</returns>
    public virtual async Task<WallboxMeterInfo?> GetMeterInfoAsync()
    {
        try
        {
            var url = $"servlet/rest/chargebox/meterinfo/EXTERNAL?_={AccessCounter}";
            var json = await httpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<WallboxMeterInfo>(json, JsonOptions);
        }
        catch (TaskCanceledException)
        {
            // Händer ibland, får vi leva med
            logger.LogError("Timeout vid hämtning av wallbox-mäterinformation");
            return null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Fel vid hämtning av wallbox-mäterinformation");
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Ogiltigt JSON-svar vid hämtning av wallbox-mäterinformation");
            return null;
        }
    }

    /// <summary>
    /// Hämtar laddscheman konfigurerade i wallboxen.
    /// </summary>
    /// <returns>Lista med schemaposter, eller <c>null</c> vid kommunikationsfel.</returns>
    public async Task<IReadOnlyList<WallboxSchemaEntry>?> GetSchemaAsync()
    {
        try
        {
            var url = $"servlet/rest/chargebox/schema?_={AccessCounter}";
            var json = await httpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<List<WallboxSchemaEntry>>(json, JsonOptions);
        }
        catch (TaskCanceledException)
        {
            logger.LogError("Timeout vid hämtning av wallbox-schema");
            return null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Fel vid hämtning av wallbox-schema");
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Ogiltigt JSON-svar vid hämtning av wallbox-schema");
            return null;
        }
    }

    /// <summary>
    /// Hämtar konfigurationen från wallboxen.
    /// </summary>
    /// <returns>Wallbox-konfiguration, eller <c>null</c> vid kommunikationsfel.</returns>
    public async Task<WallboxConfig?> GetConfigAsync()
    {
        try
        {
            var url = $"servlet/rest/chargebox/config?_={AccessCounter}";
            var json = await httpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<WallboxConfig>(json, JsonOptions);
        }
        catch (TaskCanceledException)
        {
            logger.LogError("Timeout vid hämtning av wallbox-konfiguration");
            return null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Fel vid hämtning av wallbox-konfiguration");
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Ogiltigt JSON-svar vid hämtning av wallbox-konfiguration");
            return null;
        }
    }

    /// <summary>
    /// Hämtar konfiguration för anslutna slave-enheter till wallboxen.
    /// </summary>
    /// <returns>Lista med slave-konfigurationer, eller <c>null</c> vid kommunikationsfel.</returns>
    public async Task<IReadOnlyList<WallboxSlaveConfig>?> GetSlavesAsync()
    {
        try
        {
            var url = $"servlet/rest/chargebox/slaves/false?_={AccessCounter}";
            var json = await httpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<List<WallboxSlaveConfig>>(json, JsonOptions);
        }
        catch (TaskCanceledException)
        {
            logger.LogError("Timeout vid hämtning av wallbox-slavar");
            return null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Fel vid hämtning av wallbox-slavar");
            return null;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Ogiltigt JSON-svar vid hämtning av wallbox-slavar");
            return null;
        }
    }

    /// <summary>
    /// Sparar eller uppdaterar ett laddschema i wallboxen.
    /// </summary>
    /// <param name="schemaEntry">Schemaposten som ska sparas.</param>
    /// <returns><c>true</c> om anropet lyckades, annars <c>false</c>.</returns>
    public async Task<bool> SetSchemaAsync(WallboxSchemaEntry schemaEntry)
    {
        try
        {
            var url = "/servlet/rest/chargebox/schema";
            var response = await httpClient.PostAsJsonAsync(url, schemaEntry, JsonOptions);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Fel vid inställning av wallbox-schema");
            return false;
        }
    }

    /// <summary>
    /// Tar bort ett laddschema från wallboxen.
    /// </summary>
    /// <param name="schemaId">Id för schemat som ska tas bort.</param>
    /// <returns><c>true</c> om anropet lyckades, annars <c>false</c>.</returns>
    public async Task<bool> DeleteSchemaAsync(int schemaId)
    {
        try
        {
            var url = $"/servlet/rest/chargebox/schema/{schemaId}";
            var response = await httpClient.DeleteAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Fel vid borttagning av wallbox-schema {SchemaId}", schemaId);
            return false;
        }
    }
}

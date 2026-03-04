using System.Text.Json;

namespace ChargeMaster.Services.Wallbox;

    /// <summary>
    /// Åtkomst till Garo wallbox via HTTP-gränssnitt
    /// </summary>
    /// <param name="httpClient"></param>
    public class WallboxService(HttpClient httpClient, ILogger<WallboxService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public long AccessCounter
    {
        get => ++field;
        set => field = value;
    } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

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
    }

    public async Task<IReadOnlyList<WallboxSchemaEntry>?> GetSchemaAsync()
    {
        try
        {
            var url = $"servlet/rest/chargebox/schema?_={AccessCounter}";
            var json = await httpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<List<WallboxSchemaEntry>>(json, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Fel vid hämtning av wallbox-schema");
            return null;
        }
    }

    public async Task<WallboxConfig?> GetConfigAsync()
    {
        try
        {
            var url = $"servlet/rest/chargebox/config?_={AccessCounter}";
            var json = await httpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<WallboxConfig>(json, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Fel vid hämtning av wallbox-konfiguration");
            return null;
        }
    }

    public async Task<IReadOnlyList<WallboxSlaveConfig>?> GetSlavesAsync()
    {
        try
        {
            var url = $"servlet/rest/chargebox/slaves/false?_={AccessCounter}";
            var json = await httpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<List<WallboxSlaveConfig>>(json, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Fel vid hämtning av wallbox-slavar");
            return null;
        }
    }

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

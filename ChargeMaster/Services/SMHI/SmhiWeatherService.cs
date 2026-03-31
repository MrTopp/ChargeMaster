using System.Globalization;
using System.Text.Json;

namespace ChargeMaster.Services.SMHI;

/// <summary>
/// Tjänst för att hämta väderprognos från SMHI.
/// Hämtar temperatur (verklig och upplevd) för närmaste 12 timmarna.
/// </summary>
public class SmhiWeatherService(
    HttpClient httpClient,
    ILogger<SmhiWeatherService> logger,
    IServiceScopeFactory serviceScopeFactory)
{
    private const string SmhiBaseUrl = "https://opendata-download-metfcst.smhi.se/api/category/pmp3g/version/2/geotype/point";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Hämta väderprognos för en specifik plats.
    /// </summary>
    /// <param name="longitude">Longitud (t.ex. 18.0686 för Stockholm)</param>
    /// <param name="latitude">Latitud (t.ex. 59.3293 för Stockholm)</param>
    /// <returns>Lista med väderdata, för närvarande 3 dygn framåt</returns>
    public async Task<List<WeatherForecast>> GetForecastAsync(double longitude, double latitude)
    {
        try
        {
            var url = $"{SmhiBaseUrl}/lon/{longitude.ToString("F4", 
                CultureInfo.InvariantCulture)}/lat/{latitude.ToString("F4", CultureInfo.InvariantCulture)}/data.json";
            
            logger.LogInformation("Fetching weather forecast from SMHI for coordinates: {Lat},{Lon}", 
                latitude, longitude);

            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<SmhiWeatherResponse>(content, JsonOptions);

            if (data?.TimeSeries == null || data.TimeSeries.Count == 0)
            {
                logger.LogWarning("No weather data received from SMHI");
                return [];
            }

            var now = DateTime.UtcNow;
            var forecasts = data.TimeSeries
                .Where(ts => ts.Time >= now)
                .Select(ts => new WeatherForecast
                {
                    Time = ts.Time,
                    Temperature = GetParameterValue(ts.Parameters, "t") ?? 0,
                    CloudCoverage = GetParameterValue(ts.Parameters, "tcc_mean") is double tcc
                        ? tcc / 8.0 * 100
                        : null,
                    Precipitation = GetParameterValue(ts.Parameters, "pmin"),
                    MaxPrecipitation = GetParameterValue(ts.Parameters, "pmax"),
                    MeanPrecipitation = GetParameterValue(ts.Parameters, "pmean"),
                    WindSpeed = GetParameterValue(ts.Parameters, "ws"),
                    WindGust = GetParameterValue(ts.Parameters, "gust"),
                    WindDirection = (int?)GetParameterValue(ts.Parameters, "wd"),
                    Luftfuktighet = (int?)GetParameterValue(ts.Parameters, "r"),
                    Lufttryck = GetParameterValue(ts.Parameters, "msl"),
                    Sikt = GetParameterValue(ts.Parameters, "vis"),
                    ThunderstormProbability = (int?)GetParameterValue(ts.Parameters, "tstm"),
                    PrecipitationMedian = GetParameterValue(ts.Parameters, "pmedian"),
                    PrecipitationProbability = (int?)GetParameterValue(ts.Parameters, "spp"),
                    PrecipitationCategory = (int?)GetParameterValue(ts.Parameters, "pcat"),
                    WeatherSymbol = (int?)GetParameterValue(ts.Parameters, "Wsymb2"),
                    TotalPrecipitation = GetParameterValue(ts.Parameters, "tp")
                })
                .OrderBy(f => f.Time)
                .ToList();

            logger.LogDebug("Retrieved {Count} weather forecast entries from SMHI", forecasts.Count);

            // Spara väderdata i databasen via repository
            using var scope = serviceScopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IWeatherForecastRepository>();
            await repository.SaveForecastsAsync(forecasts);

            return forecasts;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to fetch weather forecast from SMHI");
            return [];
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse SMHI weather response");
            return [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while fetching weather forecast");
            return [];
        }
    }

    /// <summary>
    /// Hämta väderprognos för strömtorp.
    /// </summary>
    /// <returns>Lista med väderdata</returns>
    public async Task<List<WeatherForecast>> GetForecastAsync()
    {
        // Koordinater för strömtorp
        return await GetForecastAsync(longitude: 14.4308, latitude: 59.2301);
    }

    /// <summary>
    /// Extrahera värde från parameters-array baserat på parameternamn.
    /// </summary>
    /// <param name="parameters">SMHI parameters array</param>
    /// <param name="name">Parameternamn (t.ex. "t", "vis", "ws")</param>
    /// <returns>Första värdet från parametern, eller null om den inte finns</returns>
    private static double? GetParameterValue(List<SmhiParameter>? parameters, string name)
    {
        return parameters?.FirstOrDefault(p => p.Name == name)?.Values?.FirstOrDefault();
    }
}

namespace ChargeMaster.Services.SMHI;

/// <summary>
/// Tjänst för att hämta väderprognos från SMHI.
/// Hämtar temperatur (verklig och upplevd) för närmaste 12 timmarna.
/// </summary>
public class SmhiWeatherService(HttpClient httpClient, ILogger<SmhiWeatherService> logger)
{
    private const string SmhiBaseUrl = "https://opendata-download-metfcst.smhi.se/api/category/pmp3g/version/2/geotype/point";

    /// <summary>
    /// Hämta väderprognos för närmaste 12 timmarna för en specifik plats.
    /// </summary>
    /// <param name="longitude">Longitud (t.ex. 18.0686 för Stockholm)</param>
    /// <param name="latitude">Latitud (t.ex. 59.3293 för Stockholm)</param>
    /// <returns>Lista med väderdata för närmaste 12 timmarna</returns>
    public async Task<List<WeatherForecast>> GetForecastAsync(double longitude, double latitude)
    {
        try
        {
            var url = $"{SmhiBaseUrl}/lon/{longitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}/lat/{latitude.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}/data.json";
            
            logger.LogInformation("Fetching weather forecast from SMHI for coordinates: {Lat},{Lon}", 
                latitude, longitude);

            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };
            var data = System.Text.Json.JsonSerializer.Deserialize<SmhiWeatherResponse>(content, options);

            if (data?.TimeSeries == null || data.TimeSeries.Count == 0)
            {
                logger.LogWarning("No weather data received from SMHI");
                return [];
            }

            var now = DateTime.UtcNow;
            var forecasts = data.TimeSeries
                .Where(ts => ts.Time >= now && ts.Time <= now.AddHours(12))
                .Select(ts => new WeatherForecast
                {
                    Time = ts.Time,
                    Temperature = GetParameterValue(ts.Parameters, "t") ?? 0,
                    CloudCoverage = GetParameterValue(ts.Parameters, "n"),
                    Precipitation = GetParameterValue(ts.Parameters, "pmin"),
                    WindSpeed = GetParameterValue(ts.Parameters, "ws")
                })
                .OrderBy(f => f.Time)
                .ToList();

            logger.LogInformation("Retrieved {Count} weather forecast entries from SMHI", forecasts.Count);
            
            return forecasts;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to fetch weather forecast from SMHI");
            return [];
        }
        catch (System.Text.Json.JsonException ex)
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
    /// Hämta väderprognos för Stockholm (default position).
    /// </summary>
    /// <returns>Lista med väderdata för närmaste 12 timmarna</returns>
    public async Task<List<WeatherForecast>> GetForecastForStockholmAsync()
    {
        // Koordinater för Stockholm
        return await GetForecastAsync(longitude: 18.0686, latitude: 59.3293);
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

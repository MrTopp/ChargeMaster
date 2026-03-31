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
            var url = $"https://opendata-download-metfcst.smhi.se/api/category/snow1g/version/1/geotype/point/lon/{longitude.ToString("F3",
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
                .Where(ts => ts.Time >= now && ts.Data != null)
                .Select(ts => new WeatherForecast
                {
                    Time = ts.Time,
                    Temperature = ts.Data!.AirTemperature ?? 0,
                    CloudCoverage = ts.Data.CloudAreaFraction is double tcc
                        ? tcc / 8.0 * 100
                        : null,
                    Precipitation = ts.Data.PrecipitationAmountMin,
                    MaxPrecipitation = ts.Data.PrecipitationAmountMax,
                    MeanPrecipitation = ts.Data.PrecipitationAmountMean,
                    WindSpeed = ts.Data.WindSpeed,
                    WindGust = ts.Data.WindSpeedOfGust,
                    WindDirection = (int?)ts.Data.WindFromDirection,
                    Luftfuktighet = (int?)ts.Data.RelativeHumidity,
                    Lufttryck = ts.Data.AirPressureAtMeanSeaLevel,
                    Sikt = ts.Data.VisibilityInAir,
                    ThunderstormProbability = (int?)ts.Data.ThunderstormProbability,
                    PrecipitationMedian = ts.Data.PrecipitationAmountMedian,
                    PrecipitationProbability = (int?)ts.Data.ProbabilityOfPrecipitation,
                    PrecipitationCategory = (int?)ts.Data.PredominantPrecipitationTypeAtSurface,
                    WeatherSymbol = (int?)ts.Data.SymbolCode,
                    TotalPrecipitation = ts.Data.PrecipitationAmountMeanDeterministic
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

    }

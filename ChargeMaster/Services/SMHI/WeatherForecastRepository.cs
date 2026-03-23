namespace ChargeMaster.Services.SMHI;

using ChargeMaster.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Repository för att hantera väderprognos-data i databasen.
/// Abstraherar all databasåtkomst för väderprognos.
/// </summary>
public class WeatherForecastRepository(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<WeatherForecastRepository> logger) : IWeatherForecastRepository
{
    /// <summary>
    /// Sparar eller uppdaterar väderprognos i databasen.
    /// </summary>
    /// <param name="forecasts">Lista med väderprognos-data att spara</param>
    public async Task SaveForecastsAsync(List<WeatherForecast> forecasts)
    {
        try
        {
            // Använd IServiceScopeFactory för att skapa ett scope för databasanrop
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            foreach (var forecast in forecasts)
            {
                // Kontrollera om en prognos redan finns för denna tid
                var existingForecast = await dbContext.WeatherForecasts
                    .FirstOrDefaultAsync(f => f.Time == forecast.Time);

                if (existingForecast != null)
                {
                    // Uppdatera befintligt värde
                    existingForecast.Temperature = forecast.Temperature;
                    existingForecast.CloudCoverage = forecast.CloudCoverage;
                    existingForecast.Precipitation = forecast.Precipitation;
                    existingForecast.MaxPrecipitation = forecast.MaxPrecipitation;
                    existingForecast.MeanPrecipitation = forecast.MeanPrecipitation;
                    existingForecast.WindSpeed = forecast.WindSpeed;
                    existingForecast.WindGust = forecast.WindGust;
                    existingForecast.WindDirection = forecast.WindDirection;
                    existingForecast.Luftfuktighet = forecast.Luftfuktighet;
                    existingForecast.Lufttryck = forecast.Lufttryck;
                    existingForecast.Sikt = forecast.Sikt;
                    existingForecast.ThunderstormProbability = forecast.ThunderstormProbability;
                    existingForecast.PrecipitationMedian = forecast.PrecipitationMedian;
                    existingForecast.PrecipitationProbability = forecast.PrecipitationProbability;
                    existingForecast.PrecipitationCategory = forecast.PrecipitationCategory;
                    existingForecast.WeatherSymbol = forecast.WeatherSymbol;
                    existingForecast.TotalPrecipitation = forecast.TotalPrecipitation;

                    dbContext.WeatherForecasts.Update(existingForecast);
                }
                else
                {
                    // Lägg till ny prognos
                    dbContext.WeatherForecasts.Add(forecast);
                }
            }

            await dbContext.SaveChangesAsync();
            logger.LogInformation("Saved {Count} weather forecasts to database", forecasts.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save weather forecasts to database");
            throw;
        }
    }
}

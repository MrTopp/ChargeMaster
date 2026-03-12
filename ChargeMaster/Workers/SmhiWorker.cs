using ChargeMaster.Services.SMHI;

namespace ChargeMaster.Workers;

/// <summary>
/// Event args för väderdata-uppdateringar.
/// </summary>
public class WeatherForecastUpdatedEventArgs : EventArgs
{
    /// <summary>
    /// Den uppdaterade väderprognosen.
    /// </summary>
    public required List<WeatherForecast> Forecast { get; set; }

    /// <summary>
    /// Tidpunkt för uppdateringen.
    /// </summary>
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// Bakgrundstjänst som en gång i timmen hämtar väderprognos från SMHI.
/// Väderdata lagras i minnet för användning av andra tjänster.
/// </summary>
public class SmhiWorker(
    SmhiWeatherService smhiWeatherService,
    ILogger<SmhiWorker> logger) : BackgroundService
{
    private const int HourlyIntervalMinutes = 60;

    /// <summary>
    /// Aktuell väderprognos för Strömtorp (lagras efter senaste hämtningen).
    /// </summary>
    public List<WeatherForecast> CurrentForecast { get; private set; } = [];

    /// <summary>
    /// Tidpunkt för senaste lyckade väderdata-hämtningen.
    /// </summary>
    public DateTime LastUpdate { get; private set; } = DateTime.MinValue;

    /// <summary>
    /// Event som publiceras när väderprognosen uppdateras.
    /// </summary>
    public event EventHandler<WeatherForecastUpdatedEventArgs>? WeatherForecastUpdated;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Hämta väderdata vid start
        await FetchWeatherAsync(stoppingToken);

        // Schemalägga timliga hämtningar
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Vänta en timme
                await Task.Delay(TimeSpan.FromMinutes(HourlyIntervalMinutes), stoppingToken);
                
                await FetchWeatherAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fel vid hämtning av väderdata från SMHI");
            }
        }
    }

    /// <summary>
    /// Hämtar väderprognos från SMHI för Strömtorp.
    /// </summary>
    private async Task FetchWeatherAsync(CancellationToken stoppingToken)
    {
        try
        {
            var forecast = await smhiWeatherService.GetForecast();

            if (forecast.Count > 0)
            {
                CurrentForecast = forecast;
                LastUpdate = DateTime.UtcNow;

                var firstTemp = forecast.FirstOrDefault()?.Temperature ?? 0;
                logger.LogInformation(
                    "SMHI väderdata uppdaterad: {Count} prognoser, nu {Temperature}°C",
                    forecast.Count, firstTemp);

                // Publicera event när väderdata uppdateras
                OnWeatherForecastUpdated(new WeatherForecastUpdatedEventArgs
                {
                    Forecast = forecast,
                    UpdateTime = DateTime.UtcNow
                });
            }
            else
            {
                logger.LogWarning("Ingen väderdata mottagen från SMHI");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Okänt fel vid hämtning av väderdata");
        }
    }

    /// <summary>
    /// Publicerar WeatherForecastUpdated-event.
    /// </summary>
    protected virtual void OnWeatherForecastUpdated(WeatherForecastUpdatedEventArgs e)
    {
        WeatherForecastUpdated?.Invoke(this, e);
    }

    /// <summary>
    /// Prenumerera på väderdata-uppdateringar. Publicerar omedelbar nuvarande data vid prenumeration.
    /// </summary>
    /// <param name="handler">Event handler att registrera</param>
    public void Subscribe(EventHandler<WeatherForecastUpdatedEventArgs> handler)
    {
        WeatherForecastUpdated += handler;

        // Publicera omedelbar nuvarande data till ny prenumerant
        if (CurrentForecast.Count > 0)
        {
            OnWeatherForecastUpdated(new WeatherForecastUpdatedEventArgs
            {
                Forecast = CurrentForecast,
                UpdateTime = LastUpdate
            });
        }
        else
        {
            logger.LogInformation("Ny prenumeration på väderdata, men ingen data tillgänglig ännu");
        }
    }

    /// <summary>
    /// Avprenumerera från väderdata-uppdateringar.
    /// </summary>
    /// <param name="handler">Event handler att avregistrera</param>
    public void Unsubscribe(EventHandler<WeatherForecastUpdatedEventArgs> handler)
    {
        WeatherForecastUpdated -= handler;
    }

    /// <summary>
    /// Hämtar den aktuella temperaturen från den senaste prognosen.
    /// Returnerar null om ingen prognos är tillgänglig.
    /// </summary>
    public double? GetCurrentTemperature(int hours = 0)
    {
        var forecast = CurrentForecast.Skip(hours).FirstOrDefault();
        return forecast?.Temperature;
    }

    /// <summary>
    /// Hämtar den genomsnittliga temperaturen för de närmaste timmarna.
    /// </summary>
    /// <param name="hours">Antal timmar att beräkna medel för (standard: 3)</param>
    public double? GetAverageTemperature(int hours = 3)
    {
        if (CurrentForecast.Count == 0)
            return null;

        var now = DateTime.UtcNow;
        var temperatures = CurrentForecast
            .Where(f => f.Time >= now && f.Time <= now.AddHours(hours))
            .Select(f => f.Temperature)
            .Where(t => t > 0)
            .ToList();

        return temperatures.Count > 0 ? temperatures.Average() : null;
    }
}

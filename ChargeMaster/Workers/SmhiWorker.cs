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
    private const int IntervalMinutes = 60 * 5;

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
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Hämta väderdata vid start
        await FetchWeatherAsync(cancellationToken);

        // Schemalägga timliga hämtningar
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(IntervalMinutes), cancellationToken);
                await FetchWeatherAsync(cancellationToken);
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
    private async Task FetchWeatherAsync(CancellationToken cancellationToken)
    {
        try
        {
            var forecast = await smhiWeatherService.GetForecastAsync(cancellationToken);
            if (forecast.Count > 0)
            {
                CurrentForecast = forecast;
                LastUpdate = DateTime.Now;

                var firstTemp = forecast.FirstOrDefault()?.Temperature ?? 0;
                logger.LogInformation(
                    "SMHI väderdata uppdaterad: {Count} prognoser, nu {Temperature}°C",
                    forecast.Count, firstTemp);

                OnWeatherForecastUpdated(new WeatherForecastUpdatedEventArgs
                {
                    Forecast = forecast,
                    UpdateTime = DateTime.Now
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
            handler.Invoke(this, new WeatherForecastUpdatedEventArgs
            {
                Forecast = CurrentForecast,
                UpdateTime = LastUpdate
            });
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
    public double? GetForecastTemperature(int hours = 0)
    {
        var forecast = CurrentForecast.Skip(hours).FirstOrDefault();
        return forecast?.Temperature;
    }
}

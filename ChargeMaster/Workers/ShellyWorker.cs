namespace ChargeMaster.Workers;

using Services.Shelly;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Bakgrundstjänst som ansluter till MQTT-brokern och lyssnar på Shelly-enheter.
/// </summary>
public class ShellyWorker(
    ShellyMqttService shelly,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ShellyWorker> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await shelly.SetupAsync();
            logger.LogInformation("ShellyMqttService initialiserad");

            // Prenumerera på temperaturändringar
            shelly.TemperatureChanged += OnTemperatureChanged;

            // Vänta på avslutning
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("ShellyWorker avslutad");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel i ShellyWorker");
        }
        finally
        {
            shelly.TemperatureChanged -= OnTemperatureChanged;
            await shelly.DisconnectAsync();
        }
    }

    private void OnTemperatureChanged(object? sender, ShellyTemperatureChangedEventArgs e)
    {
        logger.LogDebug("Temperatur från {DeviceId}: {Temperature} °C",
            e.DeviceId, e.TemperatureCelsius);

        // Spara temperaturen i databasen asynkront
        _ = SaveTemperatureAsync(e);
    }

    private async Task SaveTemperatureAsync(ShellyTemperatureChangedEventArgs e)
    {
        try
        {
            // Använd IServiceScopeFactory för att skapa ett scope för databaskall
            // Detta är nödvändigt eftersom ShellyWorker är Singleton men DbContext är Scoped
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();

            // Hämta senaste värdet för denna enhet från databasen
            var lastTemperature = await dbContext.ShellyTemperatures
                .Where(t => t.DeviceId == e.DeviceId)
                .OrderByDescending(t => t.Timestamp)
                .FirstOrDefaultAsync();

            // Spara bara om:
            // 1. Det finns inget tidigare värde, ELLER
            // 2. Det nya värdet skiljer sig från det senaste värdet
            if (lastTemperature == null || Math.Abs(lastTemperature.TemperatureCelsius - e.TemperatureCelsius) > 0.01)
            {
                var shellyTemp = new ChargeMaster.Data.ShellyTemperature
                {
                    DeviceId = e.DeviceId,
                    TemperatureCelsius = e.TemperatureCelsius,
                    Timestamp = e.Timestamp
                };

                dbContext.ShellyTemperatures.Add(shellyTemp);
                await dbContext.SaveChangesAsync();

                logger.LogDebug("Sparade temperatur för {DeviceId}: {Temperature} °C i databasen",
                    e.DeviceId, e.TemperatureCelsius);
            }
            else
            {
                logger.LogDebug("Temperatur för {DeviceId} oförändrad ({Temperature} °C), sparas inte",
                    e.DeviceId, e.TemperatureCelsius);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid sparning av temperatur för {DeviceId}", e.DeviceId);
        }
    }
}

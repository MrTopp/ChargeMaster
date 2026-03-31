using ChargeMaster.Services.Shelly;

using Microsoft.EntityFrameworkCore;

namespace ChargeMaster.Workers;

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

    private async void OnTemperatureChanged(object? sender, ShellyTemperatureChangedEventArgs e)
    {
        logger.LogDebug("Temperatur från {DeviceId}: {Temperature} °C",
            e.DeviceId, e.TemperatureCelsius);

        await SaveTemperatureAsync(e);
    }

    private async Task SaveTemperatureAsync(ShellyTemperatureChangedEventArgs e)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();

            var lastTemperature = await dbContext.ShellyTemperatures
                .Where(t => t.DeviceId == e.DeviceId)
                .OrderByDescending(t => t.Timestamp)
                .FirstOrDefaultAsync();

            if (lastTemperature == null || Math.Abs(lastTemperature.TemperatureCelsius - e.TemperatureCelsius) > 0.01)
            {
                var shellyTemp = new Data.ShellyTemperature
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

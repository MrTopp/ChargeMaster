namespace ChargeMaster.Workers;

using ChargeMaster.Services.Shelly;

/// <summary>
/// Bakgrundstjänst som ansluter till MQTT-brokern och lyssnar på Shelly-enheter.
/// </summary>
public class ShellyWorker(
    ShellyMqttService shelly,
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
    }
}

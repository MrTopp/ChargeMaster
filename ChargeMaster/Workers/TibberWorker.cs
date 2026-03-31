using ChargeMaster.Services.InfluxDB;
using ChargeMaster.Services.TibberPulse;
using Tibber.Sdk;

namespace ChargeMaster.Workers;

/// <summary>
/// Bakgrundstjänst som prenumererar på realtidsmätdata från Tibber Pulse
/// och skriver mätdata till InfluxDB. Hanterar automatisk återanslutning
/// med exponentiell backoff vid anslutningsfel.
/// </summary>
public class TibberWorker(
    TibberPulseService tibberPulseService,
    InfluxDbService influxDbService,
    ILogger<TibberWorker> logger) : BackgroundService
{
    private const int InitialDelaySeconds = 5;
    private const int MaxDelaySeconds = 300;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int delaySeconds = InitialDelaySeconds;
        int reconnectCount = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("TibberWorker: Ansluter (försök #{Attempt})...", ++reconnectCount);
                tibberPulseService.MeasurementReceived += OnMeasurementReceived;
                await tibberPulseService.SubscribeAsync(stoppingToken);

                // Framgång - återställ backoff
                delaySeconds = InitialDelaySeconds;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("TibberWorker avslutas");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, 
                    "Tibber-anslutning misslyckades. Försöker igen om {DelaySeconds}s (försök #{Attempt})",
                    delaySeconds, reconnectCount);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                delaySeconds = Math.Min(delaySeconds * 2, MaxDelaySeconds);
            }
            finally
            {
                tibberPulseService.MeasurementReceived -= OnMeasurementReceived;
            }
        }

        logger.LogInformation("TibberWorker: Totalt {Reconnects} återanslutningsförsök", reconnectCount);
    }

    private async void OnMeasurementReceived(object? sender, TibberMeasurementEventArgs e)
    {
        try
        {
            var m = e.Measurement;
            logger.LogDebug(
                "Mottog Tibber-mätning: {Power} W, {AccumulatedConsumption} kWh, {AccumulatedCost} {Currency}",
                m.Power, m.AccumulatedConsumption, m.AccumulatedCost, m.Currency);

            await influxDbService.WriteTibberMeasurementAsync(m);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hantering av Tibber-mätning");
        }
    }
}

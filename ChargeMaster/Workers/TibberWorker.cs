using ChargeMaster.Services.InfluxDB;
using ChargeMaster.Services.TibberPulse;

using Tibber.Sdk;

namespace ChargeMaster.Workers;

/// <summary>
/// Bakgrundstjänst som prenumererar på realtidsdata från Tibber Pulse och bearbetar inkommande mätdata.
/// Återansluter automatiskt vid anslutningsfel.
/// </summary>
public class TibberWorker(
    TibberPulseService tibberPulseService,
    InfluxDbService influxDbService,
    ILogger<TibberWorker> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                tibberPulseService.MeasurementReceived += OnMeasurementReceived;
                await tibberPulseService.SubscribeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("TibberWorker avslutas");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fel i TibberWorker, försöker återansluta om 5 minuter...");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            finally
            {
                tibberPulseService.MeasurementReceived -= OnMeasurementReceived;
            }
        }
    }

    private void OnMeasurementReceived(object? sender, TibberMeasurementEventArgs e)
    {
        RealTimeMeasurement m = e.Measurement;

        _ = influxDbService.WriteTibberMeasurementAsync(m);

        //logger.LogInformation(
        //    "Tibber Pulse [{Time}] Effekt: {Power} W | " +
        //    "Timme: {Hour:F4} kWh | Dag: {Day:F4} kWh | " +
        //    "U1: {V1} V | U2: {V2} V | U3: {V3} V | " +
        //    "I1: {A1} A | I2: {A2} A | I3: {A3} A | " +
        //    "Signal: {Signal} dBm",
        //    m.Timestamp.ToLocalTime().ToString("HH:mm:ss"),
        //    m.Power,
        //    m.AccumulatedConsumptionLastHour,
        //    m.AccumulatedConsumption,
        //    m.VoltagePhase1, m.VoltagePhase2, m.VoltagePhase3,
        //    m.CurrentPhase1, m.CurrentPhase2, m.CurrentPhase3,
        //    m.SignalStrength);
    }
}

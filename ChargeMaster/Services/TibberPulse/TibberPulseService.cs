using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Tibber.Sdk;

namespace ChargeMaster.Services.TibberPulse;

/// <summary>
/// Tjänst för att prenumerera på realtidsdata från Tibber Pulse via Tibber SDK.
/// </summary>
public class TibberPulseService(
    IOptions<TibberPulseOptions> options,
    ILogger<TibberPulseService> logger)
{
    private static readonly ProductInfoHeaderValue UserAgent = new("ChargeMaster", "1.0");

    /// <summary>
    /// Senast mottagna mätdata från Tibber Pulse.
    /// </summary>
    public RealTimeMeasurement? LastMeasurement { get; private set; }

    /// <summary>
    /// Event som höjs när ny mätdata tas emot från Tibber Pulse.
    /// </summary>
    public event EventHandler<TibberMeasurementEventArgs>? MeasurementReceived;

    /// <summary>
    /// Ansluter till Tibber API och prenumererar på strömmande mätdata.
    /// Blockerar tills <paramref name="cancellationToken"/> avbryts eller ett fel inträffar.
    /// </summary>
    public async Task SubscribeAsync(CancellationToken cancellationToken)
    {
        var opts = options.Value;

        if (!Guid.TryParse(opts.HomeId, out var homeId))
            throw new InvalidOperationException(
                $"HomeId '{opts.HomeId}' är inte ett giltigt GUID. Kontrollera appsettings.Development.json.");

        // Initiera session-state för denna anslutning
        var sessionState = new SessionState();

        using var client = new TibberApiClient(opts.ApiToken, UserAgent);

        logger.LogInformation("Startar Tibber Pulse-lyssnare för HomeId: {HomeId}", homeId);
        IObservable<RealTimeMeasurement>? listener;
        try
        {
            listener = await client.StartRealTimeMeasurementListener(homeId, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Misslyckades att starta Tibber Pulse-lyssnare");
            throw;
        }

        var streamErrorTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        
        // Använd using för att säkerställa att subscription disposas
        using var subscription = listener.Subscribe(new TibberObserver(this, sessionState, streamErrorTcs, logger));

        logger.LogInformation("Tibber Pulse-prenumeration aktiv");

        // Starta heartbeat monitor
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var monitorTask = MonitorStreamHealthAsync(sessionState, cts.Token);

        try
        {
            // Vänta tills avbruten eller tills strömmen rapporterar ett fel
            var waitTask = Task.Delay(Timeout.Infinite, cancellationToken);
            var completedTask = await Task.WhenAny(waitTask, streamErrorTcs.Task, monitorTask);

            if (completedTask == monitorTask)
            {
                logger.LogWarning("Tibber stream-monitor kastade exception, försöker reconnecta");
                await monitorTask; // Kastar det faktiska undantaget
            }

            // Kasta vidare strömfel så att TibberWorker kan återansluta
            if (completedTask == streamErrorTcs.Task && !cancellationToken.IsCancellationRequested)
            {
                await streamErrorTcs.Task; // Kastar det faktiska undantaget
            }
        }
        finally
        {
            cts.Cancel(); // Stoppa monitor-task
            try
            {
                await client.StopRealTimeMeasurementListener(homeId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Fel vid stopp av Tibber Pulse-lyssnare");
            }
            
            logger.LogInformation(
                "Tibber Pulse-lyssnare stoppad. Mätningar denna session: {Count}",
                sessionState.MeasurementCount);
        }
    }

    /// <summary>
    /// Övervakar stream-hälsa och detekterar om ingen data kommer in under en lång tid.
    /// </summary>
    private async Task MonitorStreamHealthAsync(SessionState sessionState, CancellationToken cancellationToken)
    {
        const int healthCheckIntervalSeconds = 30;
        const int maxSilenceDurationSeconds = 300; // 5 minuter

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(healthCheckIntervalSeconds), cancellationToken);

                var now = DateTime.UtcNow;
                var timeSinceLastMeasurement = now - sessionState.LastMeasurementTime;

                // Logga debug-info
                logger.LogDebug(
                    "Tibber stream health: {Count} mätningar, senast för {Seconds:F1}s sedan",
                    sessionState.MeasurementCount,
                    timeSinceLastMeasurement.TotalSeconds);

                if (timeSinceLastMeasurement.TotalSeconds > maxSilenceDurationSeconds)
                {
                    logger.LogWarning(
                        "Tibber stream är tyst i {Duration:F0} sekunder. Förlorar anslutning",
                        timeSinceLastMeasurement.TotalSeconds);

                    throw new InvalidOperationException(
                        $"Tibber stream timed out: no data received for {timeSinceLastMeasurement.TotalSeconds:F0} seconds");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Förväntat när prenumerationen avslutas
        }
    }

    private void HandleMeasurement(RealTimeMeasurement measurement, SessionState sessionState)
    {
        LastMeasurement = measurement;
        sessionState.LastMeasurementTime = DateTime.UtcNow;
        sessionState.MeasurementCount++;

        // Logg bara var 100:e mätning för debug
        if (sessionState.MeasurementCount % 100 == 0)
        {
            logger.LogDebug(
                "Tibber Pulse: {Count} mätningar denna session, senast: {Power}W",
                sessionState.MeasurementCount,
                measurement.Power);
        }

        MeasurementReceived?.Invoke(this, new TibberMeasurementEventArgs(measurement));
    }

    private void HandleStreamError(Exception error) =>
        logger.LogError(error, "Fel i Tibber Pulse-ström");

    private void HandleStreamCompleted() =>
        logger.LogWarning("Tibber Pulse-ström avslutad av servern");

    /// <summary>
    /// State för en session (mellan reconnects).
    /// </summary>
    private class SessionState
    {
        public DateTime LastMeasurementTime { get; set; } = DateTime.UtcNow;
        public int MeasurementCount { get; set; }
    }

    private sealed class TibberObserver(
        TibberPulseService service,
        SessionState sessionState,
        TaskCompletionSource<bool> errorTcs,
        ILogger<TibberPulseService> logger) : IObserver<RealTimeMeasurement>
    {
        /// <summary>
        /// Anropas när en ny realtidsmätning tas emot från Tibber-strömmen.
        /// </summary>
        /// <param name="value">Den inkommande mätningen.</param>
        public void OnNext(RealTimeMeasurement value)
        {
            try
            {
                service.HandleMeasurement(value, sessionState);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fel vid hantering av Tibber-mätning");
            }
        }

        /// <summary>
        /// Anropas när ett fel uppstår i Tibber-strömmen. Propagerar felet till den väntande uppgiften.
        /// </summary>
        /// <param name="error">Undantaget som orsakade felet.</param>
        public void OnError(Exception error)
        {
            service.HandleStreamError(error);
            errorTcs.TrySetException(error);
        }

        /// <summary>
        /// Anropas när servern stänger Tibber-strömmen. Behandlas som ett fel eftersom strömmen förväntas vara kontinuerlig.
        /// </summary>
        public void OnCompleted()
        {
            service.HandleStreamCompleted();
            errorTcs.TrySetException(
                new InvalidOperationException("Tibber stream was closed by the server"));
        }
    }
}
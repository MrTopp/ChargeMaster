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
                $"HomeId '{opts.HomeId}' är inte ett giltigt GUID. Kontrollera appsettings.Developer.json.");

        var client = new TibberApiClient(opts.ApiToken, UserAgent);

        logger.LogInformation("Startar Tibber Pulse-lyssnare för HomeId: {HomeId}", homeId);
        IObservable<RealTimeMeasurement>? listener;
        try
        {
            listener = await client.StartRealTimeMeasurementListener(homeId, cancellationToken: cancellationToken);
        } catch (Exception ex)
        {
            logger.LogError(ex, "Misslyckades att starta Tibber Pulse-lyssnare");
            throw;
        }

        var streamErrorTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = listener.Subscribe(new TibberObserver(this, streamErrorTcs));

        logger.LogInformation("Tibber Pulse-prenumeration aktiv");

        // Vänta tills avbruten eller tills strömmen rapporterar ett fel
        var waitTask = Task.Delay(Timeout.Infinite, cancellationToken);
        var completedTask = await Task.WhenAny(waitTask, streamErrorTcs.Task);

        await client.StopRealTimeMeasurementListener(homeId);
        logger.LogInformation("Tibber Pulse-lyssnare stoppad");

        // Kasta vidare strömfel så att TibberWorker kan återansluta
        if (completedTask == streamErrorTcs.Task)
            await streamErrorTcs.Task;
    }

    private void HandleMeasurement(RealTimeMeasurement measurement)
    {
        LastMeasurement = measurement;
        MeasurementReceived?.Invoke(this, new TibberMeasurementEventArgs(measurement));
    }

    private void HandleStreamError(Exception error) =>
        logger.LogError(error, "Fel i Tibber Pulse-ström");

    private void HandleStreamCompleted() =>
        logger.LogInformation("Tibber Pulse-ström avslutad av servern");

    private sealed class TibberObserver(
        TibberPulseService service,
        TaskCompletionSource<bool> errorTcs) : IObserver<RealTimeMeasurement>
    {
        public void OnNext(RealTimeMeasurement value) => service.HandleMeasurement(value);

        public void OnError(Exception error)
        {
            service.HandleStreamError(error);
            errorTcs.TrySetException(error);
        }

        public void OnCompleted() => service.HandleStreamCompleted();
    }
}

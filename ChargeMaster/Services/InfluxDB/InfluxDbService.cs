using ChargeMaster.Services.ElectricityPrice;
using ChargeMaster.Services.TibberPulse;
using ChargeMaster.Services.VolksWagen;
using ChargeMaster.Services.Wallbox;

using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Api.Service;
using InfluxDB.Client.Writes;

using Microsoft.Extensions.Options;
using Tibber.Sdk;

using System.Threading.Channels;

namespace ChargeMaster.Services.InfluxDB;

/// <summary>
/// Interface for creating InfluxDBClient instances.
/// Allows for dependency injection and testing without creating actual InfluxDB connections.
/// </summary>
public interface IInfluxDBClientFactory
{
    /// <summary>
    /// Creates an InfluxDBClient with the provided options.
    /// </summary>
    /// <param name="options">InfluxDB configuration options</param>
    /// <returns>A configured InfluxDBClient instance</returns>
    InfluxDBClient CreateClient(InfluxDBOptions options);
}

/// <summary>
/// Default implementation of IInfluxDBClientFactory that creates real InfluxDBClient instances.
/// </summary>
public class InfluxDBClientFactory : IInfluxDBClientFactory
{
    public InfluxDBClient CreateClient(InfluxDBOptions options)
    {
        var httpClient = new HttpClient(new SocketsHttpHandler())
        {
            Timeout = TimeSpan.FromSeconds(10) // Set timeout to 10 seconds for HTTP operations
        };

        var clientOptions = new InfluxDBClientOptions(options.Url)
        {
            Token = options.Token,
            Org = options.Org,
            Bucket = options.Bucket,
            HttpClient = httpClient
        };
        return new InfluxDBClient(clientOptions);
    }
}

/// <summary>
/// Tjänst för att skriva data till InfluxDB.
/// Serialiserar alla skrivningar via en queue för att undvika concurrency-problem.
/// </summary>
public class InfluxDbService : IAsyncDisposable
{
    private readonly InfluxDBClient _client;
    private readonly InfluxDBOptions _options;
    private readonly ILogger<InfluxDbService> _logger;
    private readonly ElectricityPriceService _priceService;
    private readonly Channel<WriteOperation> _writeChannel;
    private readonly CancellationTokenSource _cancellationTokenSource;

    private long _lastPhase1Energy = 0;
    private long _lastPhase2Energy = 0;
    private long _lastPhase3Energy = 0;
    private decimal _lastPrice = 0;
    private int _lastQuarter = -1;

    public InfluxDbService(IOptions<InfluxDBOptions> options, ElectricityPriceService priceService, ILogger<InfluxDbService> logger)
        : this(options, priceService, logger, new InfluxDBClientFactory())
    {
    }

    public InfluxDbService(IOptions<InfluxDBOptions> options, ElectricityPriceService priceService, ILogger<InfluxDbService> logger, IInfluxDBClientFactory clientFactory)
    {
        _options = options.Value;
        _logger = logger;
        _priceService = priceService;
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            ValidateUrl(_options.Url);
            _client = clientFactory.CreateClient(_options);
            _logger.LogInformation("InfluxDbService initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize InfluxDbService");
            throw;
        }

        // Skapa channel för att serialisera skrivningar
        _writeChannel = Channel.CreateUnbounded<WriteOperation>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        // Starta background task som processerar skrivningar
        _ = ProcessWriteQueueAsync(_cancellationTokenSource.Token);
    }

    /// <summary>
    /// Background task som processar skrivningar från kanalen en åt gången.
    /// </summary>
    private async Task ProcessWriteQueueAsync(CancellationToken cancellationToken)
    {
        var operationCount = 0;
        var errorCount = 0;

        try
        {
            _logger.LogDebug("InfluxDB write queue processor started");

            await foreach (var operation in _writeChannel.Reader.ReadAllAsync(cancellationToken))
            {
                operationCount++;
                try
                {
                    var operationType = operation.GetType().Name;
                    _logger.LogDebug("Processing InfluxDB write operation #{Count}: {OperationType}", operationCount, operationType);

                    await operation.ExecuteAsync(_client, _options, _logger);

                    _logger.LogDebug("Successfully completed write operation #{Count}: {OperationType}", operationCount, operationType);
                }
                catch (TaskCanceledException ex)
                {
                    errorCount++;
                    _logger.LogWarning(ex, 
                        "Write operation #{Count} was canceled (timeout). Queue may be slow or InfluxDB unresponsive",
                        operationCount);
                }
                catch (HttpRequestException ex)
                {
                    errorCount++;
                    _logger.LogError(ex,
                        "Write operation #{Count} failed with HTTP error. InfluxDB connection issue? Status: {Status}",
                        operationCount, ex.StatusCode);
                }
                catch (InvalidOperationException ex)
                {
                    errorCount++;
                    _logger.LogError(ex,
                        "Write operation #{Count} failed with invalid operation. Data validation error?",
                        operationCount);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogError(ex,
                        "Write operation #{Count} failed with unexpected error. Type: {ExceptionType}, Message: {Message}",
                        operationCount, ex.GetType().Name, ex.Message);
                }
            }

            _logger.LogInformation(
                "InfluxDB write queue processor completed. Total operations: {Total}, Errors: {Errors}, Success rate: {SuccessRate:P}",
                operationCount, errorCount, (operationCount - errorCount) / (double)Math.Max(operationCount, 1));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "InfluxDB write queue processor canceled. Processed {Count} operations with {Errors} errors before shutdown",
                operationCount, errorCount);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "InfluxDB write queue processor crashed unexpectedly. Processed {Count} operations with {Errors} errors",
                operationCount, errorCount);
        }
    }

    /// <summary>
    /// Skriver innehållet i en WallboxMeterInfo-instans till InfluxDB.
    /// </summary>
    /// <param name="meterInfo">Mätdata från Wallbox som ska skrivas</param>
    /// <returns>Task som representerar den asynkrona operationen</returns>
    public async Task WriteWallboxMeterInfoAsync(WallboxMeterInfo meterInfo)
    {
        var operation = new WallboxWriteOperation(meterInfo, _priceService, this);
        await _writeChannel.Writer.WriteAsync(operation);
    }

    /// <summary>
    /// Skriver mätdata från Tibber Pulse till InfluxDB.
    /// </summary>
    /// <param name="e">Mätdata från Tibber Pulse</param>
    public async Task WriteTibberMeasurementAsync(RealTimeMeasurement m)
    {
        var operation = new TibberWriteOperation(m, _priceService);
        await _writeChannel.Writer.WriteAsync(operation);
    }

    // ==================== WRITE OPERATIONS ====================

    private abstract record WriteOperation
    {
        public abstract Task ExecuteAsync(InfluxDBClient client, InfluxDBOptions options, ILogger<InfluxDbService> logger);
    }

    private sealed record WallboxWriteOperation(WallboxMeterInfo MeterInfo, ElectricityPriceService PriceService, InfluxDbService Service) : WriteOperation
    {
        private static List<PointData> _points = new(); 

        public override async Task ExecuteAsync(InfluxDBClient client, InfluxDBOptions options, ILogger<InfluxDbService> logger)
        {
            var timestamp = DateTime.UtcNow;
            int currentQuarter = timestamp.Minute / 15;

            if (currentQuarter != Service._lastQuarter)
            {
                timestamp = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day,
                    timestamp.Hour, timestamp.Minute, 0, DateTimeKind.Utc);
            }

            var elpris = await PriceService.GetPriceForDateTimeAsync(DateTime.Now);

            if (currentQuarter == Service._lastQuarter &&
                MeterInfo.Phase1CurrentEnergy == Service._lastPhase1Energy &&
                MeterInfo.Phase2CurrentEnergy == Service._lastPhase2Energy &&
                MeterInfo.Phase3CurrentEnergy == Service._lastPhase3Energy &&
                elpris?.SekPerKwh == Service._lastPrice)
            {
                return; // Ingen förändring
            }

            var point = PointData.Measurement("wallbox_meter")
                .Field("acc_energy_wh", MeterInfo.AccEnergy)
                .Field("phase1_current_energy_w", MeterInfo.Phase1CurrentEnergy)
                .Field("phase2_current_energy_w", MeterInfo.Phase2CurrentEnergy)
                .Field("phase3_current_energy_w", MeterInfo.Phase3CurrentEnergy)
                .Field("current_energy_w", MeterInfo.CurrentEnergy)
                .Field("sek_per_kwh", elpris?.SekPerKwh ?? 0)
                .Timestamp(timestamp, WritePrecision.Ns);

            //await client.GetWriteApiAsync().WritePointsAsync(points, options.Bucket, options.Org);
            logger.LogDebug(">> Queuing WallboxMeterInfo for InfluxDB write: {current}W", MeterInfo.CurrentEnergy);
            _points.Add(point);
            WritePoints(client, options, logger);

            logger.LogDebug(">> Writing WallboxMeterInfo to InfluxDB: {current}", MeterInfo.CurrentEnergy);

            Service._lastPhase1Energy = MeterInfo.Phase1CurrentEnergy;
            Service._lastPhase2Energy = MeterInfo.Phase2CurrentEnergy;
            Service._lastPhase3Energy = MeterInfo.Phase3CurrentEnergy;
            Service._lastPrice = elpris?.SekPerKwh ?? 0;
            Service._lastQuarter = currentQuarter;
        }

        private void WritePoints(InfluxDBClient client, InfluxDBOptions options, ILogger<InfluxDbService> logger, bool forceWrite = false)
        {
            if (_points.Count > 10 || forceWrite)
            {
                client.GetWriteApiAsync().WritePointsAsync(_points, options.Bucket, options.Org).Wait();
                logger.LogDebug(">> Successfully wrote {Count} points to InfluxDB for WallboxMeterInfo", _points.Count);
                _points.Clear();
            }
            else
            {
                logger.LogDebug(">> Accumulated {Count} points for WallboxMeterInfo, waiting to batch write to InfluxDB", _points.Count);
            }
        }
    }

    private sealed record TibberWriteOperation(RealTimeMeasurement Measurement, ElectricityPriceService PriceService) : WriteOperation
    {
        private static List<PointData> _points = new ();

        public override async Task ExecuteAsync(InfluxDBClient client, InfluxDBOptions options, ILogger<InfluxDbService> logger)
        {
            var timestamp = Measurement.Timestamp.UtcDateTime;
            var elpris = await PriceService.GetPriceForDateTimeAsync(DateTime.Now);

            var point = PointData.Measurement("tibber_pulse")
                .Field("power_w", (double)Measurement.Power)
                .Field("accumulated_consumption_last_hour_kwh", (double)Measurement.AccumulatedConsumptionLastHour)
                .Field("sek_per_kwh", elpris?.SekPerKwh ?? 0)
                .Timestamp(timestamp, WritePrecision.Ns);

            if (Measurement.PowerFactor.HasValue)
                point = point.Field("power_factor", (double)Measurement.PowerFactor.Value);
            if (Measurement.AccumulatedCost.HasValue)
                point = point.Field("accumulated_cost", (double)Measurement.AccumulatedCost.Value);
            if (Measurement.SignalStrength.HasValue)
                point = point.Field("signal_strength_db", Measurement.SignalStrength.Value);

            if (Measurement.VoltagePhase1.HasValue && Measurement.CurrentPhase1.HasValue && Measurement.PowerFactor.HasValue)
                point = point.Field("power_phase1_w", (double)(Measurement.VoltagePhase1.Value * Measurement.CurrentPhase1.Value * Measurement.PowerFactor.Value));
            if (Measurement.VoltagePhase2.HasValue && Measurement.CurrentPhase2.HasValue && Measurement.PowerFactor.HasValue)
                point = point.Field("power_phase2_w", (double)(Measurement.VoltagePhase2.Value * Measurement.CurrentPhase2.Value * Measurement.PowerFactor.Value));
            if (Measurement.VoltagePhase3.HasValue && Measurement.CurrentPhase3.HasValue && Measurement.PowerFactor.HasValue)
                point = point.Field("power_phase3_w", (double)(Measurement.VoltagePhase3.Value * Measurement.CurrentPhase3.Value * Measurement.PowerFactor.Value));

            logger.LogDebug(">> Queuing Tibber measurement for InfluxDB write: {Power}W", Measurement.Power);
            //await client.GetWriteApiAsync().WritePointAsync(point, options.Bucket, options.Org);
            _points.Add(point);
            WritePoints(client, options, logger);
            logger.LogDebug(">> Writing Tibber measurement to InfluxDB: {Power}W", Measurement.Power);
        }

        private void WritePoints(InfluxDBClient client, InfluxDBOptions options, ILogger<InfluxDbService> logger, bool forceWrite = false)
        {
            if (_points.Count > 10 || forceWrite)
            {
                client.GetWriteApiAsync().WritePointsAsync(_points, options.Bucket, options.Org).Wait();
                logger.LogDebug(">> Successfully wrote {Count} points to InfluxDB for Tibber measurement", _points.Count);
                _points.Clear();
            }
            else
            {
                logger.LogDebug(">> Accumulated {Count} points for Tibber measurement, waiting to batch write to InfluxDB", _points.Count);
            }
        }
    }

    // ==================== HELPER METHODS ====================

    private static void ValidateUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be null or empty", nameof(url));

        try
        {
            var uri = new Uri(url, UriKind.Absolute);
            if (uri.Scheme != "http" && uri.Scheme != "https")
                throw new ArgumentException($"URL scheme must be http or https, but got: {uri.Scheme}", nameof(url));
        }
        catch (UriFormatException ex)
        {
            throw new ArgumentException($"Invalid URL format: {url}", nameof(url), ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _writeChannel.Writer.Complete();
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}

/// <summary>
/// Konfigurationsalternativ för InfluxDB.
/// </summary>
public class InfluxDBOptions
{
    public string Url { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Org { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
}

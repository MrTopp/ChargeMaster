using ChargeMaster.Services.ElectricityPrice;
using ChargeMaster.Services.Wallbox;

using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Core.Exceptions;
using InfluxDB.Client.Writes;

using Microsoft.Extensions.Options;
using Tibber.Sdk;

using System.Threading.Channels;
// ReSharper disable InconsistentNaming

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
            Timeout = TimeSpan.FromSeconds(60)
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
/// Writes measurement data to InfluxDB.
/// Serializes all writes via a channel to avoid concurrency issues and batches points for efficiency.
/// </summary>
public class InfluxDbService : IAsyncDisposable
{
    private readonly InfluxDBClient _client;
    private readonly InfluxDBOptions _options;
    private readonly ILogger<InfluxDbService> _logger;
    private readonly ElectricityPriceService _priceService;
    private readonly Channel<WriteItem> _writeChannel;
    private readonly Task _processTask;

    private readonly List<PointData> _wallboxPoints = new();
    private readonly List<PointData> _tibberPoints = new();
    private bool _disposed;

    private long _lastPhase1Energy;
    private long _lastPhase2Energy;
    private long _lastPhase3Energy;
    private decimal _lastPrice;
    private int _lastQuarter = -1;

    private const int BatchSize = 10;

    public InfluxDbService(IOptions<InfluxDBOptions> options, ElectricityPriceService priceService, ILogger<InfluxDbService> logger)
        : this(options, priceService, logger, new InfluxDBClientFactory())
    {
    }

    public InfluxDbService(IOptions<InfluxDBOptions> options, ElectricityPriceService priceService, ILogger<InfluxDbService> logger, IInfluxDBClientFactory clientFactory)
    {
        _options = options.Value;
        _logger = logger;
        _priceService = priceService;

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

        _writeChannel = Channel.CreateUnbounded<WriteItem>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        _processTask = ProcessWriteQueueAsync();
    }

    /// <summary>
    /// Skriver wallbox-mätdata till InfluxDB via den interna skrivkön.
    /// </summary>
    public async Task WriteWallboxMeterInfoAsync(WallboxMeterInfo meterInfo)
    {
        await _writeChannel.Writer.WriteAsync(new WallboxItem(meterInfo));
    }

    /// <summary>
    /// Skriver Tibber Pulse-mätdata till InfluxDB via den interna skrivkön.
    /// </summary>
    public async Task WriteTibberMeasurementAsync(RealTimeMeasurement m)
    {
        await _writeChannel.Writer.WriteAsync(new TibberItem(m));
    }

    private async Task ProcessWriteQueueAsync()
    {
        try
        {
            _logger.LogDebug("InfluxDB write queue processor started");

            await foreach (var item in _writeChannel.Reader.ReadAllAsync())
            {
                try
                {
                    switch (item)
                    {
                        case WallboxItem(var meterInfo):
                            await ProcessWallboxAsync(meterInfo);
                            break;
                        case TibberItem(var measurement):
                            await ProcessTibberAsync(measurement);
                            break;
                        case FlushItem:
                            await FlushPointsAsync(_wallboxPoints, "wallbox");
                            await FlushPointsAsync(_tibberPoints, "tibber");
                            break;
                    }
                }
                catch (TaskCanceledException ex)
                {
                    _logger.LogWarning(ex, "Write operation timed out");
                }
                catch (HttpException ex) when (ex.InnerException is TaskCanceledException or OperationCanceledException)
                {
                    // Transiellt nätverksavbrott (t.ex. InfluxDB omstartat eller anslutning bruten)
                    _logger.LogWarning("Transient network error when writing to InfluxDB, buffered data will be retried: {Message}", ex.Message);
                }
                catch (HttpException ex)
                {
                    _logger.LogError(ex, "Write operation failed with InfluxDB HTTP error {Status}", ex.Status);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Write operation failed with HTTP error. Status: {Status}", ex.StatusCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Write operation failed: {Type}", ex.GetType().Name);
                }
            }

            _logger.LogInformation("InfluxDB write queue processor completed");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("InfluxDB write queue processor canceled");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "InfluxDB write queue processor crashed unexpectedly");
        }
    }

    private async Task ProcessWallboxAsync(WallboxMeterInfo meterInfo)
    {
        var timestamp = DateTime.UtcNow;
        int currentQuarter = timestamp.Hour * 4 + timestamp.Minute / 15;

        if (currentQuarter != _lastQuarter)
        {
            timestamp = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day,
                timestamp.Hour, timestamp.Minute, 0, DateTimeKind.Utc);
        }

        Data.ElectricityPrice? elpris = null;
        try
        {
            elpris = await _priceService.GetPriceForDateTimeAsync(DateTime.Now);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kunde inte hämta elpris för wallbox-mätning");
        }

        if (currentQuarter == _lastQuarter &&
            meterInfo.Phase1CurrentEnergy == _lastPhase1Energy &&
            meterInfo.Phase2CurrentEnergy == _lastPhase2Energy &&
            meterInfo.Phase3CurrentEnergy == _lastPhase3Energy &&
            elpris?.SekPerKwh == _lastPrice)
        {
            return;
        }

        var point = PointData.Measurement("wallbox_meter")
            .Field("acc_energy_wh", meterInfo.AccEnergy)
            .Field("phase1_current_energy_w", meterInfo.Phase1CurrentEnergy)
            .Field("phase2_current_energy_w", meterInfo.Phase2CurrentEnergy)
            .Field("phase3_current_energy_w", meterInfo.Phase3CurrentEnergy)
            .Field("current_energy_w", meterInfo.CurrentEnergy)
            .Field("sek_per_kwh", elpris?.SekPerKwh ?? 0)
            .Timestamp(timestamp, WritePrecision.Ns);

        _wallboxPoints.Add(point);
        if (_wallboxPoints.Count >= BatchSize)
            await FlushPointsAsync(_wallboxPoints, "wallbox");

        _lastPhase1Energy = meterInfo.Phase1CurrentEnergy;
        _lastPhase2Energy = meterInfo.Phase2CurrentEnergy;
        _lastPhase3Energy = meterInfo.Phase3CurrentEnergy;
        _lastPrice = elpris?.SekPerKwh ?? 0;
        _lastQuarter = currentQuarter;
    }

    private async Task ProcessTibberAsync(RealTimeMeasurement measurement)
    {
        var timestamp = measurement.Timestamp.UtcDateTime;

        Data.ElectricityPrice? elpris = null;
        try
        {
            elpris = await _priceService.GetPriceForDateTimeAsync(DateTime.Now);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kunde inte hämta elpris för Tibber-mätning");
        }

        var point = PointData.Measurement("tibber_pulse")
            .Field("power_w", (double)measurement.Power)
            .Field("accumulated_consumption_last_hour_kwh", (double)measurement.AccumulatedConsumptionLastHour)
            .Field("sek_per_kwh", elpris?.SekPerKwh ?? 0)
            .Timestamp(timestamp, WritePrecision.Ns);

        if (measurement.PowerFactor.HasValue)
            point = point.Field("power_factor", (double)measurement.PowerFactor.Value);
        if (measurement.AccumulatedCost.HasValue)
            point = point.Field("accumulated_cost", (double)measurement.AccumulatedCost.Value);
        if (measurement.SignalStrength.HasValue)
            point = point.Field("signal_strength_db", measurement.SignalStrength.Value);

        if (measurement.VoltagePhase1.HasValue && measurement.CurrentPhase1.HasValue && measurement.PowerFactor.HasValue)
            point = point.Field("power_phase1_w", (double)(measurement.VoltagePhase1.Value * measurement.CurrentPhase1.Value * measurement.PowerFactor.Value));
        if (measurement.VoltagePhase2.HasValue && measurement.CurrentPhase2.HasValue && measurement.PowerFactor.HasValue)
            point = point.Field("power_phase2_w", (double)(measurement.VoltagePhase2.Value * measurement.CurrentPhase2.Value * measurement.PowerFactor.Value));
        if (measurement.VoltagePhase3.HasValue && measurement.CurrentPhase3.HasValue && measurement.PowerFactor.HasValue)
            point = point.Field("power_phase3_w", (double)(measurement.VoltagePhase3.Value * measurement.CurrentPhase3.Value * measurement.PowerFactor.Value));

        _tibberPoints.Add(point);
        if (_tibberPoints.Count >= BatchSize)
            await FlushPointsAsync(_tibberPoints, "tibber");
    }

    private async Task FlushPointsAsync(List<PointData> points, string source)
    {
        if (points.Count == 0)
            return;

        await _client.GetWriteApiAsync().WritePointsAsync(points, _options.Bucket, _options.Org);
        _logger.LogDebug("Wrote {Count} {Source} points to InfluxDB", points.Count, source);
        points.Clear();
    }

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

    /// <summary>
    /// Tömmer kvarvarande mätpunkter och frigör InfluxDB-klienten.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            _writeChannel.Writer.TryWrite(new FlushItem());
            _writeChannel.Writer.TryComplete();
            await _processTask;
        }
        finally
        {
            _client?.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private abstract record WriteItem;
    private sealed record WallboxItem(WallboxMeterInfo MeterInfo) : WriteItem;
    private sealed record TibberItem(RealTimeMeasurement Measurement) : WriteItem;
    private sealed record FlushItem : WriteItem;
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

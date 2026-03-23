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
        var clientOptions = new InfluxDBClientOptions(options.Url)
        {
            Token = options.Token,
            Org = options.Org,
            Bucket = options.Bucket
        };
        return new InfluxDBClient(clientOptions);
    }
}

/// <summary>
/// Tjänst för att skriva data till InfluxDB.
/// </summary>
public class InfluxDbService
{
    private readonly InfluxDBClient _client;
    private readonly InfluxDBOptions _options;
    private readonly ILogger<InfluxDbService> _logger;
    private readonly ElectricityPriceService _priceService;

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

            // InfluxDB 2.0 autentisering med token
            _client = clientFactory.CreateClient(_options);
            _logger.LogInformation("InfluxDbService initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize InfluxDbService");
            throw;
        }
    }

    private static void ValidateUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL cannot be null or empty", nameof(url));
        }

        try
        {
            var uri = new Uri(url, UriKind.Absolute);

            if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                throw new ArgumentException($"URL scheme must be http or https, but got: {uri.Scheme}", nameof(url));
            }
        }
        catch (UriFormatException ex)
        {
            throw new ArgumentException($"Invalid URL format: {url}", nameof(url), ex);
        }
    }

    public static InfluxDbService CreateInstance(IOptions<InfluxDBOptions> options, 
        ElectricityPriceService priceService, ILogger<InfluxDbService> logger)
    {
        return new InfluxDbService(options, priceService, logger);
    }

    public static InfluxDbService CreateInstance(IOptions<InfluxDBOptions> options, 
        ElectricityPriceService priceService, ILogger<InfluxDbService> logger, IInfluxDBClientFactory clientFactory)
    {
        return new InfluxDbService(options, priceService, logger, clientFactory);
    }

    private long _lastPhase1Energy = 0;
    private long _lastPhase2Energy = 0;
    private long _lastPhase3Energy = 0;
    private decimal _lastPrice = 0;
    private int _lastQuarter = -1;

    /// <summary>
    /// Skriver innehållet i en WallboxMeterInfo-instans till InfluxDB.
    /// </summary>
    /// <param name="meterInfo">Mätdata från Wallbox som ska skrivas</param>
    /// <returns>Task som representerar den asynkrona operationen</returns>
    public async Task WriteWallboxMeterInfoAsync(WallboxMeterInfo meterInfo)
    {
        try
        {
            var timestamp = DateTime.UtcNow;
            int currentQuarter = timestamp.Minute / 15;

            if (currentQuarter != _lastQuarter)
            {
                // Första läsningen i kvarten, nolla sekunderna så det blir exakt, t.ex. 12:00:00, 12:15:00, 12:30:00 eller 12:45:00
                timestamp = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day,
                    timestamp.Hour, timestamp.Minute, 0, DateTimeKind.Utc);
            }

            var elpris = await _priceService.GetPriceForDateTimeAsync(DateTime.Now);

            if (currentQuarter == _lastQuarter &&
                meterInfo.Phase1CurrentEnergy == _lastPhase1Energy &&
                meterInfo.Phase2CurrentEnergy == _lastPhase2Energy &&
                meterInfo.Phase3CurrentEnergy == _lastPhase3Energy &&
                elpris?.SekPerKwh == _lastPrice)
            {
                return; // Ingen förändring, hoppa över skrivning
            }


            var points = new List<PointData>
            {
                PointData.Measurement("wallbox_meter")
                    //.Tag("meter_serial", meterInfo.MeterSerial ?? "unknown")
                    .Field("acc_energy_wh", meterInfo.AccEnergy)
                    .Field("phase1_current_energy_w", meterInfo.Phase1CurrentEnergy)
                    .Field("phase2_current_energy_w", meterInfo.Phase2CurrentEnergy)
                    .Field("phase3_current_energy_w", meterInfo.Phase3CurrentEnergy)
                    .Field("current_energy_w", meterInfo.CurrentEnergy)
                    .Field("sek_per_kwh", elpris?.SekPerKwh ?? 0)
                    .Timestamp(timestamp, WritePrecision.Ns)
            };
            await _client.GetWriteApiAsync().WritePointsAsync(points, _options.Bucket, _options.Org);
            _logger.LogDebug(">> Writing WallboxMeterInfo to InfluxDB: {current}", meterInfo.CurrentEnergy);

            _lastPhase1Energy = meterInfo.Phase1CurrentEnergy;
            _lastPhase2Energy = meterInfo.Phase2CurrentEnergy;
            _lastPhase3Energy = meterInfo.Phase3CurrentEnergy;
            _lastPrice = elpris?.SekPerKwh ?? 0;
            _lastQuarter = currentQuarter;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write WallboxMeterInfo to InfluxDB");
        }
    }

    /// <summary>
    /// Skriver mätdata från Tibber Pulse till InfluxDB.
    /// </summary>
    /// <param name="e">Mätdata från Tibber Pulse</param>
    public async Task WriteTibberMeasurementAsync(RealTimeMeasurement m)
    {
        try
        {
            var timestamp = m.Timestamp.UtcDateTime;

            var elpris = await _priceService.GetPriceForDateTimeAsync(DateTime.Now);

            var point = PointData.Measurement("tibber_pulse")
                .Field("power_w", (double)m.Power)
                //.Field("accumulated_consumption_kwh", (double)m.AccumulatedConsumption)
                .Field("accumulated_consumption_last_hour_kwh", (double)m.AccumulatedConsumptionLastHour)
                //.Field("accumulated_production_kwh", (double)m.AccumulatedProduction)
                //.Field("accumulated_production_last_hour_kwh", (double)m.AccumulatedProductionLastHour)
                //.Field("min_power_w", (double)m.MinPower)
                //.Field("avg_power_w", (double)m.AveragePower)
                //.Field("max_power_w", (double)m.MaxPower)
                .Field("sek_per_kwh", elpris?.SekPerKwh ?? 0)
                .Timestamp(timestamp, WritePrecision.Ns);

            //if (m.PowerProduction.HasValue)
            //    point = point.Field("power_production_w", (double)m.PowerProduction.Value);
            //if (m.VoltagePhase1.HasValue)
            //    point = point.Field("voltage_phase1_v", (double)m.VoltagePhase1.Value);
            //if (m.VoltagePhase2.HasValue)
            //    point = point.Field("voltage_phase2_v", (double)m.VoltagePhase2.Value);
            //if (m.VoltagePhase3.HasValue)
            //    point = point.Field("voltage_phase3_v", (double)m.VoltagePhase3.Value);
            //if (m.CurrentPhase1.HasValue)
            //    point = point.Field("current_phase1_a", (double)m.CurrentPhase1.Value);
            //if (m.CurrentPhase2.HasValue)
            //    point = point.Field("current_phase2_a", (double)m.CurrentPhase2.Value);
            //if (m.CurrentPhase3.HasValue)
            //    point = point.Field("current_phase3_a", (double)m.CurrentPhase3.Value);
            if (m.PowerFactor.HasValue)
                point = point.Field("power_factor", (double)m.PowerFactor.Value);
            if (m.AccumulatedCost.HasValue)
                point = point.Field("accumulated_cost", (double)m.AccumulatedCost.Value);
            if (m.SignalStrength.HasValue)
                point = point.Field("signal_strength_db", m.SignalStrength.Value);

            // Aktiv effekt per fas: P = U × I × PF
            if (m.VoltagePhase1.HasValue && m.CurrentPhase1.HasValue && m.PowerFactor.HasValue)
                point = point.Field("power_phase1_w", (double)(m.VoltagePhase1.Value * m.CurrentPhase1.Value * m.PowerFactor.Value));
            if (m.VoltagePhase2.HasValue && m.CurrentPhase2.HasValue && m.PowerFactor.HasValue)
                point = point.Field("power_phase2_w", (double)(m.VoltagePhase2.Value * m.CurrentPhase2.Value * m.PowerFactor.Value));
            if (m.VoltagePhase3.HasValue && m.CurrentPhase3.HasValue && m.PowerFactor.HasValue)
                point = point.Field("power_phase3_w", (double)(m.VoltagePhase3.Value * m.CurrentPhase3.Value * m.PowerFactor.Value));


            await _client.GetWriteApiAsync().WritePointAsync(point, _options.Bucket, _options.Org);
            _logger.LogDebug(">> Writing Tibber measurement to InfluxDB: {Power}W", m.Power);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write Tibber measurement to InfluxDB");
        }
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

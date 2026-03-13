using ChargeMaster.Services.ElectricityPrice;
using ChargeMaster.Services.VolksWagen;
using ChargeMaster.Services.Wallbox;

using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Api.Service;
using InfluxDB.Client.Writes;

using Microsoft.Extensions.Options;

namespace ChargeMaster.Services.InfluxDB;

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
    {
        _options = options.Value;
        _logger = logger;
        _priceService = priceService;

        try
        {
            // InfluxDB 2.0 autentisering med token
            var clientOptions = new InfluxDBClientOptions(_options.Url)
            {
                Token = _options.Token,
                Org = _options.Org,
                Bucket = _options.Bucket
            };
            _client = new InfluxDBClient(clientOptions);
            _logger.LogInformation("InfluxDbService initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize InfluxDbService");
            throw;
        }
    }

    public static InfluxDbService CreateInstance(IOptions<InfluxDBOptions> options, 
        ElectricityPriceService priceService, ILogger<InfluxDbService> logger)
    {
        return new InfluxDbService(options, priceService, logger);
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
                    .Tag("meter_serial", meterInfo.MeterSerial ?? "unknown")
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

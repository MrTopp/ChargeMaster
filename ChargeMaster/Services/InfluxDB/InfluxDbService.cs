using ChargeMaster.Services.Wallbox;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
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

    public InfluxDbService(IOptions<InfluxDBOptions> options, ILogger<InfluxDbService> logger)
    {
        _options = options.Value;
        _logger = logger;

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

            var points = new List<PointData>
            {
                PointData.Measurement("wallbox_meter")
                    .Tag("meter_serial", meterInfo.MeterSerial ?? "unknown")
                    .Field("acc_energy_wh", meterInfo.AccEnergy)
                    .Field("phase1_current_energy_w", meterInfo.Phase1CurrentEnergy)
                    .Field("phase2_current_energy_w", meterInfo.Phase2CurrentEnergy)
                    .Field("phase3_current_energy_w", meterInfo.Phase3CurrentEnergy)
                    .Field("current_energy_w", meterInfo.CurrentEnergy)
                    .Timestamp(timestamp, WritePrecision.Ns)
            };

            await _client.GetWriteApiAsync().WritePointsAsync(points, _options.Bucket, _options.Org);

            _logger.LogDebug("WallboxMeterInfo written to InfluxDB: {Serial}", meterInfo.MeterSerial);
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

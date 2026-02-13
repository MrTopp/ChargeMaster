using ChargeMaster.Data;
using ChargeMaster.Models;
using ChargeMaster.Services;

using Microsoft.EntityFrameworkCore;

namespace ChargeMaster.Workers;

/// <summary>
/// Event arguments containing Wallbox meter information.
/// </summary>
public class MeterInfoEventArgs : EventArgs
{
    /// <summary>
    /// Gets the meter information.
    /// </summary>
    public WallboxMeterInfo MeterInfo { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MeterInfoEventArgs"/> class.
    /// </summary>
    /// <param name="meterInfo">The meter information to include in the event.</param>
    public MeterInfoEventArgs(WallboxMeterInfo meterInfo)
    {
        MeterInfo = meterInfo;
    }
}

/// <summary>
/// Background service responsible for managing the Wallbox charger.
/// Handles tasks such as status monitoring, time synchronization, schedule enforcement,
/// and recording energy consumption data.
/// </summary>
public class WallboxWorker(IServiceProvider serviceProvider,
    WallboxService wallboxService, ILogger<WallboxWorker> logger) : BackgroundService
{
    /// <summary>
    /// Event raised when meter information is calculated and ready for consumption.
    /// </summary>
    public event EventHandler<MeterInfoEventArgs>? MeterInfoCalculated;

    /// <summary>
    /// Tracks the last recorded accumulated energy value to avoid storing duplicate readings.
    /// </summary>
    private double? LastStoredAccEnergy { get; set; }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await WallboxLoop(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in WallboxMeterWorker loop");
                await Task.Delay(TimeSpan.FromSeconds(60 * 10), stoppingToken);
            }
        }

    }

    /// <summary>
    /// The main operational loop that runs periodically to perform charger maintenance and data collection.
    /// </summary>
    /// <param name="stoppingToken">Token to monitor for cancellation requests.</param>
    internal async Task WallboxLoop(CancellationToken stoppingToken)
    {

        while (!stoppingToken.IsCancellationRequested)
        {
            // Initiera genom att läsa upp status
            WallboxStatus wallboxStatus = await InitializeWallboxStatusAsync(stoppingToken);

            // Kontrollera klockan på wallboxen
            await CheckWallboxTimeAsync(wallboxStatus);

            // Kontrollera schema 
            //await CheckWallboxScheduleAsync();
            
            // Spara status på förbrukad el
            WallboxMeterInfo? meterInfo = await ReadEnergyAsync(stoppingToken);

            // Räkna ut nuvarande effekt
            CalculateCurrentPowerAsync(meterInfo);

            // vänta innan nästa iteration
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    private void CalculateCurrentPowerAsync(WallboxMeterInfo? meterInfo)
    {
        if (meterInfo is null)
            return;
        MeterInfoCalculated?.Invoke(this, new MeterInfoEventArgs(meterInfo));
    }

    /// <summary>
    /// Initializes communications by retrieving the Wallbox status.
    /// Retries indefinitely until a valid status is received.
    /// </summary>
    /// <param name="stoppingToken">Token to monitor for cancellation requests.</param>
    /// <returns>The initial <see cref="WallboxStatus"/>.</returns>
    internal async Task<WallboxStatus> InitializeWallboxStatusAsync(CancellationToken stoppingToken)
    {
        WallboxStatus? wallboxStatus = await wallboxService.GetStatusAsync();
        while (wallboxStatus is null)
        {
            logger.LogError("Wallbox status is null, retrying in 1 minute");
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            wallboxStatus = await wallboxService.GetStatusAsync();
        }
        return wallboxStatus;
    }

    /// <summary>
    /// Enforces the charging schedule on the Wallbox based on the current season (Summer/Winter).
    /// </summary>
    /// <remarks>
    /// In summer (April-October), no schedule restrictions are applied.
    /// In winter, specific charging slots (00:00-07:00 and 19:00-24:00) are enforced on weekdays.
    /// </remarks>
    internal async Task CheckWallboxScheduleAsync()
    {
        var schema = await wallboxService.GetSchemaAsync();
        if (schema is null) return;

        var now = DateTime.Now;
        var start = new DateTime(now.Year, 4, 1);
        var end = new DateTime(now.Year, 10, 31);
        var isSummerPeriod = now.Date >= start && now.Date <= end;

        if (isSummerPeriod)
        {
            if (schema.Count == 0) return;

            foreach (var entry in schema)
            {
                await wallboxService.DeleteSchemaAsync(entry.SchemaId);
            }

            return;
        }


        var allowed = schema
            .Where(e => IsAllowedWinterTimeSlot(e.Start, e.Stop))
            .ToList();
        if (allowed.Count == 10 && schema.Count == 10)
            return;

        // Remove all not allowed entries
        foreach (var entry in schema.OrderByDescending(x => x.SchemaId))
        {
            if (!IsAllowedWinterTimeSlot(entry.Start, entry.Stop))
                await wallboxService.DeleteSchemaAsync(entry.SchemaId);
        }

        // Add missing entries
        for (int day = 1; day <= 5; day++)
        {
            WallboxSchemaEntry entry1 = new() { Start = "00:00:00", Stop = "07:00:00", Weekday = day.ToString(), ChargeLimit = 0 };
            if (!schema.Any(e => e.Equals(entry1)))
                await wallboxService.SetSchemaAsync(entry1);
            WallboxSchemaEntry entry2 = new() { Start = "19:00:00", Stop = "24:00:00", Weekday = day.ToString(), ChargeLimit = 0 };
            if (!schema.Any(e => e.Equals(entry2)))
                await wallboxService.SetSchemaAsync(entry2);
        }
    }

    /// <summary>
    /// Determines if a given time slot is allowed during the winter period.
    /// </summary>
    /// <param name="start">Start time string.</param>
    /// <param name="stop">Stop time string.</param>
    /// <returns>True if the slot matches permitted winter hours; otherwise false.</returns>
    private static bool IsAllowedWinterTimeSlot(string? start, string? stop)
    {
        if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(stop)) return false;

        if (!TimeOnly.TryParse(start, out var startTime)) return false;
        if (!TimeOnly.TryParse(stop, out var stopTime)) return false;

        if (startTime == new TimeOnly(0, 0)
            && stopTime == new TimeOnly(7, 0)) return true;

        if (startTime == new TimeOnly(19, 0)
            && stopTime == new TimeOnly(0, 0)) return true;

        return false;
    }

    /// <summary>
    /// Checks that the Wallbox clock is synchronized with the server time and updates it if the drift exceeds 5 minutes.
    /// </summary>
    /// <param name="wallboxStatus">The current status object containing the Wallbox time.</param>
    internal async Task CheckWallboxTimeAsync(WallboxStatus wallboxStatus)
    {
        // Wallboxens tid i format HH:mm
        string? wallboxTime = wallboxStatus.ChargeboxTime;
        if (wallboxTime is null) return;
        // Kontrollera om klockan är felaktig
        if (DateTime.TryParseExact(wallboxTime, "HH:mm", null,
                System.Globalization.DateTimeStyles.None, out DateTime wallboxDateTime))
        {
            DateTime now = DateTime.Now;
            DateTime correctWallboxTime = new DateTime(now.Year, now.Month, now.Day, wallboxDateTime.Hour, wallboxDateTime.Minute, 0);
            TimeSpan timeDifference = now - correctWallboxTime;
            if (timeDifference.Duration() > TimeSpan.FromMinutes(5))
            {
                logger.LogInformation("Wallbox time is incorrect by {Difference}. Updating time.", timeDifference);
                await wallboxService.SetTimeAsync(now);
            }
        }
    }

    private DateTimeOffset? _lastReadingAt;
    private long? _lastReading;

    /// <summary>
    /// Reads the current meter information from the Wallbox and persists it to the database if the accumulated energy has changed.
    /// </summary>
    /// <param name="stoppingToken">Token to monitor for cancellation requests.</param>
    /// <returns>The meter information that was read, or null if no information was available.</returns>
    internal async Task<WallboxMeterInfo?> ReadEnergyAsync(CancellationToken stoppingToken)
    {
        try
        {
            WallboxMeterInfo? info = await wallboxService.GetMeterInfoAsync();
            if (info is null)
                return null;

            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetService<ApplicationDbContext>();

            // Skip database operations if db context is not available
            if (db is null)
            {
                logger.LogWarning("Database context is not available. Meter info will not be persisted.");
                return info;
            }

            // Initialize LastStoredAccEnergy if not set (first run)
            if (!LastStoredAccEnergy.HasValue)
            {
                LastStoredAccEnergy = await db.WallboxMeterReadings
                    .OrderByDescending(r => r.ReadAt)
                    .Select(r => (double?)r.AccEnergy)
                    .FirstOrDefaultAsync(stoppingToken);
            }

            // Skip if the accumulated energy is the same as last stored
            if (LastStoredAccEnergy.HasValue && Math.Abs(LastStoredAccEnergy.Value - info.AccEnergy) < 0.01) 
                return info;

            var entry = new WallboxMeterReading
            {
                ReadAt = DateTime.Now,
                RawJson = System.Text.Json.JsonSerializer.Serialize(info),
                AccEnergy = info.AccEnergy,
                MeterSerial = info.MeterSerial,
                ApparentPower = info.ApparentPower
            };

            db.WallboxMeterReadings.Add(entry);
            await db.SaveChangesAsync(stoppingToken);
           // logger.LogInformation("Energy reading {energy} at {time}", info.AccEnergy, DateTimeOffset.Now);
            if (_lastReading != null && _lastReadingAt != null)
            {
                var tid = (TimeSpan)(DateTimeOffset.Now - _lastReadingAt);
                var sec = (long)tid.TotalSeconds;

                var effect = sec != 0 ? 3600.0 / sec / 10 : 0;
                
                logger.LogInformation("Energy usage {effect:F1} kW", effect);
            }

            _lastReadingAt = DateTimeOffset.Now;
            _lastReading = info.AccEnergy;

            LastStoredAccEnergy = info.AccEnergy;

            return info;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read or store meter info");
            return null;
        }
    }

}

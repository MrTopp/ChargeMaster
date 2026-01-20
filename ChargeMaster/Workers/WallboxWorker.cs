using ChargeMaster.Data;
using ChargeMaster.Models;
using ChargeMaster.Services;

using Microsoft.EntityFrameworkCore;

namespace ChargeMaster.Workers;

/// <summary>
/// 
/// </summary>
public class WallboxWorker(IServiceProvider serviceProvider,
    WallboxService wallboxService, ILogger<WallboxWorker> logger) : BackgroundService
{
    private double? _lastStoredAccEnergy;

    private bool isConnected { get; set; } = false;

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
            }
        }

    }
    protected async Task WallboxLoop(CancellationToken stoppingToken)
    {

        while (!stoppingToken.IsCancellationRequested)
        {
            // Initiera genom att läsa upp status
            WallboxStatus wallboxStatus = await InitializeWallboxStatus(stoppingToken);

            // Kontrollera klockan på wallboxen
            await CheckWallboxTime(wallboxStatus);

            // Kontrollera schema 
            await CheckWallboxSchedule();

            // Kontrollera om bilen är ansluten
            await CheckVehicle(wallboxStatus);

            // Spara status på förbrukad el
            await ReadAndStoreAsync(stoppingToken);

        }
    }

    private async Task CheckVehicle(WallboxStatus wallboxStatus)
    {
        bool isConnectedNow = wallboxStatus.Connector == "CONNECTED";
        if (isConnectedNow && !isConnected)
        {
            // vi går live (bilen nu ansluten)
            // Beräkna laddningsschema
        }
        // spara state connected
        isConnected = isConnectedNow;

    }

    internal async Task<WallboxStatus> InitializeWallboxStatus(CancellationToken stoppingToken)
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

    internal async Task CheckWallboxSchedule()
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
    /// Kontrollera att wallboxens klocka är korrekt och uppdatera den vid behov
    /// </summary>
    internal async Task CheckWallboxTime(WallboxStatus wallboxStatus)
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

    private async Task ReadAndStoreAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var wallbox = scope.ServiceProvider.GetRequiredService<WallboxService>();

            var info = await wallbox.GetMeterInfoAsync();
            if (info is null) return;

            var readAt = DateTimeOffset.FromUnixTimeMilliseconds(info.ReadTime).UtcDateTime;

            // Do not store if already exists for the same read time
            if (!_lastStoredAccEnergy.HasValue)
            {
                _lastStoredAccEnergy = await db.WallboxMeterReadings
                    .OrderByDescending(r => r.ReadAt)
                    .Select(r => (double?)r.AccEnergy)
                    .FirstOrDefaultAsync(stoppingToken);
            }

            if (_lastStoredAccEnergy.HasValue && Math.Abs(_lastStoredAccEnergy.Value - info.AccEnergy) < 0.01) return;

            var entry = new WallboxMeterReading
            {
                ReadAt = readAt,
                RawJson = System.Text.Json.JsonSerializer.Serialize(info),
                AccEnergy = info.AccEnergy,
                MeterSerial = info.MeterSerial,
                ApparentPower = info.ApparentPower
            };

            db.WallboxMeterReadings.Add(entry);
            await db.SaveChangesAsync(stoppingToken);

            _lastStoredAccEnergy = info.AccEnergy;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read or store meter info");
        }
    }
}

using ChargeMaster.Data;
using ChargeMaster.Models;
using Microsoft.EntityFrameworkCore;

namespace ChargeMaster.Services;

/// <summary>
/// Background service that reads meter info every hour and stores it in the database.
/// </summary>
public class WallboxMeterWorker(IServiceProvider serviceProvider, ILogger<WallboxMeterWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // On startup, run immediately then align to the next hour.
        await ReadAndStoreAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextHour = now.AddHours(1);
            var nextRun = new DateTime(nextHour.Year, nextHour.Month, nextHour.Day, nextHour.Hour, 0, 0);
            var delay = nextRun - now;
            try
            {
                await Task.Delay(delay, stoppingToken);
                await ReadAndStoreAsync(stoppingToken);
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

    private async Task ReadAndStoreAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var wallbox = scope.ServiceProvider.GetRequiredService<IWallboxService>();

            var info = await wallbox.GetMeterInfoAsync();
            if (info is null) return;

            var readAt = DateTimeOffset.FromUnixTimeMilliseconds(info.ReadTime).UtcDateTime;

            // Do not store if already exists for the same read time
            var exists = await db.WallboxMeterReadings.AnyAsync(r => r.ReadAt == readAt, stoppingToken);
            if (exists) return;

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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read or store meter info");
        }
    }
}

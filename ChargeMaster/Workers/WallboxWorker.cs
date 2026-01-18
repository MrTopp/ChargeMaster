using ChargeMaster.Data;
using ChargeMaster.Services;

using Microsoft.EntityFrameworkCore;

namespace ChargeMaster.Workers;

/// <summary>
/// Background service that reads meter info every hour and stores it in the database.
/// </summary>
public class WallboxWorker(IServiceProvider serviceProvider, ILogger<WallboxWorker> logger) : BackgroundService
{
    private double? _lastStoredAccEnergy;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // On startup, run immediately then continue every minute.
        await ReadAndStoreAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
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

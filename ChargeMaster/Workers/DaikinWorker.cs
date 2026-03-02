using ChargeMaster.Services.Daikin;

namespace ChargeMaster.Workers;

/// <summary>
/// Bakgrundstjänst som en gång i timmen läser status från Daikin värmepump.
/// </summary>
public class DaikinWorker(
    DaikinFacade daikin,
    ILogger<DaikinWorker> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DaikinLoop(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in DaikinWorker loop");
            }
        }
    }

    private async Task DaikinLoop(CancellationToken stoppingToken)
    {
        await daikin.InitializeAsync(forceEvent: true);

        DateTime previous = DateTime.Now;

        while (!stoppingToken.IsCancellationRequested)
        {
            DateTime dt = DateTime.Now;
            DateTime nu = new DateTime(dt.Year, dt.Month, dt.Day,
                dt.Hour, dt.Minute, 0);

            // Uppdatera status och skicka event vid ändring
            await daikin.InitializeAsync(forceEvent: false);


        NextIteration:
            // Vänta tills nästa hela minut
            var targetNextMinute = nu.AddMinutes(1);
            while (DateTime.Now < targetNextMinute && !stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(100, stoppingToken);
            }
            previous = nu;
        }
    }


    private static async Task WaitUntilNextMinute(CancellationToken stoppingToken)
    {
        const int maxDelayMs = 50;

        while (!stoppingToken.IsCancellationRequested)
        {
            var remainingDelay = GetDelayUntilNextMinute();

            if (remainingDelay.TotalMilliseconds <= 0)
            {
                break;
            }

            var delayMs = Math.Min((int)remainingDelay.TotalMilliseconds, maxDelayMs);
            await Task.Delay(delayMs, stoppingToken);
        }
    }

    private static TimeSpan GetDelayUntilNextMinute()
    {
        var now = DateTime.Now;
        var nextMinute = now.AddMinutes(1)
            .AddSeconds(-now.Second)
            .AddMilliseconds(-now.Millisecond);
        return nextMinute - now;
    }

    private Task ReadDaikinStatus(CancellationToken stoppingToken)
    {
        // TODO: Implementera läsning av Daikin-status
        return Task.CompletedTask;
    }
}

namespace ChargeMaster.Services;

public class PriceFetchingWorker(IServiceProvider serviceProvider, ILogger<PriceFetchingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. Startup check: Ensure current day's prices are fetched
        await CheckAndFetchAsync(DateOnly.FromDateTime(DateTime.Now), stoppingToken);

        // 2. Schedule daily fetch at 13:10
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRun = now.Date.AddHours(13).AddMinutes(10);
            
            // If 13:10 has already passed today, schedule for tomorrow
            if (now >= nextRun)
            {
                nextRun = nextRun.AddDays(1);
            }

            var delay = nextRun - now;
            logger.LogInformation("Next price fetch scheduled for {NextRun}", nextRun);

            try
            {
                await Task.Delay(delay, stoppingToken);

                // At 13:10, we usually fetch prices for the NEXT day
                var tomorrow = DateOnly.FromDateTime(DateTime.Now.AddDays(1));
                await CheckAndFetchAsync(tomorrow, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during scheduled price fetching.");
            }
        }
    }

    private async Task CheckAndFetchAsync(DateOnly date, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var priceService = scope.ServiceProvider.GetRequiredService<IElectricityPriceService>();
            
            logger.LogInformation("Worker triggering price fetch for {Date}", date);
            await priceService.FetchAndStorePricesForDateAsync(date);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Worker failed to fetch prices for {Date}", date);
        }
    }
}

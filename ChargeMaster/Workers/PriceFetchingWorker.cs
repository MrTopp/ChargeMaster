using ChargeMaster.Services;

namespace ChargeMaster.Workers;


/// <summary>
/// A background service that schedules and executes daily electricity price fetching tasks at a specified time.
/// </summary>
/// <remarks>This worker ensures that electricity prices are fetched for the current day on startup and then
/// schedules a recurring fetch at 13:10 each day, typically for the following day's prices. The service is designed to
/// run continuously until the application is stopped. Logging is provided for both successful operations and error
/// conditions.</remarks>
/// <param name="serviceProvider">The service provider used to resolve application services required for price fetching operations.</param>
/// <param name="logger">The logger used to record informational and error messages related to the worker's execution.</param>
public class PriceFetchingWorker(IServiceProvider serviceProvider, ILogger<PriceFetchingWorker> logger) : BackgroundService
{
    /// <summary>
    /// Executes the background service logic to ensure daily price data is fetched and scheduled at the appropriate
    /// time.
    /// </summary>
    /// <remarks>On startup, this method ensures that the current day's prices are fetched. It then schedules
    /// a daily fetch at 13:10 local time, typically to retrieve prices for the next day. The method continues running
    /// until cancellation is requested via the provided token. If an error occurs during a scheduled fetch, it is
    /// logged and the service continues running.</remarks>
    /// <param name="stoppingToken">A cancellation token that can be used to request graceful termination of the background operation.</param>
    /// <returns>A task that represents the asynchronous execution of the background service.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Ensure current day's prices are fetched
        await CheckAndFetchAsync(DateOnly.FromDateTime(DateTime.Now), stoppingToken);

        // Fetch tomorrow's prices if past 13:00 on startup
        var now = DateTime.Now;
        if (now.Hour >= 13)
        {
            await CheckAndFetchAsync(DateOnly.FromDateTime(DateTime.Now.AddDays(1)), stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = now.Date.AddHours(13).AddMinutes(10);
            var delay = nextRun - now;
            if (delay < TimeSpan.Zero)
            {
                delay = delay.Add(TimeSpan.FromDays(1));
            }
            
            try
            {
                await Task.Delay(delay, stoppingToken);

                // At 13:10, we usually fetch prices for the NEXT day
                var tomorrow = DateOnly.FromDateTime(DateTime.Now.AddDays(1));
                
                bool success = await CheckAndFetchAsync(tomorrow, stoppingToken);
                while (!success && !stoppingToken.IsCancellationRequested)
                {
                    logger.LogInformation("Fetch failed for tomorrow's prices. Retrying in 10 minutes...");
                    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                    success = await CheckAndFetchAsync(tomorrow, stoppingToken);
                }
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

    /// <summary>
    /// Checks for electricity prices for the specified date and initiates an asynchronous fetch and store operation.
    /// </summary>
    /// <param name="date">The date for which to fetch electricity prices.</param>
    /// <param name="stoppingToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation, returning true if successful.</returns>
    private async Task<bool> CheckAndFetchAsync(DateOnly date, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            ElectricityPriceService priceService = scope.ServiceProvider.GetRequiredService<ElectricityPriceService>();
            
            logger.LogInformation("Worker triggering price fetch for {Date}", date);
            await priceService.FetchAndStorePricesForDateAsync(date);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Worker failed to fetch prices for {Date}", date);
            return false;
        }
    }
}

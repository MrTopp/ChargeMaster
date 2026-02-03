using ChargeMaster.Services;
// ReSharper disable UnusedParameter.Local

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
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool success = await CheckAndFetchAsync(DateOnly.FromDateTime(DateTime.Now), stoppingToken);

        var now = DateTime.Now;
        if (now.Hour >= 13)
        {
            success = await CheckAndFetchAsync(DateOnly.FromDateTime(now.AddDays(1)), stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                now = DateTime.Now;
                var nextRun = CalculateNextRunTime(now, success);
                var delay = nextRun - now;

                await Task.Delay(delay, stoppingToken);
                var tomorrow = DateOnly.FromDateTime(DateTime.Now.AddDays(1));
                success = await CheckAndFetchAsync(tomorrow, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred during scheduled price fetching.");
            }
        }
    }

    /// <summary>
    /// Calculates the next time to run the price fetch based on current time and success of the last fetch.
    /// </summary>
    private DateTime CalculateNextRunTime(DateTime now, bool success)
    {
        DateTime nextRun;

        if (success && (now.Hour < 13 || now is { Hour: 13, Minute: < 10 }))
        {
            nextRun = now.Date.AddHours(13).AddMinutes(10);
        }
        else if (success)
        {
            nextRun = now.Date.AddDays(1).AddHours(13).AddMinutes(10);
        }
        else
        {
            nextRun = now.AddMinutes(30);
        }

        return nextRun;
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
            // Kontrollera att morgondagens priser inte hämtas före 13:00
            if (date == DateOnly.FromDateTime(DateTime.Now.AddDays(1)) && DateTime.Now.Hour < 13)
            {
                logger.LogInformation("Skipping fetch for {Date} as it's before 13:00", date);
                return true;
            }

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

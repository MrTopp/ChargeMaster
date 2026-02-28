using ChargeMaster.Services.Daikin;

namespace ChargeMaster.Workers;

/// <summary>
/// Bakgrundstjänst som en gång i timmen läser status från Daikin värmepump.
/// </summary>
public class DaikinWorker(
    DaikinService daikinService,
    ILogger<DaikinWorker> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReadDaikinStatus(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in DaikinWorker loop");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private Task ReadDaikinStatus(CancellationToken stoppingToken)
    {
        // TODO: Implementera läsning av Daikin-status
        return Task.CompletedTask;
    }
}

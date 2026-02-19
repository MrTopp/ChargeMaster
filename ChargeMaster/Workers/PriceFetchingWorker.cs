using ChargeMaster.Services.ElectricityPrice;

// ReSharper disable UnusedParameter.Local

namespace ChargeMaster.Workers;

/// <summary>
/// En bakgrundstjänst som schemalägger och kör dagliga elprishämtningsuppgifter vid en angiven tid.
/// </summary>
/// <remarks>Den här arbetaren säkerställer att elpriser hämtas för aktuell dag vid start och schemalägger sedan
/// en återkommande hämtning kl. 13:10 varje dag, vanligtvis för följande dags priser. Tjänsten är utformad för att
/// köras kontinuerligt tills programmet stoppas. Loggning tillhandahålls för både lyckade operationer och feltillstånd.
/// </remarks>
/// <param name="serviceProvider">Tjänsteleverantören som används för att lösa programtjänster som krävs för prishämtningsoperationer.</param>
/// <param name="logger">Loggern som används för att registrera informations- och felmeddelanden relaterade till arbetarens körning.</param>
public class PriceFetchingWorker(
    IServiceProvider serviceProvider,
    ILogger<PriceFetchingWorker> logger) : BackgroundService
{
    /// <summary>
    /// Kör bakgrundslogiken för tjänsten för att säkerställa att daglig prisdata hämtas och schemaläggs vid lämplig
    /// tid.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool success = await CheckAndFetchAsync(DateOnly.FromDateTime(DateTime.Now), stoppingToken);

        var now = DateTime.Now;
        if (now.Hour >= 13)
        {
            success = await CheckAndFetchAsync(DateOnly.FromDateTime(now.AddDays(1)),
                stoppingToken);
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
                logger.LogInformation(ex, "Ett fel inträffade under schemalägning av pushishämtning.");
            }
        }
    }

    /// <summary>
    /// Beräknar nästa gång för att köra prishämtningen baserat på aktuell tid och framgång för den senaste hämtningen.
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
    /// Kontrollerar elpriser för det angivna datumet och initierar en asynkron hämtnings- och lagringsoperation.
    /// </summary>
    /// <param name="date">Datumet för vilket elpriser ska hämtas.</param>
    /// <param name="stoppingToken">En avbytningstoken som kan användas för att avbryta operationen.</param>
    /// <returns>En uppgift som repräsenterar den asynkrona operationen och returnerar true om den lyckas.</returns>
    private async Task<bool> CheckAndFetchAsync(DateOnly date, CancellationToken stoppingToken)
    {
        try
        {
            // Kontrollera att morgondagens priser inte hämtas före 13:00
            if (date == DateOnly.FromDateTime(DateTime.Now.AddDays(1)) && DateTime.Now.Hour < 13)
            {
                logger.LogInformation("Hämtar inte {Date} då det är innan 13:00", date);
                return true;
            }

            using var scope = serviceProvider.CreateScope();
            ElectricityPriceService priceService
                = scope.ServiceProvider.GetRequiredService<ElectricityPriceService>();

            logger.LogInformation("Arbetare initierar pushishämtning för {Date}", date);
            await priceService.FetchAndStorePricesForDateAsync(date);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Arbetare kunde inte hämta priser för {Date}", date);
            return false;
        }
    }
}

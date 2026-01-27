using ChargeMaster.Data;
using ChargeMaster.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System;
using System.Threading;
using System.Threading.Tasks;
using ChargeMaster.Models;

namespace ChargeMaster.Workers;

public class ChargeWorker(
    IServiceProvider serviceProvider,
    ILogger<ChargeWorker> logger,
    WallboxService wallbox,
    VWService vwService) : BackgroundService
{
    // Kvartar när laddning skall ske
    private List<ElectricityPrice> _kvartlista = new List<ElectricityPrice>();

    // Flagga om laddning är tillåten denna timme
    private bool Timladdning { get; set; }

    private long FörbrukningVidTimstart { get; set; }

    private async Task<long> InitieraFörbrukningAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.Now;
        var startOfHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);

        // Fetch closest reading to start of hour
        var before = await context.WallboxMeterReadings
            .Where(x => x.ReadAt <= startOfHour)
            .OrderByDescending(x => x.ReadAt)
            .FirstOrDefaultAsync(cancellationToken);

        var after = await context.WallboxMeterReadings
            .Where(x => x.ReadAt >= startOfHour)
            .OrderBy(x => x.ReadAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (before == null && after == null) return 0;
        if (before == null) return after!.AccEnergy;
        if (after == null) return before!.AccEnergy;

        var diffBefore = startOfHour - before.ReadAt;
        var diffAfter = after.ReadAt - startOfHour;

        return diffBefore <= diffAfter ? before.AccEnergy : after.AccEnergy;
    }

    private bool BilAnsluten { get; set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ChargeLoop(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in ChargeWorker loop");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    internal async Task ChargeLoop(CancellationToken stoppingToken)
    {
        FörbrukningVidTimstart = await InitieraFörbrukningAsync(stoppingToken);
        DateTime previous = DateTime.Now;

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("ChargeWorker tick at: {time}", DateTimeOffset.Now);
            WallboxMeterInfo? wstat = await wallbox.GetMeterInfoAsync();
            DateTime nu = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                DateTime.Now.Hour, DateTime.Now.Minute, 0);

            // ***** Varje minut

            // ----- Kontrollera förväntad timförbrukning(nu -timstart) *60 / minuter_nu
            long förbrukningDennaTimme = wstat.AccEnergy - FörbrukningVidTimstart;
            if (förbrukningDennaTimme * 60 / nu.Minute > 300)
            {
                //  För hög förbrukning -> stoppa laddning
                Timladdning = false;
                await StoppaLaddningAsync();
            }

            // ***** Varje timme
            if (nu.Hour != previous.Hour)
            {
                //    - nolla timladdning
                Timladdning = true;
                //    - spara förbrukad el
                FörbrukningVidTimstart = wstat.AccEnergy;
            }

            // ***** Varje kvart
            if (nu.Minute % 15 == 0 && nu.Minute != previous.Minute)
            {
                //    - om finns i listan med kvartar och timladdning
                if (_kvartlista.Any(x =>
                        x.TimeStart.Hour == nu.Hour && x.TimeStart.Minute == nu.Minute)
                    && Timladdning)
                {
                    await StartaLaddningAsync();
                }
                else
                {
                    await StoppaLaddningAsync();
                }
            }


            var paus = nu.AddMinutes(1) - DateTime.Now;
            await Task.Delay(paus, stoppingToken);
            previous = nu;
        }
    }

    private bool bilenLaddar = false;

    internal async Task StartaLaddningAsync()
    {
        if (Timladdning && !bilenLaddar)
        {
            bilenLaddar = await vwService.StartChargingAsync();
        }
    }

    internal async Task StoppaLaddningAsync()
    {
        if (bilenLaddar)
            bilenLaddar = await vwService.StopChargingAsync();
    }
}
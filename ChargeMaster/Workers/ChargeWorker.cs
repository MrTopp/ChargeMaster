using ChargeMaster.Data;
using ChargeMaster.Services;

using Microsoft.EntityFrameworkCore;
using ChargeMaster.Models;

namespace ChargeMaster.Workers;

public class ChargeWorker : BackgroundService
{
    // Kvartar när laddning skall ske
    private List<ElectricityPrice> _kvartlista = new List<ElectricityPrice>();

    // Flagga om laddning är tillåten denna timme
    private bool Timladdning { get; set; }

    private long FörbrukningVidTimstart { get; set; }

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChargeWorker> _logger;
    private readonly WallboxService _wallbox;
    private readonly VWService _vwService;

    public ChargeWorker(IServiceProvider serviceProvider,
        ILogger<ChargeWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _wallbox = serviceProvider.GetService<WallboxService>();
        _vwService = serviceProvider.GetService<VWService>();
    }


    private async Task<long> InitieraFörbrukningAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
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
                _logger.LogError(ex, "Error in ChargeWorker loop");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    internal async Task ChargeLoop(CancellationToken stoppingToken)
    {
        FörbrukningVidTimstart = await InitieraFörbrukningAsync(stoppingToken);
        DateTime previous = DateTime.Now;
        Timladdning = true;
        bilenLaddar = await LaddStatus();

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogTrace("ChargeWorker tick at: {time}", DateTimeOffset.Now);
            WallboxMeterInfo? wstat = await _wallbox.GetMeterInfoAsync();
            DateTime nu = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                DateTime.Now.Hour, DateTime.Now.Minute, 0);

            // ***** Varje minut

            // ----- Kontrollera förväntad timförbrukning(nu -timstart) *60 / minuter_nu
            long förbrukningDennaTimme = wstat.AccEnergy - FörbrukningVidTimstart;
            if (förbrukningDennaTimme > 3000 && Timladdning)
            {
                //  För hög förbrukning -> stoppa laddning
                _logger.LogInformation("Charging disabled due to high consumption: {consumption} Wh.", förbrukningDennaTimme);
                Timladdning = false;
                await StoppaLaddningAsync();
            }

            // ***** Varje timme
            if (nu.Hour != previous.Hour)
            {
                _logger.LogInformation("Hourly consumption: {consumption} Wh", förbrukningDennaTimme);
                Timladdning = true;
                FörbrukningVidTimstart = wstat.AccEnergy;
            }

            // ***** Varje kvart
            if (nu.Minute % 15 == 0 && nu.Minute != previous.Minute)
            {
                await SkapaKvartlista();
                int minutAvrundad = nu.Minute / 15 * 15;

                //    - om finns i listan med kvartar och timladdning
                if (_kvartlista.Any(x =>
                        x.TimeStart.Hour == nu.Hour && x.TimeStart.Minute == minutAvrundad))
                {
                    if (Timladdning)
                    {
                        _logger.LogInformation("Quarter, evaluate charge, starting");
                        await StartaLaddningAsync();
                    }
                    else
                    {
                        _logger.LogInformation("Quarter, evaluate charge, high consumption");
                    }
                }
                else
                {
                    _logger.LogInformation("Quarter, evaluate charge, not starting");
                    await StoppaLaddningAsync();
                }
            }

            var paus = nu.AddMinutes(1) - DateTime.Now;
            if (paus.TotalSeconds > 0)
                await Task.Delay(paus, stoppingToken);
            previous = nu;
        }
    }

    private bool bilenLaddar { get; set; }

    internal async Task StartaLaddningAsync()
    {
        if (Timladdning && !bilenLaddar)
        {
            bilenLaddar = await _vwService.StartChargingAsync();
            if (bilenLaddar)
            {
                _logger.LogInformation("Charging started successfully.");
            }
        }
    }

    internal async Task StoppaLaddningAsync()
    {
        if (bilenLaddar)
        {
            bool success = await _vwService.StopChargingAsync();
            if (success)
            {
                _logger.LogInformation("Charging stopped successfully.");
                bilenLaddar = false;
            }
        }
    }

    internal async Task<bool> LaddStatus()
    {
        VWStatusResponse? response = await _vwService.GetStatus();
        return (response?.Status?.ChargingPower ?? 0) > 0;
    }

    /// <summary>
    /// Räknar ut behov av laddning i procent
    /// </summary>
    /// <returns></returns>
    internal async Task<double> LaddBehov()
    {
        // Beräkna laddbehov
        VWStatusResponse? response = await _vwService.GetStatus();
        if (response?.Status == null)
            return 0;
        if (response.Status.VehicleState == VWVehicleState.Unknown)
            return 0;
        var status = response.Status;
        if (status.BatteryLevel == null)
            return 0;
        double level = status.BatteryLevel ?? 0;
        double target = status.ChargingSettingsTargetLevel ?? 0;

        return target - level;
    }

    /// <summary>
    /// Skapa lista med kvartar där laddning skall vara aktiv
    /// </summary>
    /// <returns></returns>
    internal async Task SkapaKvartlista()
    {
        _kvartlista.Clear();
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var priser = await context.ElectricityPrices
            .Where(x => x.TimeStart >= DateTime.Now.AddMinutes(-30)
            && x.TimeStart < DateTime.Today.AddDays(1).AddHours(7)
                        )
            .OrderBy(x => x.TimeStart)
            .ToListAsync();

        // Beräkna laddbehov
        var behovProcent = await LaddBehov();
        // Kan behöva justering
        var antalKvartar = (int)(behovProcent * 1.3);


        _kvartlista = priser.Where(x => x.ChargingAllowed)
            .OrderBy(x => x.SekPerKwh)
            .Take(antalKvartar)
            .ToList();
    }

}
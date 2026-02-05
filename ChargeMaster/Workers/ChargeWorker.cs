using ChargeMaster.Data;
using ChargeMaster.Services;
using Microsoft.EntityFrameworkCore;
using ChargeMaster.Models;

namespace ChargeMaster.Workers;

public class ChargeWorker(
    IServiceProvider serviceProvider,
    ILogger<ChargeWorker> logger)
    : BackgroundService
{
    // Kvartar när laddning skall ske
    private List<ElectricityPrice> _kvartlista = new();

    // Flagga om laddning är tillåten denna timme
    private bool Timladdning { get; set; }

    private long FörbrukningVidTimstart { get; set; }

    private readonly WallboxService _wallbox = serviceProvider.GetService<WallboxService>() ??
                                               throw new InvalidOperationException(
                                                   "Initiering av WallboxService misslyckas");

    private readonly VWService _vwService = serviceProvider.GetService<VWService>() ??
                                            throw new InvalidOperationException(
                                                "Initiering av VWService misslyckas");


    private async Task<long> InitieraFörbrukningAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.Now;
        var startOfHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);

        // Fetch the closest reading to start of hour
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
        if (after == null) return before.AccEnergy;

        var diffBefore = startOfHour - before.ReadAt;
        var diffAfter = after.ReadAt - startOfHour;

        return diffBefore <= diffAfter ? before.AccEnergy : after.AccEnergy;
    }

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
        Timladdning = true;
        BilenLaddar = await LaddStatus();
        var connectorStatus = ConnectionEnum.Unknown;

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogTrace("ChargeWorker tick at: {time}", DateTimeOffset.Now);
            DateTime nu = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                DateTime.Now.Hour, DateTime.Now.Minute, 0);
            var currentConnectorStatus = await GetConnectorStatusAsync();
            WallboxMeterInfo? wstat = await _wallbox.GetMeterInfoAsync();

            // ----- Varje timme, nollställ timförbrukning
            if (wstat != null)
            {
                long förbrukningDennaTimme = wstat.AccEnergy - FörbrukningVidTimstart;
                if (nu.Hour != previous.Hour)
                {
                    logger.LogInformation("Hourly consumption: {consumption} Wh",
                        förbrukningDennaTimme);
                    Timladdning = true;
                    FörbrukningVidTimstart = wstat.AccEnergy;
                }
            }

            // ----- Om bilen inte är hemma, hoppa över resten av loopen
            if (currentConnectorStatus == ConnectionEnum.SearchingForCommunication)
            {
                // Bilen inte hemma, laddning irrelevant
                // Använd inte continue utan hoppa till för att få med 
                // viloperioden i slutet på loopen
                goto NextIteration;
            }

            // ----- State-övergång
            if (currentConnectorStatus != connectorStatus)
            {
                logger.LogInformation(
                    $"Charge transition {connectorStatus}->{currentConnectorStatus}");

                // ----- Bilen börjar ladda.
                if (currentConnectorStatus == ConnectionEnum.Charging)
                {
                    // Bilen har börjat ladda, skall den stoppas?
                    // Det kan hända när bilen kopplas in, då skall laddningen 
                    // stoppas om det inte är rätt tid för laddning
                    logger.LogInformation("Car started charging");
                    await SkapaKvartlista();
                    int minutAvrundad = nu.Minute / 15 * 15;
                    if (!_kvartlista.Any(x =>
                            x.TimeStart.Hour == nu.Hour && x.TimeStart.Minute == minutAvrundad))
                    {
                        // Bilen skall stoppas
                        logger.LogInformation("Stopped charging.");
                        await StoppaLaddningAsync(force: true);
                    }
                }
                connectorStatus = currentConnectorStatus;
            }

            // ----- Kontrollera förväntad timförbrukning(nu -timstart) *60 / minuter_nu
            if (wstat != null)
            {
                long förbrukningDennaTimme = wstat.AccEnergy - FörbrukningVidTimstart;
                if (förbrukningDennaTimme > 2500 && Timladdning)
                {
                    //  För hög förbrukning -> stoppa laddning
                    logger.LogInformation(
                        "Charging disabled due to high consumption: {consumption} Wh.",
                        förbrukningDennaTimme);
                    Timladdning = false;
                    await StoppaLaddningAsync();
                }
            }


            // ***** Varje kvart
            if (nu.Minute % 15 == 0 && nu.Minute != previous.Minute)
            {
                logger.LogInformation("-- Quarter --");
                await SkapaKvartlista();
                int numin = nu.Minute;
                int minutAvrundad = numin / 15 * 15;

                foreach (var price in _kvartlista.OrderBy(x => x.TimeStart)/*.Take(5)*/)
                {
                    logger.LogInformation(
                        $"Kvart {price.TimeStart} - {price.TimeEnd} charge {price.ChargingAllowed}");
                }

                // Om 'nu' finns i listan med kvartar 
                if (_kvartlista.Any(x =>
                        x.TimeStart.Hour == nu.Hour && x.TimeStart.Minute == minutAvrundad))
                {
                    if (Timladdning)
                    {
                        logger.LogInformation("Quarter, evaluate charge, starting");
                        await StartaLaddningAsync();
                    }
                    else
                    {
                        logger.LogInformation("Quarter, evaluate charge, high consumption");
                    }
                }
                else
                {
                    var next = _kvartlista.OrderBy(x => x.TimeStart).FirstOrDefault();

                    logger.LogInformation("Quarter, not starting, next {time}",
                        next?.TimeStart.ToShortTimeString() ?? "---");
                    await StoppaLaddningAsync();
                }
            }

        NextIteration:
            TimeSpan paus = nu.AddMinutes(1) - DateTime.Now;
            if (paus.TotalSeconds > 0)
                await Task.Delay(paus, stoppingToken);
            previous = nu;
        }
    }

    private bool BilenLaddar { get; set; }

    internal async Task StartaLaddningAsync()
    {
        if (Timladdning && !BilenLaddar)
        {
            BilenLaddar = await _vwService.StartChargingAsync();
            if (BilenLaddar)
            {
                logger.LogInformation("Charging started successfully.");
            }
        }
    }

    internal async Task StoppaLaddningAsync(bool force = false)
    {
        if (BilenLaddar || force)
        {
            var behov = await LaddBehov();
            if (behov <= 1)
            {
                // Låt bilen bestämma
                logger.LogInformation($"StoppaLaddning:LaddBehov{behov}");
                return;
            }

            bool success = await _vwService.StopChargingAsync();
            if (success)
            {
                logger.LogInformation("Charging stopped successfully.");
                BilenLaddar = false;
            }
        }
    }

    internal async Task<bool> LaddStatus()
    {
        VWStatusResponse? response = await _vwService.GetStatus();
        return (response?.Status?.ChargingPower ?? 0) > 0;
    }

    internal async Task<ConnectionEnum> GetConnectorStatusAsync()
    {
        WallboxStatus? response = await _wallbox.GetStatusAsync();
        if (response == null)
            return ConnectionEnum.Unknown;
        switch (response.Connector)
        {
            case "CHARGING_PAUSED":
                return ConnectionEnum.ChargingPaused;
            case "CONNECTED":
                return ConnectionEnum.Connected;
            case "CHARGING":
                return ConnectionEnum.Charging;
            case "DISABLED":
                return ConnectionEnum.Disabled;
            case "CHARGING_FINISHED":
                return ConnectionEnum.ChargingFinished;
            case "SEARCH_COMM":
                return ConnectionEnum.SearchingForCommunication;

            default:
                logger.LogError("Unknown value för WallboxStatus.Connector: {value}",
                    response.Connector);
                return ConnectionEnum.Unknown;
        }
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
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var nu = DateTime.Now;
        var grans = new DateTime(nu.Year, nu.Month, nu.Day, nu.Hour, 0, 0);
        var priser = await context.ElectricityPrices
            .Where(x => x.TimeEnd >= grans
            && x.TimeStart < DateTime.Today.AddDays(1).AddHours(7)
            )
            .OrderBy(x => x.TimeStart)
            .ToListAsync();

        // Sätt ChargingAllowed = false på den dyraste kvarten varje timme
        var dyrasteKvartPerTimme =
            priser.GroupBy(x => new
            { x.TimeStart.Year, x.TimeStart.Month, x.TimeStart.Day, x.TimeStart.Hour });
        foreach (var grupp in dyrasteKvartPerTimme)
        {
            var dyrasteKvartar = grupp.OrderByDescending(x => x.SekPerKwh).Take(2);
            foreach (var dyrasteKvart in dyrasteKvartar)
            {
                dyrasteKvart.ChargingAllowed = false;
            }
        }

        // Beräkna laddbehov
        var behovProcent = await LaddBehov();
        if (behovProcent <= 1)
        {
            logger.LogInformation($"Ingen kvartlista: Charge full {behovProcent}");
            return;
        }

        // Kan behöva justering
        var antalKvartar = (int)(behovProcent * 2.1);


        _kvartlista = priser.Where(x => x.ChargingAllowed
                                        && x.TimeEnd > DateTime.Now)
            .OrderBy(x => x.SekPerKwh)
            .Take(antalKvartar)
            .ToList();
    }
}

enum ConnectionEnum
{
    Connected,
    Charging,
    ChargingPaused,
    Disabled,
    Unknown,
    ChargingFinished,
    SearchingForCommunication
}

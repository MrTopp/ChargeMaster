using System.Diagnostics.CodeAnalysis;
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
        var connectorStatus = ConnectionEnum.Unknown;

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogTrace("ChargeWorker tick at: {time}", DateTimeOffset.Now);
            WallboxMeterInfo? wstat = await _wallbox.GetMeterInfoAsync();
            DateTime nu = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                DateTime.Now.Hour, DateTime.Now.Minute, 0);

            // ***** Varje minut

            // ----- Kontrollera om bilen börjar ladda.
            var currentConnectorStatus = await GetConnectorStatusAsync();
            if (currentConnectorStatus != connectorStatus)
            {
                _logger.LogInformation($"Charge transition {connectorStatus}->{currentConnectorStatus}");
                connectorStatus = currentConnectorStatus;
                if (connectorStatus == ConnectionEnum.Charging)
                {
                    // Bilen har börjat ladda, skall den stoppas?
                    // Det kan hända när bilen kopplas in, då skall laddningen 
                    // stoppas om det inte är rätt tid för laddning
                    _logger.LogInformation("Car started charging, OK?");
                    await SkapaKvartlista();
                    int minutAvrundad = nu.Minute / 15 * 15;
                    if (!_kvartlista.Any(x =>
                            x.TimeStart.Hour == nu.Hour && x.TimeStart.Minute == minutAvrundad))
                    {
                        // Bilen skall stoppas
                        _logger.LogInformation("Stopped charging.");
                        await StoppaLaddningAsync(force: true);
                    }

                }
            }

            // ----- Kontrollera förväntad timförbrukning(nu -timstart) *60 / minuter_nu
            long förbrukningDennaTimme = wstat.AccEnergy - FörbrukningVidTimstart;
            if (förbrukningDennaTimme > 2500 && Timladdning)
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
                _logger.LogInformation("-- Quarter --");
                await SkapaKvartlista();
                int minutAvrundad = nu.Minute / 15 * 15;

                foreach (var price in _kvartlista.OrderBy(x => x.TimeStart).Take(5))
                {
                    _logger.LogInformation($"Kvart {price.TimeStart} - {price.TimeEnd} charge {price.ChargingAllowed}");
                }

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
                    var next = _kvartlista.OrderBy(x => x.TimeStart).FirstOrDefault();

                    _logger.LogInformation("Quarter, not starting, next {time}", next?.TimeStart.ToShortTimeString() ?? "---");
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

    internal async Task StoppaLaddningAsync(bool force = false)
    {
        if (bilenLaddar || force)
        {
            var behov = await LaddBehov();
            if (behov <= 1)
            {
                // Låt bilen bestämma
                _logger.LogInformation($"StoppaLaddning:LaddBehov{behov}");
                return;
            }

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

            default:
                _logger.LogError("Unknown value för WallboxStatus.Connector: {value}", response.Connector);
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
        var nu = DateTime.Now;
        var grans = new DateTime(nu.Year, nu.Month, nu.Day, nu.Hour, 0, 0);
        var priser = await context.ElectricityPrices
            .Where(x => x.TimeEnd >= grans
            //&& x.TimeStart < DateTime.Today.AddDays(1).AddHours(7)
                        )
            .OrderBy(x => x.TimeStart)
            .ToListAsync();

        // Sätt ChargingAllowed = false på den dyraste kvarten varje timme
        var dyrasteKvartPerTimme =
            priser.GroupBy(x => new { x.TimeStart.Year, x.TimeStart.Month, x.TimeStart.Day, x.TimeStart.Hour });
        foreach (var grupp in dyrasteKvartPerTimme)
        {
            var dyrasteKvartar = grupp.OrderByDescending(x => x.SekPerKwh).Take(2);
            foreach (var dyrasteKvart in dyrasteKvartar)
                dyrasteKvart?.ChargingAllowed = false;
        }

        // Beräkna laddbehov
        var behovProcent = await LaddBehov();
        if (behovProcent <= 1)
        {
            _logger.LogInformation($"Ingen kvartlista: Charge full {behovProcent}");
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
    ChargingFinished
}

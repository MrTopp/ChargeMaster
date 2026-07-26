using ChargeMaster.Data;
using Microsoft.EntityFrameworkCore;
using ChargeMaster.Services.Wallbox;
using ChargeMaster.Services.TibberVehicle;

namespace ChargeMaster.Workers;

public class KvartlistaEventArgs(List<ElectricityPrice> kvartlista) : EventArgs
{
    public List<ElectricityPrice> Kvartlista { get; } = kvartlista;
}

/// <summary>
/// Övervakar och styr laddning av bilen baserat på elpriser, timförbrukning och bilens status.
/// </summary>
public class ChargeWorker(
    IServiceScopeFactory serviceScopeFactory,
    WallboxService wallboxService,
    TibberVehicleService tibberVehicleService,
    WallboxWorker wallboxWorker,
    DaikinWorker daikinWorker,
    ILogger<ChargeWorker> logger)
    : BackgroundService
{
    /// <summary>
    /// Event som utlöses när Kvartlistan är uppdaterad. För den som har
    /// bråttom kan man hämta den med GetKvartlista()
    /// </summary>
    public event EventHandler<KvartlistaEventArgs>? KvartlistaUpdated;

    /// <summary>
    /// Skillnad mellan nuvarande batterinivå och mål för laddning, i procent.
    /// </summary>
    private double LaddBehovProcent
    {
        get
        {
            if (VehicleStatus?.BatteryLevel == null || VehicleStatus?.ChargingSettingsTargetLevel == null)
                return 0;
            return VehicleStatus.ChargingSettingsTargetLevel.Value - VehicleStatus.BatteryLevel.Value;
        }
    }

    private TibberVehicleStatus? VehicleStatus { get; set; }

    /// <summary>
    /// Tracks the last saved charge session data to avoid saving duplicates.
    /// Used to detect changes in ChargeLevel and SessionEnergy.
    /// </summary>
    private ChargeSession? LastSavedChargeSession { get; set; }

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
                // Förväntat när tjänsten stoppas, ingen åtgärd krävs.
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
        DateTime previous = DateTime.Now;

        while (!wallboxWorker.WallboxInitierad)
            await Task.Delay(100, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            DateTime dt = DateTime.Now;
            DateTime nu = new DateTime(dt.Year, dt.Month, dt.Day,
                dt.Hour, dt.Minute, 0);
            var currentConnectorStatus = await wallboxService.GetConnectorStatusAsync();

            // ----- Varje timme
            if (nu.Hour != previous.Hour)
            {
                logger.LogInformation("** Hourly consumption: {Consumption} Wh **",
                    wallboxWorker.FörbrukningFöregåendeTimme);
            }

            // ----- Effektvakt värmepump
            await daikinWorker.KontrolleraEffekt(wallboxWorker.FörbrukningDennaTimme, nu,
                stoppingToken);

            // ----- Bilens status

            VehicleStatus = await tibberVehicleService.GetStatusAsync();

            // ----- Kvartlista, tom om bilen inte är ansluten
            GetKvartlista(tom: currentConnectorStatus == ConnectionEnum.SearchingForCommunication);

            await SaveChargeSessionAsync(currentConnectorStatus.ToString(), stoppingToken);


            // ----- Om bilen inte är ansluten, hoppa över resten av loopen

            if (currentConnectorStatus == ConnectionEnum.SearchingForCommunication)
            {
                goto NextIteration;
            }


            // ----- Start/Stoppa laddning -----

            bool chargingAllowed = await IsChargingAllowedAsync();
            if (!chargingAllowed)
            {
                await wallboxService.StoppaLaddningAsync();
            }
            else
            {
                await wallboxService.StartaLaddningAsync();
            }

        NextIteration:

            // ----- Vänta tills nästa hela minut
            var targetNextMinute = nu.AddMinutes(1);
            while (DateTime.Now < targetNextMinute && !stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(100, stoppingToken);
            }

            previous = nu;
            // Tvinga uppdatering av kvartlista varje varv
            _kvartlista = null;
        }
    }

    /// <summary>
    /// Räknar ut behov av laddning i procent
    /// </summary>
    /// <returns>laddbehov i procent</returns>
    private async Task<(int, int)> VehicleStateAsync()
    {
        // Beräkna laddbehov
        TibberVehicleStatus? status;
        try
        {
            status = await tibberVehicleService.GetStatusAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching vehicle status: {Message}", ex.Message);
            return (0, 0);
        }

        if (status?.BatteryLevel == null)
            return (0, 0);

        var chargeLevelCurrent = (int)Math.Floor(status.BatteryLevel ?? 0);
        var chargeLevelTarget = (int)Math.Floor(status.ChargingSettingsTargetLevel ?? 0);

        return (chargeLevelCurrent, chargeLevelTarget);
    }

    /// <summary>
    /// ! Använd GetKvartlista() i stället!
    /// </summary>
    private List<ElectricityPrice>? _kvartlista;

    private readonly Lock _kvartlistaLock = new();

    private async Task<bool> IsChargingAllowedAsync()
    {
        // Kontrollera om nuvarande kvart finns i kvartlistan
        var nu = DateTime.Now;
        int minutAvrundad = nu.Minute / 15 * 15;
        var kvartlista = GetKvartlista();
        bool allowed = kvartlista.Any(x =>
            x.TimeStart.Day == nu.Day &&
            x.TimeStart.Hour == nu.Hour &&
            x.TimeStart.Minute == minutAvrundad);
        if (!allowed)
        {
            return false;
        }

        // Kontrollera om timförbrukningen är över gränsen
        if (nu.Minute > 10)
        {
            var minuterKvar = 60 - nu.Minute;

            var förbrukningKvar = minuterKvar * 8000 / 60;
            var totalförbrukningTimme
                = wallboxWorker.FörbrukningDennaTimme + förbrukningKvar;

            HourlyEnergyUsage maxFörbrukning
                = await wallboxWorker.GetHighestHourlyEnergyUsageDaytimeAsync(nu);
            var förbrukningGräns = (long)(maxFörbrukning.EnergyUsageWh * 0.9);
            if (förbrukningGräns < 4000)
            {
                förbrukningGräns = 4000;
            }

            if (totalförbrukningTimme > förbrukningGräns)
            {
                logger.LogInformation(
                    "! Charging disabled due to high consumption: {consumption} Wh.",
                    wallboxWorker.FörbrukningDennaTimme);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Skapa lista med kvartar där laddning skall vara aktiv
    /// </summary>
    public List<ElectricityPrice> GetKvartlista(bool tom = false)
    {
        lock (_kvartlistaLock)
        {
            var kvartlista = new List<ElectricityPrice>();
            if (tom || LaddBehovProcent < 1)
            {
                // LaddBehovProcent är oinitierat eller bilen fulladdad
                KvartlistaUpdated?.Invoke(this, new KvartlistaEventArgs(kvartlista));
                _kvartlista = kvartlista;
                return _kvartlista;
            }

            // _kvartlista skapas en gång per varv i loopen, sätts till null i slutet av varje varv.
            if (_kvartlista is { Count: > 0 })
                return _kvartlista;

            using var scope = serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nu = DateTime.Now;
            var grans = new DateTime(nu.Year, nu.Month, nu.Day, nu.Hour, 0, 0);
            var priser = context.ElectricityPrices
                .Where(x => x.TimeEnd >= grans
                            // aldrig vardagar 7-19 november till mars
                            && !((x.TimeStart.Month >= 11 || x.TimeStart.Month <= 3) &&
                                 x.TimeStart.Hour >= 7 && x.TimeStart.Hour < 19 &&
                                 x.TimeStart.DayOfWeek != DayOfWeek.Saturday &&
                                 x.TimeStart.DayOfWeek != DayOfWeek.Sunday)
                )
                .OrderBy(x => x.TimeStart)
                .ToList();

            // Sätt ChargingAllowed = false på de två dyraste kvartarna varje timme
            //var dyrasteKvartPerTimme =
            //    priser.GroupBy(x => new
            //    { x.TimeStart.Year, x.TimeStart.Month, x.TimeStart.Day, x.TimeStart.Hour });
            //foreach (var grupp in dyrasteKvartPerTimme)
            //{
            //    var dyrasteKvartar = grupp.OrderByDescending(x => x.SekPerKwh).Take(2);
            //    foreach (var dyrasteKvart in dyrasteKvartar)
            //    {
            //        dyrasteKvart.ChargingAllowed = false;
            //    }
            //}

            // Ta bort kvart(ar) i början av varje timme.
            foreach (var kvart in priser)
            {
                if (kvart.TimeStart.Minute < 10)
                {
                    kvart.ChargingAllowed = false;
                }
            }

            // Antar att det behövs 1.0 kvartar per procent laddbehov.
            var antalKvartar = (int)(LaddBehovProcent * 1.0);

            // Skapa lista med kvartar där laddning är tillåten och priset är under prisTak
            kvartlista = priser.Where(x => x.ChargingAllowed
                                           && x.TimeEnd > DateTime.Now)
                .OrderBy(x => x.SekPerKwh)
                .Take(antalKvartar)
                .ToList();

            var nextKvart = kvartlista.OrderBy(x => x.TimeStart).FirstOrDefault();
            logger.LogDebug(
                "Laddbehov {behovProcent}, kvartar {antalKvartar} nästa {nextKvart}",
                LaddBehovProcent, antalKvartar, nextKvart?.TimeStart.ToString("HH:mm") ?? "---");

            KvartlistaUpdated?.Invoke(this, new KvartlistaEventArgs(kvartlista));

            _kvartlista = kvartlista;
            return _kvartlista;
        }
    }

    /// <summary>
    /// Saves the current charge session data to the database if it differs from the previous save.
    /// Compares ChargeLevel and SessionEnergy to detect changes.
    /// </summary>
    /// <param name="chargeState">The current state of charging (e.g., "CHARGING", "IDLE").</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    internal async Task SaveChargeSessionAsync(
        string chargeState,
        CancellationToken cancellationToken)
    {
        try
        {
            var (chargeLevel, chargeTarget) = await VehicleStateAsync();

            // Get session data from WallboxWorker
            var sessionData = wallboxWorker.ChargeSessionData;
            if (sessionData is null)
            {
                return;
            }

            if (!sessionData.HasData)
            {
                // gissar att det inte finns någon inkopplad bil, eller den har inte laddat något.
                //logger.LogInformation("SaveChargeSessionAsync: Incomplete session data. Skipping save.");
                return;
            }

            // Initialize _lastSavedChargeSession from database if it's null
            if (LastSavedChargeSession is null)
            {
                try
                {
                    using var initScope = serviceScopeFactory.CreateScope();
                    var initContext = initScope.ServiceProvider
                        .GetRequiredService<ApplicationDbContext>();
                    LastSavedChargeSession = await initContext.ChargeSessions
                        .OrderByDescending(x => x.Timestamp)
                        .FirstOrDefaultAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "SaveChargeSessionAsync: Error initializing _lastSavedChargeSession from database");
                }

                // fallback om databasen är tom eller det blev något fel vid inläsning
                LastSavedChargeSession ??= new ChargeSession();
            }

            // Check if data has changed compared to last save
            var sessionEnergy = sessionData.AccSessionEnergy;
            if (LastSavedChargeSession.ChargeLevel == chargeLevel &&
                LastSavedChargeSession.SessionEnergy == sessionEnergy)
            {
                logger.LogDebug(
                    "SaveChargeSessionAsync: No change detected. ChargeLevel={level}, SessionEnergy={energy}",
                    chargeLevel, sessionEnergy);
                return;
            }


            // Create new charge session record
            var chargeSession = new ChargeSession
            {
                Timestamp = DateTime.Now,
                ChargeState = chargeState,
                ChargeLevel = chargeLevel,
                ChargeTarget = chargeTarget,
                SessionEnergy = sessionEnergy,
                SessionStartValue = sessionData.SessionStartValue,
                SessionStartTime = sessionData.SessionStartTime ?? 0
            };


            // Save to database
            using var scope = serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.ChargeSessions.Add(chargeSession);
            await context.SaveChangesAsync(cancellationToken);

            // Update last saved data
            LastSavedChargeSession = chargeSession;

            logger.LogInformation(
                "SaveChargeSessionAsync: Charge session saved. State={state}, Level={level}%, Target={target}%, Energy={energy}Wh",
                chargeState, chargeLevel, chargeTarget, sessionEnergy);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SaveChargeSessionAsync: Error saving charge session");
        }
    }
}
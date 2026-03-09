using ChargeMaster.Data;
using Microsoft.EntityFrameworkCore;
using ChargeMaster.Services.Wallbox;
using ChargeMaster.Services.VolksWagen;

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
    VWService vwService,
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
    /// Flagga om laddning är tillåten denna timme, sätts till false
    /// om förbrukningen innevarande timme är över tillåten nivå.
    /// </summary>
    private bool Timladdning { get; set; }

    /// <summary>
    /// Nuvarande laddnivå i bilen, i procent. 
    /// </summary>
    private double _chargeLevelCurrent;
    /// <summary>
    /// Målvärde för laddningen i procent
    /// </summary>
    private double _chargeLevelTarget;

    /// <summary>
    /// Flagga att wallbox är panikstoppad.
    /// </summary>
    private bool WallboxStopped
    {
        get;
        set
        {
            if (value != field)
            {
                field = value;
                logger.LogInformation("WallboxStopped.set: WallboxStopped set to {value}", value);
            }
        }
    } = false;

    /// <summary>
    /// Skillnad mellan nuvarande batterinivå och mål för laddning, i procent.
    /// </summary>
    public double LaddBehovProcent { get; private set; }

    /// <summary>
    /// Status för laddningen
    /// </summary>
    private ConnectionEnum ConnectorStatus
    {
        get;
        set
        {
            if (value != field)
            {
                field = value;
                // Logga klockslag för state-övergången
                ConnectorStatusTime = DateTime.Now;
            }
        }
    } = ConnectionEnum.Unknown;

    private DateTime ConnectorStatusTime { get; set; } = DateTime.Now;


    /// <summary>
    /// Tracks the last saved charge session data to avoid saving duplicates.
    /// Used to detect changes in ChargeLevel and SessionEnergy.
    /// </summary>
    private ChargeSession? _lastSavedChargeSession;

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
                logger.LogInformation("ChargeWorker is stopping due to cancellation.");
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
        Timladdning = true;
        BilenLaddar = await LaddStatus();
        var currentConnectorStatus = await GetConnectorStatusAsync();
        if (currentConnectorStatus == ConnectionEnum.Disabled)
        {
            logger.LogInformation("Wallbox is disabled.");
            WallboxStopped = true;
        }
        LaddBehovProcent = await LaddBehov();
        while(!wallboxWorker.WallboxInitierad)
            await Task.Delay(100, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogDebug("ChargeWorker tick at: {time}", DateTimeOffset.Now);
            DateTime dt = DateTime.Now;
            DateTime nu = new DateTime(dt.Year, dt.Month, dt.Day,
                dt.Hour, dt.Minute, 0);
            currentConnectorStatus = await GetConnectorStatusAsync();

            // ----- Varje timme
            if (nu.Hour != previous.Hour)
            {
                logger.LogInformation("** Hourly consumption: {consumption} Wh **",
                    wallboxWorker.FörbrukningFöregåendeTimme);
                Timladdning = true;
            }

            // ----- Effektvakt värmepump
            await daikinWorker.KontrolleraEffekt(wallboxWorker.FörbrukningTotalDennaTimme, nu);

            // ----- Bilen inte ansluten, hoppa över utvärdering av laddning
            if (currentConnectorStatus == ConnectionEnum.SearchingForCommunication)
            {
                goto NextIteration; // Hoppa till avslutande paus.
            }

            // ----- Bilen är hemma, dags att utvärdera laddning -----

            await SaveChargeSessionAsync(currentConnectorStatus.ToString(),
                (int)_chargeLevelCurrent,
                (int)_chargeLevelTarget,
                wallboxWorker,
                stoppingToken);

            // ----- Nödstopp om bilen laddar när det inte är tillåtet
            if (ConnectorStatus == ConnectionEnum.Charging && !BilenLaddar)
            {
                int minutAvrundad = nu.Minute / 15 * 15;
                var kvartlista = GetKvartlista();
                if (!kvartlista.Any(x =>
                        x.TimeStart.Day == nu.Day &&
                        x.TimeStart.Hour == nu.Hour &&
                        x.TimeStart.Minute == minutAvrundad))
                {
                    logger.LogInformation(
                        "Illegal charging, stop car and wallbox. {ConnectorStatusTime}",
                        ConnectorStatusTime);
                    await StoppaLaddningAsync(force: true);
                    await StopWallbox();
                }
            }

            // ----- State-övergång
            if (currentConnectorStatus != ConnectorStatus)
            {
                logger.LogInformation(
                    $"Charge transition {ConnectorStatus}->{currentConnectorStatus}");

                // ----- Bilen börjar ladda.
                if (currentConnectorStatus == ConnectionEnum.Charging)
                {
                    // Bilen har börjat ladda, skall den stoppas?
                    // Det kan hända när bilen kopplas in, då skall laddningen 
                    // stoppas om det inte är rätt tid för laddning
                    logger.LogDebug("Car started charging");
                    int minutAvrundad = nu.Minute / 15 * 15;
                    var kvartlista = GetKvartlista();
                    if (kvartlista.Any(x =>
                            x.TimeStart.Day == nu.Day &&
                            x.TimeStart.Hour == nu.Hour &&
                            x.TimeStart.Minute == minutAvrundad))
                    {
                        logger.LogDebug("Charging allowed, continue.");
                        BilenLaddar = true;
                    }
                    else
                    {
                        logger.LogInformation("Charging not allowed, stop charging.");
                        await StoppaLaddningAsync(force: true);
                    }
                }
                ConnectorStatus = currentConnectorStatus;
            }

            // ----- Kontrollera förväntad timförbrukning
            if (Timladdning)
            {
                // TODO: kalkylera gränsen exclusive bilens laddade effekt
                // kräver att vi räknar ut bilens laddade effekt i realtid.
                var grans = await wallboxWorker.KalkyleraGrans((DateTime)nu);
                if (nu.Minute < 30) grans = int.MaxValue;
                if (wallboxWorker.FörbrukningDennaTimme > grans)
                {
                    //  För hög förbrukning -> stoppa laddning
                    logger.LogInformation(
                        "! Charging disabled due to high consumption: {consumption} Wh.",
                        wallboxWorker.FörbrukningDennaTimme);
                    Timladdning = false;
                    await StoppaLaddningAsync();
                }
            }

            // ***** Varje kvart
            if (nu.Minute % 15 == 0 && nu.Minute != previous.Minute)
            {
                LaddBehovProcent = await LaddBehov();
                // Starta/stoppa laddning beroende på om det är tillåtet eller inte
                if (wallboxWorker.FörbrukningDennaTimme > 0)
                    logger.LogInformation("-- Quarter, consumption: {consumption} Wh --",
                        wallboxWorker.FörbrukningDennaTimme);
                if (Timladdning)
                {
                    int numin = nu.Minute;
                    int minutAvrundad = numin / 15 * 15;
                    var kvartlista = GetKvartlista();

                    // Om 'nu' finns i listan med kvartar 
                    if (kvartlista.Any(x =>
                            x.TimeStart.Day == nu.Day &&
                            x.TimeStart.Hour == nu.Hour &&
                            x.TimeStart.Minute == minutAvrundad))
                    {
                        logger.LogInformation("Quarter, start charge");
                        await StartaLaddningAsync();
                    }
                    else
                    {
                        var next = kvartlista.OrderBy(x => x.TimeStart).FirstOrDefault();
                        logger.LogInformation("Quarter, not starting, next {time}",
                            next?.TimeStart.ToShortTimeString() ?? "---");
                        await StoppaLaddningAsync();
                    }
                }
            }

        NextIteration:
            // Vänta tills nästa hela minut
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




    private bool BilenLaddar { get; set; }

    internal async Task StartaLaddningAsync()
    {
        if (Timladdning && !BilenLaddar)
        {
            try
            {
                var wallboxStatus = await GetConnectorStatusAsync();
                if (wallboxStatus == ConnectionEnum.Disabled)
                {
                    await StartWallbox();
                }

                BilenLaddar = await vwService.StartChargingAsync();
            }
            catch (CarConnectionException ex)
            {
                logger.LogError(ex, "Error starting charging");
            }

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
            bool success;
            try
            {
                logger.LogInformation("StoppaLaddningAsync: stopping car charging");
                success = await vwService.StopChargingAsync();
                if (!success)
                {
                    logger.LogError(
                        "StoppaLaddningAsync: failed to stop car charging, stopping wallbox");
                    await StopWallbox();
                }
            }
            catch (CarConnectionException ex)
            {
                logger.LogError(ex, "Error stopping charging");
                await StopWallbox(); // Om det inte går att stänga av genom att fråga bilen, stäng av wallboxen så att bilen inte kan ladda.
            }

            BilenLaddar = false;
        }
    }

    internal async Task<bool> LaddStatus()
    {
        try
        {
            VWStatus? response = await vwService.GetStatus();
            _chargeLevelCurrent = response?.BatteryLevel ?? 0;
            _chargeLevelTarget = response?.ChargingSettingsTargetLevel ?? 0;
            return (response?.ChargingPower ?? 0) > 0;
        }
        catch (CarConnectionException ex)
        {
            logger.LogError(ex, "Error fetching VW status");
            await StopWallbox();
            return false;
        }
    }

    /// <summary>
    /// Stäng av laddning genom att sätta wallboxen i NotAvailable-läge, används när kopplingen till bilen krånglar. Då kan bilen inte ladda.
    /// </summary>
    /// <returns></returns>
    internal async Task StopWallbox()
    {
        // Stoppa laddning genom att sätta wallboxen i NotAvailable-läge
        try
        {
            logger.LogInformation("StopWallbox:");
            await wallboxService.SetModeAsync(WallboxMode.NotAvailable);
            WallboxStopped = true;
            BilenLaddar = false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting wallbox mode to NotAvailable");
        }
    }

    internal async Task<bool> StartWallbox()
    {
        // Tillåt laddning genom att sätta wallboxen i Normal-läge
        try
        {
            logger.LogInformation("StartWallbox: ");
            await wallboxService.SetModeAsync(WallboxMode.Available);
            WallboxStopped = false;
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error setting wallbox mode to Available");
            return false;
        }
    }

    /// <summary>
    /// Status för laddning, används för att avgöra om bilen är inkopplad, laddar,
    /// eller inte är ansluten. Om det inte går att få status från wallboxen, returneras Unknown.
    /// </summary>
    /// <returns></returns>
    internal async Task<ConnectionEnum> GetConnectorStatusAsync()
    {
        WallboxStatus? response = await wallboxService.GetStatusAsync();
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
    /// <returns>laddbehov i procent</returns>
    internal async Task<double> LaddBehov()
    {
        // Beräkna laddbehov
        VWStatus? status;
        try
        {
            status = await vwService.GetStatus();
        }
        catch (CarConnectionException ex)
        {
            logger.LogError(ex, "Error fetching VW status");
            await StopWallbox();
            return 0;
        }

        if (status == null)
            return 0;
        if (status.BatteryLevel == null)
            return 0;
        _chargeLevelCurrent = status.BatteryLevel ?? 0;
        _chargeLevelTarget = status.ChargingSettingsTargetLevel ?? 0;

        return _chargeLevelTarget - _chargeLevelCurrent;
    }

    /// <summary>
    /// ! Använd GetKvartlista() i stället!
    /// </summary>
    private List<ElectricityPrice>? _kvartlista;

    private object _kvartlistaLock = new object();

    /// <summary>
    /// Skapa lista med kvartar där laddning skall vara aktiv
    /// </summary>
    public List<ElectricityPrice> GetKvartlista()
    {
        lock (_kvartlistaLock)
        {
            var kvartlista = new List<ElectricityPrice>();
            if (LaddBehovProcent < 1)
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

            // Antar att det behövs 2.4 kvartar per procent laddbehov.
            var antalKvartar = (int)(LaddBehovProcent * 2.4);

            kvartlista = priser.Where(x => x.ChargingAllowed
                                           && x.TimeEnd > DateTime.Now)
                .OrderBy(x => x.SekPerKwh)
                .Take(antalKvartar)
                .ToList();

            var nextKvart = kvartlista.OrderBy(x => x.TimeStart).FirstOrDefault();
            logger.LogInformation(
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
    /// <param name="chargeLevel">The current battery charge level in percentage (0-100).</param>
    /// <param name="chargeTarget">The target charge level in percentage (0-100).</param>
    /// <param name="wallboxWorker">The WallboxWorker instance to get session data from.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    internal async Task SaveChargeSessionAsync(
        string chargeState,
        int? chargeLevel,
        int? chargeTarget,
        WallboxWorker wallboxWorker,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get session data from WallboxWorker
            var sessionData = wallboxWorker.ChargeSessionData;
            if (sessionData is null)
            {
                logger.LogInformation("SaveChargeSessionAsync: No session data available from WallboxWorker");
                return;
            }

            if (!sessionData.HasData)
            {
                // gissar att det inte finns någon inkopplad bil, eller den har inte laddat något.
                logger.LogInformation("SaveChargeSessionAsync: Incomplete session data. Skipping save.");
                return;
            }

            // Initialize _lastSavedChargeSession from database if it's null
            if (_lastSavedChargeSession is null)
            {
                try
                {
                    using var initScope = serviceScopeFactory.CreateScope();
                    var initContext = initScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    _lastSavedChargeSession = await initContext.ChargeSessions
                        .OrderByDescending(x => x.Timestamp)
                        .FirstOrDefaultAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "SaveChargeSessionAsync: Error initializing _lastSavedChargeSession from database");
                }
                // fallback om databasen är tom eller det blev något fel vid inläsning
                _lastSavedChargeSession ??= new ChargeSession();
            }

            // Check if data has changed compared to last save
            var sessionEnergy = sessionData.AccSessionEnergy;
            if (_lastSavedChargeSession.ChargeLevel == chargeLevel &&
                _lastSavedChargeSession.SessionEnergy == sessionEnergy)
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
            _lastSavedChargeSession = chargeSession;

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

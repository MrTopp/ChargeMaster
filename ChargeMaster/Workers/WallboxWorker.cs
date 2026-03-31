using ChargeMaster.Data;
using ChargeMaster.Services.InfluxDB;
using ChargeMaster.Services.Wallbox;

using Microsoft.EntityFrameworkCore;

namespace ChargeMaster.Workers;

/// <summary>
/// Event arguments containing Wallbox meter information.
/// </summary>
public class MeterInfoEventArgs : EventArgs
{
    /// <summary>
    /// Gets the meter information.
    /// </summary>
    public WallboxMeterInfo MeterInfo { get; }

    public DateTime Timestamp { get; } = DateTime.Now;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeterInfoEventArgs"/> class.
    /// </summary>
    /// <param name="meterInfo">The meter information to include in the event.</param>
    public MeterInfoEventArgs(WallboxMeterInfo meterInfo)
    {
        MeterInfo = meterInfo;
    }
}

/// <summary>
/// Background service responsible for managing the Wallbox charger.
/// Handles tasks such as status monitoring, time synchronization, schedule enforcement,
/// and recording energy consumption data.
/// </summary>
public class WallboxWorker(
    IServiceScopeFactory serviceScopeFactory,
    WallboxService wallboxService,
    InfluxDbService influxDbService,
    ILogger<WallboxWorker> logger) : BackgroundService
{
    /// <summary>
    /// Event raised when meter information is calculated and ready for consumption.
    /// </summary>
    public event EventHandler<MeterInfoEventArgs>? MeterInfoCalculated;

    /// <summary>
    /// För extern konsumtion
    /// </summary>
    public WallboxMeterInfo? MeterInfo { get; private set; }

    /// <summary>
    /// Event raised when a new charging session starts (SessionStartTime changes).
    /// </summary>
    public event EventHandler<ChargeSessionData>? ChargeSessionChanged;

    /// <summary>
    /// Uppräknad förbrukning innevarande timme.
    /// </summary>
    public long FörbrukningDennaTimme { get; private set; }

    /// <summary>
    /// Uppskattad total förbrukning innevarande timme.
    /// </summary>
    public long FörbrukningTotalDennaTimme { get; private set; }

    /// <summary>
    /// Flagga att initieringen av förbrukningsberäkning är klar.
    /// </summary>
    public bool WallboxInitierad { get; private set; } = false;

    /// <summary>
    /// Förbrukning föregående timme
    /// </summary>
    public long FörbrukningFöregåendeTimme
    {
        get
        {
            if (NästSistaMeterInfoFöregåendeTimme != null && SistaMeterInfoFöregåendeTimme != null)
            {
                return SistaMeterInfoFöregåendeTimme.AccEnergy -
                       NästSistaMeterInfoFöregåendeTimme.AccEnergy;
            }
            return 0;
        }
    }

    /// <summary>
    /// Senaste läsningen från wallbox. Uppdateras endast vid förändring i ackumulerad energi.
    /// </summary>
    internal WallboxMeterInfo? NuvarandeMeterInfo { get; set; }

    /// <summary>
    /// Föregående mätning med skillnad i ackumulerad energi från senaste mätningen. 
    /// </summary> 
    internal WallboxMeterInfo? FöregåendeMeterInfo { get; set; }

    /// <summary>
    /// Sista mätning föregående timme. 
    /// </summary>
    internal WallboxMeterInfo? SistaMeterInfoFöregåendeTimme { get; set; }

    /// <summary>
    /// Sista mätarställningen timmen före föregående timme
    /// </summary>
    internal WallboxMeterInfo? NästSistaMeterInfoFöregåendeTimme { get; set; }

    /// <summary>
    /// Cache for hourly energy usage calculations. Stores data and timestamp of last calculation.
    /// </summary>
    private DateTime LastHourlyEnergyUsageCacheTime { get; set; } = DateTime.MinValue;
    private List<HourlyEnergyUsage> HourlyEnergyUsageCache { get; set; } = [];
    private object CacheLocker { get; } = new();

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await WallboxLoop(stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Förväntat när tjänsten stoppas, ingen åtgärd krävs.
                logger.LogInformation("WallboxWorker is stopping due to cancellation request.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in WallboxMeterWorker loop");
                await Task.Delay(TimeSpan.FromSeconds(60 * 10), stoppingToken);
            }
        }
    }

    /// <summary>
    /// The main operational loop that runs periodically to perform charger maintenance and data collection.
    /// </summary>
    /// <param name="stoppingToken">Token to monitor for cancellation requests.</param>
    internal async Task WallboxLoop(CancellationToken stoppingToken)
    {
        await InitieraFörbrukningAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Initiera genom att läsa upp status
            WallboxStatus wallboxStatus = await InitializeWallboxStatusAsync(stoppingToken);

            // Extrahera status för aktuell laddning
            ExtractChargeData(wallboxStatus);

            // Kontrollera klockan på wallbox
            await CheckWallboxTimeAsync(wallboxStatus);

            // Räkna ut nuvarande effekt
            var currentMeterInfo = await ReadEnergyAsync(stoppingToken, DateTime.Now);

            // Sätt förbrukningsvärden innan objektet exponeras för konsumenter
            if (currentMeterInfo != null)
            {
                currentMeterInfo.EffektTimmeNu = FörbrukningDennaTimme;
                currentMeterInfo.EffektTimmeTotal = FörbrukningTotalDennaTimme;
            }

            // Exponera fullt initierat objekt via property
            MeterInfo = currentMeterInfo;

            // Uppdatera InfluxDB
            if (currentMeterInfo != null)
            {
                await influxDbService.WriteWallboxMeterInfoAsync(currentMeterInfo);
            }

            // Posta mätarinfo
            PostMeterInfo(currentMeterInfo);

            // vänta innan nästa iteration
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    /// <summary>
    /// Gets the current charging session data extracted from the Wallbox status.
    /// </summary>
    public ChargeSessionData? ChargeSessionData { get; private set; }

    /// <summary>
    /// Posta mätarinfo till prenumeranter på MeterInfoCalculated eventet.
    /// Objektet ska vara fullt initierat (inklusive EffektTimmeNu/EffektTimmeTotal) innan anrop.
    /// </summary>
    private void PostMeterInfo(WallboxMeterInfo? meterInfo)
    {
        if (meterInfo == null)
            return;
        MeterInfoCalculated?.Invoke(this, new MeterInfoEventArgs(meterInfo));
    }

    /// <summary>
    /// Hämta upp den senaste mätarställningen före innevarande timme
    /// </summary>
    internal async Task InitieraFörbrukningAsync(CancellationToken cancellationToken, DateTime? testNu = null)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        DateTime nu = testNu ?? DateTime.Now;
        var startOfHour = new DateTime(nu.Year, nu.Month, nu.Day, nu.Hour, 0, 0);

        // Hämta sista mätningen föregående timme.
        var hour = startOfHour;
        var before = await context.WallboxMeterReadings
            .Where(x => x.ReadAt <= hour)
            .OrderByDescending(x => x.ReadAt)
            .FirstOrDefaultAsync(cancellationToken);

        SistaMeterInfoFöregåendeTimme = new WallboxMeterInfo
        {
            AccEnergy = before?.AccEnergy ?? 0,
            ReadDateTime = before?.ReadAt ?? DateTime.Now
        };

        // Hämta sista mätarställningen 2 timmar bakåt
        var now2 = SistaMeterInfoFöregåendeTimme.ReadDateTime.AddMinutes(-1);
        var startOfHour2 = new DateTime(now2.Year, now2.Month, now2.Day, now2.Hour, 0, 0);

        var before2 = await context.WallboxMeterReadings
            .Where(x => x.ReadAt <= startOfHour2)
            .OrderByDescending(x => x.ReadAt)
            .FirstOrDefaultAsync(cancellationToken);

        NästSistaMeterInfoFöregåendeTimme = new WallboxMeterInfo
        {
            AccEnergy = before2?.AccEnergy ?? 0,
            ReadDateTime = before2?.ReadAt ?? DateTime.Now
        };

        // Hämta upp sista mätningen 
        var last = await context.WallboxMeterReadings
            .OrderByDescending(x => x.ReadAt)
            .FirstOrDefaultAsync(cancellationToken);
        NuvarandeMeterInfo = new WallboxMeterInfo
        {
            AccEnergy = last?.AccEnergy ?? 0,
            ReadDateTime = last?.ReadAt ?? DateTime.Now
        };
        // hämta upp näst sista mätningen 
        var previous = await context.WallboxMeterReadings
            .Where(x => x.ReadAt < NuvarandeMeterInfo.ReadDateTime)
            .OrderByDescending(x => x.ReadAt)
            .FirstOrDefaultAsync(cancellationToken);
        FöregåendeMeterInfo = new WallboxMeterInfo
        {
            AccEnergy = previous?.AccEnergy ?? 0,
            ReadDateTime = previous?.ReadAt ?? DateTime.Now
        };

        KalkyleraFörbrukningInnevarandeTimme(nu);
        // Släpp in patrasket
        WallboxInitierad = true;
    }

    /// <summary>
    /// Extracts charging session data from the Wallbox status and updates the ChargeSessionData property.
    /// Raises the ChargeSessionChanged event if SessionStartTime differs from the previous reading.
    /// </summary>
    /// <param name="wallboxStatus">The Wallbox status containing session information.</param>
    private void ExtractChargeData(WallboxStatus wallboxStatus)
    {
        if (wallboxStatus.MainCharger is null)
        {
            ChargeSessionData = null;
            return;
        }

        // Check if SessionStartTime has changed, indicating a new session
        if (ChargeSessionData?.SessionStartTime != wallboxStatus.MainCharger.SessionStartTime)
        {
            // Raise the event with the previous session data
            if (ChargeSessionData != null) ChargeSessionChanged?.Invoke(this, ChargeSessionData);
        }

        ChargeSessionData = new ChargeSessionData(
            wallboxStatus.MainCharger.AccSessionEnergy,
            wallboxStatus.MainCharger.SessionStartValue,
            wallboxStatus.MainCharger.AccSessionMillis,
            wallboxStatus.MainCharger.SessionStartTime
        );
    }

    /// <summary>
    /// Initializes communications by retrieving the Wallbox status.
    /// Retries indefinitely until a valid status is received.
    /// </summary>
    /// <param name="stoppingToken">Token to monitor for cancellation requests.</param>
    /// <returns>The initial <see cref="WallboxStatus"/>.</returns>
    internal async Task<WallboxStatus> InitializeWallboxStatusAsync(CancellationToken stoppingToken)
    {
        WallboxStatus? wallboxStatus = await wallboxService.GetStatusAsync();
        while (wallboxStatus is null)
        {
            logger.LogWarning("Wallbox status is null, retrying in 1 minute");
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            wallboxStatus = await wallboxService.GetStatusAsync();
        }

        return wallboxStatus;
    }

    /// <summary>
    /// Checks that the Wallbox clock is synchronized with the server time and updates it if the drift exceeds 5 minutes.
    /// </summary>
    /// <param name="wallboxStatus">The current status object containing the Wallbox time.</param>
    internal async Task CheckWallboxTimeAsync(WallboxStatus wallboxStatus)
    {
        // Wallbox tid i format HH:mm
        string? wallboxTime = wallboxStatus.ChargeboxTime;
        if (wallboxTime is null) return;
        // Kontrollera om klockan är felaktig
        if (DateTime.TryParseExact(wallboxTime, "HH:mm", null,
                System.Globalization.DateTimeStyles.None, out DateTime wallboxDateTime))
        {
            DateTime now = DateTime.Now;
            DateTime correctWallboxTime = new DateTime(now.Year, now.Month, now.Day,
                wallboxDateTime.Hour, wallboxDateTime.Minute, 0);
            TimeSpan timeDifference = now - correctWallboxTime;
            if (timeDifference.Duration() > TimeSpan.FromMinutes(5))
            {
                logger.LogInformation("Wallbox time is incorrect by {Difference}. Updating time.",
                    timeDifference);
                await wallboxService.SetTimeAsync(now);
            }
        }
    }

    /// <summary>
    /// Bearbetar senaste läsningen från wallbox
    /// </summary>
    /// <param name="stoppingToken">Token to monitor for cancellation requests.</param>
    /// <param name="nu">Datum för beräkning, normalt DateTime.Now</param>
    /// <returns>The meter information that was read, or null if no information was available.</returns>
    internal async Task<WallboxMeterInfo?> ReadEnergyAsync(CancellationToken stoppingToken, DateTime nu)
    {
        try
        {
            WallboxMeterInfo? info = await wallboxService.GetMeterInfoAsync();
            if (info == null || FöregåendeMeterInfo == null ||
                FöregåendeMeterInfo.AccEnergy == info.AccEnergy)
            {
                return info;
            }

            // OK - nytt värde på ackumulerad effekt från wallbox
            NuvarandeMeterInfo = info;

            // Ny timme, uppdatera föregående mätarställningar
            if (FöregåendeMeterInfo != null && nu.Hour != FöregåendeMeterInfo.ReadDateTime.Hour)
            {
                NästSistaMeterInfoFöregåendeTimme = SistaMeterInfoFöregåendeTimme;
                SistaMeterInfoFöregåendeTimme = FöregåendeMeterInfo;
            }

            KalkyleraFörbrukningInnevarandeTimme(nu);

            FöregåendeMeterInfo = NuvarandeMeterInfo;

            // ----- Spara i databasen -----
            using var scope = serviceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Generera poster för varje timgräns
            var entries = GenerateHourBoundaryReadings(
                FöregåendeMeterInfo!.ReadDateTime,
                FöregåendeMeterInfo.AccEnergy,
                NuvarandeMeterInfo.ReadDateTime,
                NuvarandeMeterInfo.AccEnergy);

            db.WallboxMeterReadings.AddRange(entries);
            await db.SaveChangesAsync(stoppingToken);

            return NuvarandeMeterInfo;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read or store meter info");
            return null;
        }
    }

    private void KalkyleraFörbrukningInnevarandeTimme(DateTime nu)
    {
        try
        {
            // räkna ut förbrukning innevarande timme 
            if (NuvarandeMeterInfo != null &&
                FöregåendeMeterInfo != null &&
                SistaMeterInfoFöregåendeTimme != null)
            {
                var timeSinceLast = NuvarandeMeterInfo!.ReadDateTime -
                                    SistaMeterInfoFöregåendeTimme.ReadDateTime;

                var totalFörbrukning = NuvarandeMeterInfo.AccEnergy -
                                       SistaMeterInfoFöregåendeTimme.AccEnergy;
                var forbrukningPerSecond = totalFörbrukning > 0
                    ? totalFörbrukning / timeSinceLast.TotalSeconds
                    : 0;

                var sekunderDennaTimme = NuvarandeMeterInfo.ReadDateTime
                    .Subtract(new DateTime(nu.Year, nu.Month, nu.Day, nu.Hour, 0, 0)).TotalSeconds;
                FörbrukningDennaTimme = (long)(forbrukningPerSecond * sekunderDennaTimme);
                FörbrukningTotalDennaTimme = (long)(forbrukningPerSecond * 3600);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "KalkyleraFörbrukningInnevarandeTimme: oväntad exeption!");
        }
    }

    /// <summary>
    /// Genererar mätarposter för varje timgräns mellan två mätningar.
    /// Om mätningarna är inom samma timme returneras endast en post med slutvärdet.
    /// Om de spänner över timgränser skapas en post vid varje jämn timme med interpolerat AccEnergy.
    /// </summary>
    /// <param name="previousTime">Tidpunkt för föregående mätning.</param>
    /// <param name="previousEnergy">Ackumulerad energi vid föregående mätning.</param>
    /// <param name="currentTime">Tidpunkt för aktuell mätning.</param>
    /// <param name="currentEnergy">Ackumulerad energi vid aktuell mätning.</param>
    /// <returns>Lista med mätarposter, en för varje timgräns plus slutmätningen.</returns>
    internal List<WallboxMeterReading> GenerateHourBoundaryReadings(
        DateTime previousTime,
        long previousEnergy,
        DateTime currentTime,
        long currentEnergy)
    {
        var entries = new List<WallboxMeterReading>();

        var previousHour = new DateTime(previousTime.Year, previousTime.Month, previousTime.Day, previousTime.Hour, 0, 0);
        var currentHour = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, currentTime.Hour, 0, 0);

        // Om samma timme, skapa bara en post med slutvärdet
        if (previousHour == currentHour)
        {
            entries.Add(new WallboxMeterReading
            {
                ReadAt = currentTime,
                RawJson = "",
                AccEnergy = currentEnergy,
                MeterSerial = "",
                ApparentPower = 0
            });
            return entries;
        }

        // Beräkna energiökning per sekund för interpolering
        var totalSeconds = (currentTime - previousTime).TotalSeconds;
        var totalEnergyDiff = currentEnergy - previousEnergy;
        var energyPerSecond = totalSeconds > 0 ? totalEnergyDiff / totalSeconds : 0;

        // Skapa poster för varje timgräns
        var hourBoundary = previousHour.AddHours(1);
        while (hourBoundary <= currentHour)
        {
            var secondsFromPrevious = (hourBoundary - previousTime).TotalSeconds;
            var interpolatedEnergy = previousEnergy + (long)(energyPerSecond * secondsFromPrevious);

            entries.Add(new WallboxMeterReading
            {
                ReadAt = hourBoundary,
                RawJson = "",
                AccEnergy = interpolatedEnergy,
                MeterSerial = "",
                ApparentPower = 0
            });

            hourBoundary = hourBoundary.AddHours(1);
        }

        // Lägg till slutmätningen med faktiskt värde
        entries.Add(new WallboxMeterReading
        {
            ReadAt = currentTime,
            RawJson = "",
            AccEnergy = currentEnergy,
            MeterSerial = "",
            ApparentPower = 0
        });

        return entries;
    }


    /// <summary>
    /// Calculates total monthly energy consumption by retrieving the first and last meter readings for the specified month.
    /// This is more efficient than hourly calculations when you only need the total monthly usage.
    /// </summary>
    /// <param name="dateInMonth">A date that determines which month to calculate for.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>Total energy consumption in Wh for the specified month, or 0 if insufficient data.</returns>
    public async Task<long> GetMonthlyEnergyUsageAsync(DateTime dateInMonth, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Bestäm start- och slutdatum för månaden
            var startOfMonth = new DateTime(dateInMonth.Year, dateInMonth.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);

            logger.LogDebug("Calculating monthly energy usage for {Month:yyyy-MM}", dateInMonth);

            // Hämta sista läsningen i föregående månad
            var firstReading = await db.WallboxMeterReadings
                .Where(x => x.ReadAt >= startOfMonth)
                .OrderBy(x => x.ReadAt)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            if (firstReading is null)
            {
                logger.LogWarning("No meter readings found for {Month:yyyy-MM}", dateInMonth);
                return 0;
            }

            // Första läsningen i nästa månad.
            var lastReading = await db.WallboxMeterReadings
                .Where(x => x.ReadAt > endOfMonth)
                .OrderBy(x => x.ReadAt)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            if (lastReading is null)
            {
                // Månaden är innevarande månad, hämta senaste läsningen istället
                lastReading = await db.WallboxMeterReadings
                    .Where(x => x.ReadAt <= endOfMonth)
                    .OrderByDescending(x => x.ReadAt)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (lastReading is null)
            {
                logger.LogInformation("Could not find last reading for {Month:yyyy-MM}", dateInMonth);
                return 0;
            }

            // Beräkna skillnaden mellan första och sista läsningen
            var monthlyUsageWh = lastReading.AccEnergy - firstReading.AccEnergy;

            logger.LogDebug("Monthly energy usage for {Month:yyyy-MM}: {Usage} Wh ({UsageKwh:F2} kWh)",
                dateInMonth, monthlyUsageWh, monthlyUsageWh / 1000.0);

            return monthlyUsageWh;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calculating monthly energy usage for {Month:yyyy-MM}", dateInMonth);
            return 0;
        }
    }

    /// <summary>
    /// Beräkna gränsvärde när timförbrukningen är för hög.
    /// </summary>
    /// <param name="nu">Justeringsvärde, används för att beräkna gränsvärdet baserat på aktuell tid.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<long> KalkyleraGrans(DateTime nu, CancellationToken cancellationToken )
    {
        long max;   // Högsta timförbrukningen i Wh
        HourlyEnergyUsage usage;
        if (IsHighEffect(nu))
        {
            usage = await GetHighestHourlyEnergyUsageDaytimeAsync(nu, cancellationToken);
            max = usage.EnergyUsageWh;
            if (max < 1500)
            {
                max = 1500;
            }
        }
        else
        {
            usage = await GetHighestHourlyEnergyUsageAsync(nu, cancellationToken);
            max = usage.EnergyUsageWh;
            if (max < 3000)
            {
                max = 3000;
            }
        }
        return max * nu.Minute / 60;
    }


    public async Task<HourlyEnergyUsage> GetHighestHourlyEnergyUsageAsync(DateTime dateInMonth, CancellationToken cancellationToken = default)
    {
        var hourlyUsage = await GetHourlyEnergyUsageAsync(dateInMonth, cancellationToken);
        lock (CacheLocker)
        {
            return hourlyUsage.OrderByDescending(x => x.EnergyUsageWh)
                .FirstOrDefault(
                    new HourlyEnergyUsage(new DateTime(dateInMonth.Year, dateInMonth.Month, 1), 0));
        }
    }

    /// <summary>
    /// Högsta värdet i intervallet för oktober till mars vardagar 7-19
    /// </summary>
    /// <param name="dateInMonth">Datum i efterfrågad månad</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    public async Task<HourlyEnergyUsage> GetHighestHourlyEnergyUsageDaytimeAsync(DateTime dateInMonth, CancellationToken cancellationToken = default)
    {
        var hourlyUsage = await GetHourlyEnergyUsageAsync(dateInMonth, cancellationToken);

        // Filter for October to March weekdays 7-19
        lock (CacheLocker)
        {
            return hourlyUsage
            .Where(x => x.Hour.Hour >= 7 && x.Hour.Hour < 19) // Hours between 7-19
            .Where(x => x.Hour.DayOfWeek >= DayOfWeek.Monday && x.Hour.DayOfWeek <= DayOfWeek.Friday) // Weekdays only
            .Where(x => x.Hour.Month >= 10 || x.Hour.Month <= 3) // October to March
            .OrderByDescending(x => x.EnergyUsageWh)
            .FirstOrDefault(new HourlyEnergyUsage(new DateTime(dateInMonth.Year, dateInMonth.Month, 1), 0));
        }
    }

    /// <summary>
    /// Calculates hourly energy consumption for a specific month by comparing the last meter reading of each hour
    /// with the last meter reading from the previous hour. Uses streaming for efficient memory usage.
    /// Results are cached for one hour to avoid unnecessary recalculation.
    /// </summary>
    /// <param name="dateInMonth">A date that determines which month to calculate for. Only readings from this month will be included.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A list of hourly energy usage data sorted by hour for the specified month.</returns>
    internal async Task<List<HourlyEnergyUsage>> GetHourlyEnergyUsageAsync(DateTime dateInMonth, CancellationToken cancellationToken = default)
    {
        // Kontrollera om cachen är giltig (samma timme som nu)
        lock (CacheLocker)
        {
            var now = DateTime.Now;
            var currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);

            // Om cachen är från samma timme, returnera cachad data
            if (currentHour == LastHourlyEnergyUsageCacheTime && HourlyEnergyUsageCache.Count > 0)
            {
                return HourlyEnergyUsageCache;
            }
        }

        // Beräkna ny data
        var result = await CalculateHourlyEnergyUsageAsync(dateInMonth, cancellationToken);

        // Uppdatera cachen
        lock (CacheLocker)
        {
            DateTime nu = DateTime.Now;
            LastHourlyEnergyUsageCacheTime = new DateTime(nu.Year, nu.Month, nu.Day, nu.Hour, 0, 0);
            HourlyEnergyUsageCache = result;
        }

        return result;
    }

    /// <summary>
    /// Performs the actual hourly energy consumption calculation using streaming for efficient memory usage.
    /// </summary>
    private async Task<List<HourlyEnergyUsage>> CalculateHourlyEnergyUsageAsync(DateTime dateInMonth, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Bestäm start- och slutdatum för månaden
            var startOfMonth = new DateTime(dateInMonth.Year, dateInMonth.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);

            logger.LogDebug("Calculating hourly energy usage for {Month:yyyy-MM} (from {Start:yyyy-MM-dd} to {End:yyyy-MM-dd})",
                dateInMonth, startOfMonth, endOfMonth);

            var hourlyUsage = new List<HourlyEnergyUsage>();
            WallboxMeterReading? lastReading = null;
            long totalReadings = 0;

            // Använd streaming för att undvika att ladda allt i minnet på en gång
            await foreach (var reading in db.WallboxMeterReadings
                .Where(x => x.ReadAt >= startOfMonth && x.ReadAt <= endOfMonth)
                .OrderBy(x => x.ReadAt)
                .AsNoTracking()
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken))
            {
                totalReadings++;

                if (lastReading is null)
                {
                    lastReading = reading;
                    continue;
                }

                // Om vi skiftat timme eller dag, beräkna förbrukningen
                if (reading.ReadAt.Hour != lastReading.ReadAt.Hour || reading.ReadAt.Date != lastReading.ReadAt.Date)
                {
                    var tidsdiff = reading.ReadAt - lastReading.ReadAt;
                    var energyUsageWh = reading.AccEnergy - lastReading.AccEnergy;
                    // Fördela förbrukningen jämnt över timmarna
                    energyUsageWh = (long)(energyUsageWh * (3600.0 / tidsdiff.TotalSeconds));

                    hourlyUsage.Add(new HourlyEnergyUsage(
                        new DateTime(lastReading.ReadAt.Year, lastReading.ReadAt.Month, lastReading.ReadAt.Day, lastReading.ReadAt.Hour, 0, 0),
                        energyUsageWh
                    ));
                    lastReading = reading;
                }
            }

            logger.LogDebug("Processed {TotalReadings} readings and calculated {HourlyCount} hourly usages for {Month:yyyy-MM}.",
                totalReadings, hourlyUsage.Count, dateInMonth);

            return hourlyUsage;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calculating hourly energy usage");
            return new List<HourlyEnergyUsage>();
        }
    }

    /// <summary>
    /// Kontrollera om högbelastningstaxan är aktiv (oktober-mars vardagar kl 7-19).
    /// </summary>
    /// <param name="nu"></param>
    /// <returns></returns>
    private bool IsHighEffect(DateTime nu)
    {
        var month = nu.Month;
        var hour = nu.Hour;

        // Oktober till mars (10-12, 1-3) och mellan kl 7-19, endast vardagar
        bool isWinterMonth = month >= 10 || month <= 3;
        bool isHighEffectHour = hour >= 7 && hour < 19;
        bool isWeekday = nu.DayOfWeek >= DayOfWeek.Monday && nu.DayOfWeek <= DayOfWeek.Friday;

        return isWinterMonth && isHighEffectHour && isWeekday;
    }
}

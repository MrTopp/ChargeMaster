using ChargeMaster.Data;
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
    IServiceProvider serviceProvider,
    WallboxService wallboxService,
    ILogger<WallboxWorker> logger) : BackgroundService
{
    /// <summary>
    /// Event raised when meter information is calculated and ready for consumption.
    /// </summary>
    public event EventHandler<MeterInfoEventArgs>? MeterInfoCalculated;

    /// <summary>
    /// Tracks the last recorded accumulated energy value to avoid storing duplicate readings.
    /// </summary>
    private double? LastStoredAccEnergy { get; set; }

    /// <summary>
    /// Cache for hourly energy usage calculations. Stores data and timestamp of last calculation.
    /// </summary>
    private DateTime _lastHourlyEnergyUsageCacheTime = DateTime.MinValue;
    private List<HourlyEnergyUsage> _hourlyEnergyUsageCache = new();
    private readonly object _cacheLocker = new object();

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await WallboxLoop(stoppingToken);
            }
            //catch (OperationCanceledException)
            //{
            //    break;
            //}
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
        while (!stoppingToken.IsCancellationRequested)
        {
            // Initiera genom att läsa upp status
            WallboxStatus wallboxStatus = await InitializeWallboxStatusAsync(stoppingToken);

            // Kontrollera klockan på wallboxen
            await CheckWallboxTimeAsync(wallboxStatus);

            // Kontrollera schema 
            //await CheckWallboxScheduleAsync();

            // Spara status på förbrukad el
            WallboxMeterInfo? meterInfo = await ReadEnergyAsync(stoppingToken);

            // Räkna ut nuvarande effekt
            CalculateCurrentPowerAsync(meterInfo);

            // vänta innan nästa iteration
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }

    public WallboxMeterInfo? MeterInfo { get; private set; }

    // Föregående mätning
    private DateTime _lastMeterInfoCalculationTime = DateTime.MinValue;
    private long _lastStartAccEnergy;

    // Sista mätningen i förra timmen
    private DateTime _finalMeterInfoCalculationTime = DateTime.MinValue;
    private long _finalStartAccEnergy;

    private double _currentHourAccEnergy;     // Ackumulerad energi för innevarande timme
    private double _currentHourTotalEnergy;   // Beräknad total energi för innevarande timme

    internal void CalculateCurrentPowerAsync(WallboxMeterInfo? meterInfo, DateTime? testNu = null)
    {
        // Räknar ut förbrukning innevarande timme. Sista mätningen från föregående timme är startpunkt
        // eftersom vi inte kan få exakt värde vid timskiftet.
        MeterInfo = meterInfo;
        if (meterInfo is null) return;

        // Initiering vid uppstart, sätt startvärden
        if (_lastStartAccEnergy == 0)
        {
            // Kan inget göra, vänta på läsning med uppdaterad effekt
            _lastStartAccEnergy = meterInfo.AccEnergy;
        }
        else if (_lastStartAccEnergy != meterInfo.AccEnergy)
        {
            // Uppdaterad effekt
            DateTime nu = testNu ?? DateTime.Now;
            if (_finalMeterInfoCalculationTime == DateTime.MinValue)
            {
                // initiera baseline för timme
                _finalMeterInfoCalculationTime = nu;
                _finalStartAccEnergy = meterInfo.AccEnergy;
            }
            else
            {
                // Om ny timme, nollställ startvärden för timme
                if (nu.Hour != _lastMeterInfoCalculationTime.Hour)
                {
                    _finalMeterInfoCalculationTime = _lastMeterInfoCalculationTime;
                    _finalStartAccEnergy = _lastStartAccEnergy;
                }
                // Beräkna timmens effektvärden
                _currentHourAccEnergy = meterInfo.AccEnergy - _finalStartAccEnergy;
                var secondsSinceLastCalculation = (int)(nu - _finalMeterInfoCalculationTime).TotalSeconds;
                _currentHourTotalEnergy = secondsSinceLastCalculation > 0
                    ? _currentHourAccEnergy * 3600 / secondsSinceLastCalculation
                    : 0;
            }
            _lastMeterInfoCalculationTime = nu;
            _lastStartAccEnergy = meterInfo.AccEnergy;
        }

        // Posta alltid event med aktuella värden.
        meterInfo.EffektTimmeNu = _currentHourAccEnergy;
        meterInfo.EffektTimmeTotal = _currentHourTotalEnergy;
        MeterInfoCalculated?.Invoke(this, new MeterInfoEventArgs(meterInfo));

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
    /// Enforces the charging schedule on the Wallbox based on the current season (Summer/Winter).
    /// </summary>
    /// <remarks>
    /// In summer (April-October), no schedule restrictions are applied.
    /// In winter, specific charging slots (00:00-07:00 and 19:00-24:00) are enforced on weekdays.
    /// </remarks>
    internal async Task CheckWallboxScheduleAsync()
    {
        var schema = await wallboxService.GetSchemaAsync();
        if (schema is null) return;

        var now = DateTime.Now;
        var start = new DateTime(now.Year, 4, 1);
        var end = new DateTime(now.Year, 10, 31);
        var isSummerPeriod = now.Date >= start && now.Date <= end;

        if (isSummerPeriod)
        {
            if (schema.Count == 0) return;

            foreach (var entry in schema)
            {
                await wallboxService.DeleteSchemaAsync(entry.SchemaId);
            }

            return;
        }


        var allowed = schema
            .Where(e => IsAllowedWinterTimeSlot(e.Start, e.Stop))
            .ToList();
        if (allowed.Count == 10 && schema.Count == 10)
            return;

        // Remove all not allowed entries
        foreach (var entry in schema.OrderByDescending(x => x.SchemaId))
        {
            if (!IsAllowedWinterTimeSlot(entry.Start, entry.Stop))
                await wallboxService.DeleteSchemaAsync(entry.SchemaId);
        }

        // Add missing entries
        for (int day = 1; day <= 5; day++)
        {
            WallboxSchemaEntry entry1 = new()
            {
                Start = "00:00:00",
                Stop = "07:00:00",
                Weekday = day.ToString(),
                ChargeLimit = 0
            };
            if (!schema.Any(e => e.Equals(entry1)))
                await wallboxService.SetSchemaAsync(entry1);
            WallboxSchemaEntry entry2 = new()
            {
                Start = "19:00:00",
                Stop = "24:00:00",
                Weekday = day.ToString(),
                ChargeLimit = 0
            };
            if (!schema.Any(e => e.Equals(entry2)))
                await wallboxService.SetSchemaAsync(entry2);
        }
    }

    /// <summary>
    /// Determines if a given time slot is allowed during the winter period.
    /// </summary>
    /// <param name="start">Start time string.</param>
    /// <param name="stop">Stop time string.</param>
    /// <returns>True if the slot matches permitted winter hours; otherwise false.</returns>
    private static bool IsAllowedWinterTimeSlot(string? start, string? stop)
    {
        if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(stop)) return false;

        if (!TimeOnly.TryParse(start, out var startTime)) return false;
        if (!TimeOnly.TryParse(stop, out var stopTime)) return false;

        if (startTime == new TimeOnly(0, 0)
            && stopTime == new TimeOnly(7, 0)) return true;

        if (startTime == new TimeOnly(19, 0)
            && stopTime == new TimeOnly(0, 0)) return true;

        return false;
    }

    /// <summary>
    /// Checks that the Wallbox clock is synchronized with the server time and updates it if the drift exceeds 5 minutes.
    /// </summary>
    /// <param name="wallboxStatus">The current status object containing the Wallbox time.</param>
    internal async Task CheckWallboxTimeAsync(WallboxStatus wallboxStatus)
    {
        // Wallboxens tid i format HH:mm
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

    private DateTimeOffset? _lastReadingAt;
    private long? _lastReading;

    /// <summary>
    /// Reads the current meter information from the Wallbox and persists it to the database if the accumulated energy has changed.
    /// </summary>
    /// <param name="stoppingToken">Token to monitor for cancellation requests.</param>
    /// <returns>The meter information that was read, or null if no information was available.</returns>
    internal async Task<WallboxMeterInfo?> ReadEnergyAsync(CancellationToken stoppingToken)
    {
        try
        {
            WallboxMeterInfo? info = await wallboxService.GetMeterInfoAsync();
            if (info is null)
                return null;

            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetService<ApplicationDbContext>();

            // Skip database operations if db context is not available
            if (db is null)
            {
                logger.LogCritical(
                    "Database context is not available. Meter info will not be persisted.");
                return info;
            }

            // Initialize LastStoredAccEnergy if not set (first run)
            if (!LastStoredAccEnergy.HasValue)
            {
                LastStoredAccEnergy = await db.WallboxMeterReadings
                    .OrderByDescending(r => r.ReadAt)
                    .Select(r => (double?)r.AccEnergy)
                    .FirstOrDefaultAsync(stoppingToken);
            }

            // Skip if the accumulated energy is the same as last stored
            if (LastStoredAccEnergy.HasValue &&
                Math.Abs(LastStoredAccEnergy.Value - info.AccEnergy) < 0.01)
                return info;

            var entry = new WallboxMeterReading
            {
                ReadAt = DateTime.Now,
                RawJson = "",  // System.Text.Json.JsonSerializer.Serialize(info),
                AccEnergy = info.AccEnergy,
                MeterSerial = "", // info.MeterSerial,
                ApparentPower = info.ApparentPower
            };

            db.WallboxMeterReadings.Add(entry);
            await db.SaveChangesAsync(stoppingToken);
            if (_lastReading != null && _lastReadingAt != null)
            {
                var tid = (TimeSpan)(DateTimeOffset.Now - _lastReadingAt);
                var sec = (long)tid.TotalSeconds;

                var effect = sec != 0 ? 3600.0 / sec / 10 : 0;

                logger.LogInformation("Energy usage {effect:F1} kW", effect);
            }

            _lastReadingAt = DateTimeOffset.Now;
            _lastReading = info.AccEnergy;

            LastStoredAccEnergy = info.AccEnergy;

            return info;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read or store meter info");
            return null;
        }
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
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetService<ApplicationDbContext>();

            if (db is null)
            {
                logger.LogCritical("Database context is not available. Cannot calculate monthly energy usage.");
                return 0;
            }

            // Bestäm start- och slutdatum för månaden
            var startOfMonth = new DateTime(dateInMonth.Year, dateInMonth.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);

            logger.LogDebug("Calculating monthly energy usage for {Month:yyyy-MM}", dateInMonth);

            // Hämta första läsningen i månaden
            var firstReading = await db.WallboxMeterReadings
                .Where(x => x.ReadAt >= startOfMonth && x.ReadAt <= endOfMonth)
                .OrderBy(x => x.ReadAt)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            if (firstReading is null)
            {
                logger.LogWarning("No meter readings found for {Month:yyyy-MM}", dateInMonth);
                return 0;
            }

            // Hämta sista läsningen i månaden
            var lastReading = await db.WallboxMeterReadings
                .Where(x => x.ReadAt >= startOfMonth && x.ReadAt <= endOfMonth)
                .OrderByDescending(x => x.ReadAt)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

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

    public async Task<HourlyEnergyUsage> GetHighestHourlyEnergyUsageAsync(DateTime dateInMonth, CancellationToken cancellationToken = default)
    {
        var hourlyUsage = await GetHourlyEnergyUsageAsync(dateInMonth, cancellationToken);
        return hourlyUsage.OrderByDescending(x => x.EnergyUsageWh)
            .FirstOrDefault(new HourlyEnergyUsage(new DateTime(dateInMonth.Year, dateInMonth.Month, 1), 0));
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
        return hourlyUsage
            .Where(x => x.Hour.Hour >= 7 && x.Hour.Hour < 19) // Hours between 7-19
            .Where(x => x.Hour.DayOfWeek >= DayOfWeek.Monday && x.Hour.DayOfWeek <= DayOfWeek.Friday) // Weekdays only
            .Where(x => x.Hour.Month >= 10 || x.Hour.Month <= 3) // October to March
            .OrderByDescending(x => x.EnergyUsageWh)
            .FirstOrDefault(new HourlyEnergyUsage(new DateTime(dateInMonth.Year, dateInMonth.Month, 1), 0));
    }

    /// <summary>
    /// Calculates hourly energy consumption for a specific month by comparing the last meter reading of each hour
    /// with the last meter reading from the previous hour. Uses streaming for efficient memory usage.
    /// Results are cached for one hour to avoid unnecessary recalculation.
    /// </summary>
    /// <param name="dateInMonth">A date that determines which month to calculate for. Only readings from this month will be included.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>A list of hourly energy usage data sorted by hour for the specified month.</returns>
    public async Task<List<HourlyEnergyUsage>> GetHourlyEnergyUsageAsync(DateTime dateInMonth, CancellationToken cancellationToken = default)
    {
        // Kontrollera om cachen är giltig (samma timme som nu)
        lock (_cacheLocker)
        {
            var now = DateTime.Now;
            var cacheHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
            var lastCacheHour = new DateTime(_lastHourlyEnergyUsageCacheTime.Year, _lastHourlyEnergyUsageCacheTime.Month,
                _lastHourlyEnergyUsageCacheTime.Day, _lastHourlyEnergyUsageCacheTime.Hour, 0, 0);

            // Om cachen är från samma timme, returnera cachad data
            if (cacheHour == lastCacheHour && _hourlyEnergyUsageCache.Count > 0)
            {
                return _hourlyEnergyUsageCache;
            }
        }

        // Beräkna ny data
        var result = await CalculateHourlyEnergyUsageAsync(dateInMonth, cancellationToken);

        // Uppdatera cachen
        lock (_cacheLocker)
        {
            _lastHourlyEnergyUsageCacheTime = DateTime.Now;
            _hourlyEnergyUsageCache = result;
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
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetService<ApplicationDbContext>();

            if (db is null)
            {
                logger.LogCritical("Database context is not available. Cannot calculate hourly energy usage.");
                return new List<HourlyEnergyUsage>();
            }

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
}

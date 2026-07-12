using ChargeMaster.Data;
using ChargeMaster.Services.Daikin;
using ChargeMaster.Services.ElectricityPrice;
using ChargeMaster.Services.Shelly;

namespace ChargeMaster.Workers;

/// <summary>
/// Bakgrundstjänst som en gång i timmen läser status från Daikin värmepump.
/// </summary>
public class DaikinWorker(
    IServiceScopeFactory serviceScopeFactory,
    DaikinFacade daikinFacade,
    ElectricityPriceService electricityPriceService,
    WallboxWorker wallboxWorker,
    SmhiWorker smhiWorker,
    ShellyMqttService shellyMqttService,
    ILogger<DaikinWorker> logger) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DaikinLoop(stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Förväntat när tjänsten stoppas, ingen åtgärd krävs.
                logger.LogInformation("DaikinWorker is stopping due to cancellation.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in DaikinWorker loop");
            }
        }
    }

    private async Task DaikinLoop(CancellationToken stoppingToken)
    {
        await daikinFacade.InitializeAsync(forceEvent: true);
        double previousTemp = daikinFacade.TargetTemperature ?? 22L;
        bool previousHeat = true;
        DateTime previous = DateTime.Now;

        while (!stoppingToken.IsCancellationRequested)
        {
            DateTime dt = DateTime.Now;
            DateTime nu = new DateTime(dt.Year, dt.Month, dt.Day,
                dt.Hour, dt.Minute, 0);

            // Varje timme
            if (nu.Hour != previous.Hour)
            {
                EmergencyStopped = false; // Återställ nödstopp
            }

            // Uppdatera börvärde mot schema och maxpris
            var (temp, heat, log) = await KalkyleraTemperatur(nu);

            // Uppdatera Daikin endast om börvärde är ändrad eller läge är ändrat
            if (Math.Abs(temp - previousTemp) > 0.2 || heat != previousHeat)
            {
                logger.LogDebug("Uppdaterar Daikin måltemperatur: {Temp}°C (Värme: {Heat})", temp,
                    heat);
                logger.LogInformation(log);
                await daikinFacade.SetTargetTemperatureAsync(temp, heat);
                await SaveDaikinSession(nu, temp, heat);
                previousTemp = temp;
                previousHeat = heat;
            }
            else
            {
                await daikinFacade.UpdateStatusAsync(forceEvent: false);
            }

            previous = nu;

            // Vänta tills nästa hela minut
            var targetNextMinute = nu.AddMinutes(1);
            var delayTimeMs = (targetNextMinute - DateTime.Now).TotalMilliseconds;
            if (delayTimeMs > 0)
            {
                await Task.Delay((int)delayTimeMs, stoppingToken);
            }
        }
    }

    /// <summary>
    /// Beräkna börvärde för temperatur
    /// </summary>
    /// <param name="nu">Referenstid, normalt DateTime.Now</param>
    /// <returns>Börvärde temperatur och true/false för heat/cool</returns>
    private async Task<(double, bool, string)> KalkyleraTemperatur(DateTime nu)
    {
        // ----- Nödstopp draget -----
        //if (EmergencyStopped)
        //{
        //    return (16, true);
        //}

        bool heat = true;
        double temp = 23;

        // ----- Justera mot temperatur inne -----
        double inneTemp = shellyMqttService.GetAverage();
        temp = inneTemp switch
        {
            < 18 => 28,
            < 20 => temp + 4,
            < 20.5 => temp + 3,
            < 21.5 => temp + 2,
            < 21.9 => temp + 1,
            > 24 => 16,
            > 23 => 20,
            > 22.7 => temp - 3,
            > 22.5 => temp - 2,
            > 22.2 => temp - 1,
            _ => temp
        };

        // ----- Justera mot temperatur ute om 5 timmar -----
        double? smhiTemp = smhiWorker.GetForecastTemperature(5);
        if (smhiTemp != null)
        {
            temp = smhiTemp switch
            {
                < -15 => temp + 6,
                < -10 => temp + 4,
                < -5 => temp + 2,
                < 2 => temp + 1,
                _ => temp
            };
        }

        // ----- Justera mot temperatur ute enligt Daikin -----
        double daikinUteTemp = daikinFacade.OutdoorTemperature ?? 0;
        if (daikinUteTemp > smhiTemp + 5)
        {
            temp = daikinUteTemp switch
            {
                > 25 => 16,
                > 20 => 20,
                > 15 => temp - 2,
                > 10 => temp - 1,
                _ => temp
            };
        }

        // ----- Justera mot elpris -----
        if (inneTemp > 20.5)
        {
            if (await JusteraMotPris(nu))
            {
                // sätt måltemp till 20 under högprisperioder 
                return (20, true,
                    "High price period detected. Setting target temperature to 20°C.");
            }
        }

        // ----- Aktivera cool mode om det är varmt i sovrummet på kvällen -----
        if (DateTime.Now.Hour >= 20 || DateTime.Now.Hour <= 4)
        {
            var sovtemp = shellyMqttService.GetSovrumTemperature();
            if (sovtemp > 24)
            {
                temp = sovtemp switch
                {
                    > 26 => 16,
                    > 25.5 => 18,
                    > 25 => 20,
                    _ => 21
                };
                heat = false;
            }
        }

        string log
            = $"Calculated target temperature: target {temp:F1}°C inne {inneTemp:F2} daikin {daikinUteTemp:F1} smhi {smhiTemp:F1} (Heat: {heat})";


        return (temp, heat, log);
    }


    private (DateOnly Date, List<ElectricityPrice> ExpensiveHours)? _cachedPrices;

    /// <summary>
    /// Kontrollera om det är hög elprisperiod.
    /// </summary>
    /// <param name="nu">Referenstid för prisjämförelse.</param>
    /// <param name="temp">Nuvarande beräknad måltemperatur.</param>
    /// <returns>true om high price.</returns>
    private async Task<bool> JusteraMotPris(DateTime nu)
    {
        // Om priset är riktigt lågt, kosta på lite extra värme
        //var currentPrice = await GetCurrentPrice(nu);
        //if (currentPrice != null && currentPrice < 0.1m)
        //{
        //    return temp + 2;
        //}

        // sänk temperaturen kraftigt under de dyraste timmarna på dagen
        if (_cachedPrices == null || _cachedPrices.Value.Date != DateOnly.FromDateTime(nu))
        {
            var todaysPrices
                = await electricityPriceService.GetPricesForDateAsync(DateOnly.FromDateTime(nu));
            var expensiveHours = todaysPrices.OrderByDescending(p => p.SekPerKwh)
                .Where(p => p.SekPerKwh >= 1.0m)
                .Take(10).ToList();
            _cachedPrices = (DateOnly.FromDateTime(nu), expensiveHours);
        }

        var priceList = _cachedPrices.Value.ExpensiveHours;
        if (priceList.Count == 0)
        {
            return false;
        }

        // Finns 'nu' i listan?
        var pris = priceList
            .FirstOrDefault(p => p.TimeStart <= nu && p.TimeEnd > nu);
        if (pris != null)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Stoppa värmepumpen omedelbart, oavsett schema. Gäller innevarande timme.
    /// </summary>
    /// <returns></returns>
    private async Task EmergencyStop()
    {
        logger.LogInformation("Emergency stop activated. Setting target temperature to 16°C.");
        EmergencyStopped = true;
        await daikinFacade.SetTargetTemperatureAsync(16, true);
    }

    /// <summary>
    /// Hämta aktuellt elpris för given tid.
    /// </summary>
    /// <param name="nu">Tidpunkt att hämta pris för.</param>
    private async Task<decimal?> GetCurrentPrice(DateTime nu)
    {
        try
        {
            var prices
                = await electricityPriceService.GetPricesForDateAsync(DateOnly.FromDateTime(nu));
            var currentPriceData = prices.FirstOrDefault(p => p.TimeStart <= nu && p.TimeEnd > nu);
            return currentPriceData?.SekPerKwh;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting current electricity price");
            return null;
        }
    }

    /// <summary>
    /// Spara Daikin sessiondata till databasen.
    /// </summary>
    private async Task SaveDaikinSession(
        DateTime timestamp, double targetTemperature, bool isHeating)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var session = new DaikinSession
            {
                Timestamp = timestamp,
                TargetTemperature = targetTemperature,
                IsHeating = isHeating,
                ArbetsrumTemperature = shellyMqttService.GetArbetsrumTemperature(),
                HallTemperature = shellyMqttService.GetHallTemperature(),
                SovrumTemperature = shellyMqttService.GetSovrumTemperature(),
                CurrentPrice = await GetCurrentPrice(timestamp)
            };

            dbContext.DaikinSessions.Add(session);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving Daikin session data");
        }
    }

    private bool EmergencyStopped { get; set; }

    /// <summary>
    /// Kontrollera om timmens förbrukning överstiger gränsvärdet.
    /// </summary>
    /// <param name="forbrukningDennaTimme">Uppskattad förbrukning för innevarande timme i Wh.</param>
    /// <param name="nu">Datum för beräkning.</param>
    /// <param name="cancellationToken">Token för att avbryta operationen.</param>
    public async Task KontrolleraEffekt(
        long forbrukningDennaTimme, DateTime nu, CancellationToken cancellationToken)
    {
        if (nu.Minute < 20)
        {
            return; // Vänta 20 minuter in i timmen innan vi börjar kolla.
        }

        var grans = await wallboxWorker.KalkyleraGrans(nu, cancellationToken);
        if (EmergencyStopped && forbrukningDennaTimme < grans * 0.8)
        {
            logger.LogInformation(
                "Emergency stop restore. Förbrukning: {Forbrukning} Wh, Grans: {Grans} Wh.",
                forbrukningDennaTimme, grans);
            EmergencyStopped = false;
            return;
        }

        if (EmergencyStopped) return;

        // bilens laddning stoppas om förbrikningen är större än gränsvärdet. 
        // värmepumpen stoppas lite senare för att undvika ryckig drift.
        if (forbrukningDennaTimme > grans * 1.05)
        {
            //  För hög förbrukning -> stoppa värmepumpen
            logger.LogInformation(
                "! Värmepumpen stoppas på grund av hög förbrukning: {forbrukning} Wh.",
                forbrukningDennaTimme);
            await EmergencyStop();
        }
    }
}

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

            // Uppdatera Daikin endast om börvärde är ändrad
            if (Math.Abs(temp - previousTemp) > 0.2)
            {
                logger.LogDebug("Uppdaterar Daikin måltemperatur: {Temp}°C", temp);
                logger.LogInformation(log);
                await daikinFacade.SetTargetTemperatureAsync(temp, heat);
                await SaveDaikinSession(nu, temp, heat);
                previousTemp = temp;
            }
            else
            {
                await daikinFacade.UpdateStatusAsync(forceEvent: false);
            }

            previous = nu;

            // Vänta tills nästa hela minut
            var targetNextMinute = nu.AddMinutes(1);
            while (DateTime.Now < targetNextMinute && !stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(100, stoppingToken);
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
        double temp = 24;

        // ----- Justera mot temperatur inne -----
        var inneTemp = shellyMqttService.GetAverage();
        temp = inneTemp switch
        {
            < 18 => 28,
            < 20 => temp + 4,
            < 20.5 => temp + 3,
            < 21 => temp + 2,
            < 21.5 => temp + 1,
            > 24 => 16,
            > 23 => 20,
            > 22.5 => temp - 2,
            > 22.2 => temp - 1,
            _ => temp
        };

        // ----- Justera mot temperatur ute -----
        var temperature = smhiWorker.GetCurrentTemperature(2);
        if (temperature != null)
        {
            temp = temperature switch
            {
                < -15 => temp + 6,
                < -10 => temp + 4,
                < -5 => temp + 2,
                < 0 => temp + 1,
                _ => temp
            };
        }

        // ----- Justera mot temperatur ute enligt Daikin -----
        double daikinUteTemp = daikinFacade.OutdoorTemperature ?? 0;
        if (daikinUteTemp > temperature + 5)
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
        temp = await JusteraMotPris(nu, temp);

        // ----- Aktivera cool mode under varma sommardagar -----
        if (inneTemp > 25)
        {
            temp = 20;
            heat = false;
        }

        string log
            = $"Calculated target temperature: target {temp:F1}°C inne {inneTemp:F1} daikin {daikinUteTemp:F1} smhi {temperature:F1} (Heat: {heat})";
       

        return (temp, heat, log);
    }


    private (DateOnly Date, List<ElectricityPrice> ExpensiveHours)? _cachedPrices;

    /// <summary>
    /// Justera temperaturen om elpriset är extremt.
    /// </summary>
    /// <param name="nu">Referenstid för prisjämförelse.</param>
    /// <param name="temp">Nuvarande beräknad måltemperatur.</param>
    /// <returns>Justerad måltemperatur.</returns>
    private async Task<double> JusteraMotPris(DateTime nu, double temp)
    {
        // Om priset är riktigt lågt, kosta på lite extra värme
        var currentPrice = await GetCurrentPrice(nu);
        if (currentPrice != null && currentPrice < 0.1m)
        {
            return temp + 2;
        }
        if (_cachedPrices == null || _cachedPrices.Value.Date != DateOnly.FromDateTime(nu))
        {
            var todaysPrices
                = await electricityPriceService.GetPricesForDateAsync(DateOnly.FromDateTime(nu));
            var expensiveHours = todaysPrices.OrderByDescending(p => p.SekPerKwh)
                .Where(p => p.SekPerKwh >= 3.0m)
                .Take(10).ToList();
            _cachedPrices = (DateOnly.FromDateTime(nu), expensiveHours);
        }
        var priceList = _cachedPrices.Value.ExpensiveHours;
        if (priceList.Count == 0)
        {
            return temp;
        }
        // Finns 'nu' i listan?
        var pris = priceList
            .FirstOrDefault(p => p.TimeStart <= nu && p.TimeEnd > nu);
        if (pris != null)
        {
            logger.LogInformation("High price period detected. Setting target temperature to 16°C.");
            return 16;
        }
        return temp;
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
            var prices = await electricityPriceService.GetPricesForDateAsync(DateOnly.FromDateTime(nu));
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
    private async Task SaveDaikinSession(DateTime timestamp, double targetTemperature, bool isHeating)
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
    public async Task KontrolleraEffekt(long forbrukningDennaTimme, DateTime nu, CancellationToken cancellationToken)
    {
        if (nu.Minute < 20)
        {
            return; // Vänta 20 minuter in i timmen innan vi börjar kolla.
        }

        var grans = await wallboxWorker.KalkyleraGrans(nu, cancellationToken);
        if (EmergencyStopped && forbrukningDennaTimme < grans * 0.8)
        {
            logger.LogInformation("Emergency stop restore. Förbrukning: {forbrukning} Wh, Grans: {grans} Wh.", forbrukningDennaTimme, grans);
            EmergencyStopped = false;
            return;
        }

        if (EmergencyStopped) return;

        if (forbrukningDennaTimme > grans)
        {
            //  För hög förbrukning -> stoppa värmepumpen
            logger.LogInformation(
                "! Värmepumpen stoppas på grund av hög förbrukning: {forbrukning} Wh.",
                forbrukningDennaTimme);
            await EmergencyStop();
        }
    }
}

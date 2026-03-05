using ChargeMaster.Data;
using ChargeMaster.Services.Daikin;
using ChargeMaster.Services.ElectricityPrice;

namespace ChargeMaster.Workers;

/// <summary>
/// Bakgrundstjänst som en gång i timmen läser status från Daikin värmepump.
/// </summary>
public class DaikinWorker(
    DaikinFacade daikinFacade,
    ElectricityPriceService electricityPriceService,
    WallboxWorker wallboxWorker,
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
            double temp = await KalkyleraTemperatur(nu);

            // Uppdatera Daikin endast om börvärde är ändrad
            if (Math.Abs(temp - previousTemp) > 0.2)
            {
                logger.LogInformation("Uppdaterar Daikin måltemperatur: {Temp}°C (tid: {Time})",
                    temp, nu.ToString("HH:mm"));
                await daikinFacade.SetTargetTemperatureAsync(temp);
                previousTemp = temp;
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
    /// <param name="nu"></param>
    /// <returns></returns>
    private async Task<double> KalkyleraTemperatur(DateTime nu)
    {
        // Nödstopp draget
        if (EmergencyStopped)
        {
            return 16;
        }
        // Schema
        double temp = nu.Hour switch
        {
            < 4 => 21,
            < 7 => 24,
            < 11 => 20,
            < 14 => 24,
            < 16 => 22,
            < 19 => 18,
            < 22 => 24,
            _ => 21
        };
        // Justera mot elpris
        temp = await JusteraMotPris(nu, temp);
        return temp;
    }


    private (DateOnly, List<ElectricityPrice>)? _cachedPrices;

    /// <summary>
    /// Sänk temperaturen till 16°C under de 10 dyraste kvartarna (om de överstiger 3 kr/kWh).
    /// </summary>
    /// <param name="nu"></param>
    /// <param name="temp"></param>
    /// <returns></returns>
    private async Task<double> JusteraMotPris(DateTime nu, double temp)
    {
        if (_cachedPrices == null || _cachedPrices.Value.Item1 != DateOnly.FromDateTime(nu))
        {
            // Hämta upp dagens elpris 
            var l
                = await electricityPriceService.GetPricesForDateAsync(DateOnly.FromDateTime(nu));
            var l2 = l.OrderByDescending(p => p.SekPerKwh)
                .Where(p => p.SekPerKwh >= (decimal)3.0)
                .Take(10).ToList();
            _cachedPrices = (DateOnly.FromDateTime(nu), l2);
        }
        var priceList = _cachedPrices.Value.Item2;
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
        await daikinFacade.SetTargetTemperatureAsync(16);
    }

    private bool EmergencyStopped { get; set; }


    /// <summary>
    /// Kontrollera om timmens förbrukning överstiger gränsvärdet.
    /// </summary>
    public async Task KontrolleraEffekt(DateTime nu, long forbrukningDennaTimme)
    {
        if (nu.Minute < 10)
        {
            // första minuterna kan vara förvirrande, vänta tills vi har mer data
            return;
        }

        // Aktivera pumpen om förbrukningen går ner 
        var  grans = await KalkyleraGrans(nu);
        if (EmergencyStopped && forbrukningDennaTimme < grans * 0.8)
        {
            logger.LogInformation("Emergency stop restore. Forbrukning: {forbrukning} Wh, Grans: {grans} Wh.", forbrukningDennaTimme, grans);
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

    /// <summary>
    /// Beräkna gränsvärde när timförbrukningen ör för hög.
    /// </summary>
    /// <param name="nu"></param>
    /// <returns></returns>
    private async Task<long> KalkyleraGrans(DateTime nu)
    {
        long max;
        HourlyEnergyUsage usage;
        if (IsHighEffect(nu))
        {
            usage = await wallboxWorker.GetHighestHourlyEnergyUsageDaytimeAsync(nu);
            max = usage.EnergyUsageWh;
            if (max < 2000)
            {
                max = 1000; 
            }
        }
        else
        {
            usage = await wallboxWorker.GetHighestHourlyEnergyUsageAsync(nu);
            max = usage.EnergyUsageWh;
            if (max < 4000)
            {
                max = 3000; 
            }
        }
        return nu.Minute * max / 60 + 1000;
    }

    /// <summary>
    /// Kontrollera om högbelastningstaxan är aktiv (oktober-mars kl 7-19).
    /// </summary>
    /// <param name="nu"></param>
    /// <returns></returns>
    private bool IsHighEffect(DateTime nu)
    {
        var month = nu.Month;
        var hour = nu.Hour;

        // Oktober till mars (10-12, 1-3) och mellan kl 7-19
        bool isWinterMonth = month >= 10 || month <= 3;
        bool isHighEffectHour = hour >= 7 && hour < 19;

        return isWinterMonth && isHighEffectHour;
    }
}
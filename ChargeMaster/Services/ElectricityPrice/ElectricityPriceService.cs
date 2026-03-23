using Microsoft.EntityFrameworkCore;

namespace ChargeMaster.Services.ElectricityPrice;

/// <summary>
/// Tillhandahåller metoder för att hämta, lagra och hantera elprisdata för specifika datum med hjälp av ett externt
/// API och en databaskontext.
/// </summary>
/// <remarks>Den här tjänsten är avsedd att användas av bakgrundsjobb eller programkomponenter som kräver
/// aktuell elprisinformation. Den hanterar hämtning av data från det externa API:et, persistering till
/// databasen och hantering av befintliga poster. Alla operationer är asynkrona och bör awaitas för att säkerställa korrekt
/// körning. Tjänsten är inte trådsäker; samtidiga operationer på samma datum kan resultera i tävlingstillstånd.
/// </remarks>
/// <param name="httpClient">HTTP-klienten som används för att hämta elprisdata från det externa API:et.</param>
/// <param name="repository">Repository för databasåtkomst av elprisdata.</param>
/// <param name="logger">Loggern som används för att registrera informations- och felmeddelanden relaterade till elpriser.</param>
public class ElectricityPriceService(
    HttpClient httpClient,
    IElectricityPriceRepository repository,
    ILogger<ElectricityPriceService> logger)
{
    private const string PriceClass = "SE3";

    /// <summary>
    /// Cache för elprisor lagrat per dag. Nyckel är datumet, värde är listan med priser för dagen.
    /// </summary>
    private DateOnly? _cachedDate;
    private List<Data.ElectricityPrice>? _cachedPrices;
    private readonly object _cacheLock = new();

    public async Task FetchAndStorePricesForDateAsync(DateOnly date)
    {
        if (await repository.HasPricesForDateAsync(date))
        {
            logger.LogInformation("Priser för {Date} finns redan.", date.ToString("yyyy-MM-dd"));
            return;
        }

        var year = date.Year;
        var month = date.Month.ToString("00");
        var day = date.Day.ToString("00");

        var url
            = $"https://www.elprisetjustnu.se/api/v1/prices/{year}/{month}-{day}_{PriceClass}.json";

        try
        {
            logger.LogInformation("Hämtar priser från {Url}", url);
            List<Data.ElectricityPrice>? prices
                = await httpClient.GetFromJsonAsync<List<Data.ElectricityPrice>>(url);

            if (prices != null && prices.Any())
            {
                // Validera prisdata före lagring
                ValidatePrices(prices, date);

                // Lagra priser via repository
                await repository.AddPricesAsync(prices);
                logger.LogInformation("Lagrade {Count} priser för {Date}.",
                    prices.Count, date.ToString("yyyy-MM-dd"));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Kunde inte hämta eller lagra priser för {Date}.", date);
            throw; // Omkastning eller hantering? Bakgrundstjänsten bör hantera det.
        }
    }

    /// <summary>
    /// Validerar att prisdata innehåller exakt 96 poster (kvartar per dag) och att alla
    /// poster har TimeStart inom den efterfrågade dagen.
    /// </summary>
    /// <param name="prices">Listan med priser att validera</param>
    /// <param name="date">Det datum som priserna ska tillhöra</param>
    /// <exception cref="InvalidOperationException">Kastas om validering misslyckas</exception>
    private void ValidatePrices(List<Data.ElectricityPrice> prices, DateOnly date)
    {
        // Kontrollera antal priser (96 = 24 timmar * 4 kvartar per timme)
        if (prices.Count != 96)
        {
            logger.LogError("Unexpected number of prices for {Date}: expected 96, got {Count}",
                date.ToString("yyyy-MM-dd"), prices.Count);
            throw new InvalidOperationException(
                $"Expected 96 prices for {date:yyyy-MM-dd}, but got {prices.Count}");
        }

        // Kontrollera att alla priser är inom den efterfrågade dagen
        var startOfDay = date.ToDateTime(TimeOnly.MinValue);
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue);

        foreach (var price in prices)
        {
            if (price.TimeStart < startOfDay || price.TimeStart > endOfDay)
            {
                logger.LogError(
                    "Price with TimeStart {TimeStart} is outside the requested date {Date}",
                    price.TimeStart.ToString("yyyy-MM-dd HH:mm"), date.ToString("yyyy-MM-dd"));
                throw new InvalidOperationException(
                    $"Price with TimeStart {price.TimeStart:yyyy-MM-dd HH:mm} is outside the requested date {date:yyyy-MM-dd}");
            }
        }
    }

    public async Task<List<Data.ElectricityPrice>> GetPricesForDateAsync(DateOnly date)
    {
        return await repository.GetPricesForDateAsync(date);
    }

    public async Task<bool> HasPricesForDateAsync(DateOnly date)
    {
        return await repository.HasPricesForDateAsync(date);
    }

    public async Task DeletePricesForDateAsync(DateOnly date)
    {
        var count = await repository.DeletePricesForDateAsync(date);
        logger.LogInformation("Deleted {Count} prices for {Date}.", count, date);
    }

    public virtual async Task<Data.ElectricityPrice?> GetPriceForDateTimeAsync(DateTime dateTime)
    {
        var requestedDate = DateOnly.FromDateTime(dateTime);

        // Kontrollera cache
        lock (_cacheLock)
        {
            if (_cachedDate == requestedDate && _cachedPrices != null)
            {
                logger.LogDebug("Returning cached price for {DateTime}", dateTime);
                return _cachedPrices.FirstOrDefault(p => p.TimeStart <= dateTime && p.TimeEnd > dateTime);
            }
        }

        // Cache miss - hämta från databas
        var prices = await repository.GetPricesForDateAsync(requestedDate);

        // Uppdatera cache
        lock (_cacheLock)
        {
            _cachedDate = requestedDate;
            _cachedPrices = prices;
        }

        logger.LogDebug("Loaded and cached {Count} prices for {Date}", prices.Count, requestedDate);
        return prices.FirstOrDefault(p => p.TimeStart <= dateTime && p.TimeEnd > dateTime);
    }
}

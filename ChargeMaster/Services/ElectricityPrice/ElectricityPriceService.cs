using ChargeMaster.Data;

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
/// <param name="context">Databaskontext som används för att komma åt och lagra elprisposten.</param>
/// <param name="logger">Loggern som används för att registrera informations- och felmeddelanden relaterade till elpriser.</param>
public class ElectricityPriceService(
    HttpClient httpClient,
    //ApplicationDbContext context,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ElectricityPriceService> logger)
{
    private const string PriceClass = "SE3";

    public async Task FetchAndStorePricesForDateAsync(DateOnly date)
    {
        if (await HasPricesForDateAsync(date))
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

                // Normalisera datum om nödvändigt, även om API vanligtvis skickar ISO8601.
                using var scope = serviceScopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                context.ElectricityPrices.AddRange(prices);
                await context.SaveChangesAsync();
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
        // Compare Date part of TimeStart
        var startOfDay = date.ToDateTime(TimeOnly.MinValue);
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue);

        using var scope = serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.ElectricityPrices
            .Where(p => p.TimeStart >= startOfDay && p.TimeStart <= endOfDay)
            .OrderBy(p => p.TimeStart)
            .ToListAsync();
    }

    public async Task<bool> HasPricesForDateAsync(DateOnly date)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue);
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue);

        using var scope = serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.ElectricityPrices
            .AnyAsync(p => p.TimeStart >= startOfDay && p.TimeStart <= endOfDay);
    }

    public async Task DeletePricesForDateAsync(DateOnly date)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue);
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue);

        using var scope = serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var count = await context.ElectricityPrices
            .Where(p => p.TimeStart >= startOfDay && p.TimeStart <= endOfDay)
            .ExecuteDeleteAsync();

        logger.LogInformation("Deleted {Count} prices for {Date}.", count, date);
    }
}

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
    ApplicationDbContext context,
    ILogger<ElectricityPriceService> logger)
{
    private const string PriceClass = "SE3";

    public async Task FetchAndStorePricesForDateAsync(DateOnly date)
    {
        if (await HasPricesForDateAsync(date))
        {
            logger.LogInformation("Priser för {Date} finns redan.", date);
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
            List<ElectricityPrice>? prices
                = await httpClient.GetFromJsonAsync<List<ElectricityPrice>>(url);

            if (prices != null && prices.Any())
            {
                // Normalisera datum om nödvändigt, även om API vanligtvis skickar ISO8601.
                context.ElectricityPrices.AddRange(prices);
                await context.SaveChangesAsync();
                logger.LogInformation("Lagrade {Count} priser för {Date} har lagrats.",
                    prices.Count, date);
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Kunde inte hämta eller lagra priser för {Date}.", date);
            throw; // Omkastning eller hantering? Bakgrundstjänsten bör hantera det.
        }
    }

    public async Task<List<ElectricityPrice>> GetPricesForDateAsync(DateOnly date)
    {
        // Compare Date part of TimeStart
        var startOfDay = date.ToDateTime(TimeOnly.MinValue);
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue);

        return await context.ElectricityPrices
            .Where(p => p.TimeStart >= startOfDay && p.TimeStart <= endOfDay)
            .OrderBy(p => p.TimeStart)
            .ToListAsync();
    }

    public async Task<bool> HasPricesForDateAsync(DateOnly date)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue);
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue);

        return await context.ElectricityPrices
            .AnyAsync(p => p.TimeStart >= startOfDay && p.TimeStart <= endOfDay);
    }

    public async Task DeletePricesForDateAsync(DateOnly date)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue);
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue);

        var count = await context.ElectricityPrices
            .Where(p => p.TimeStart >= startOfDay && p.TimeStart <= endOfDay)
            .ExecuteDeleteAsync();

        logger.LogInformation("Deleted {Count} prices for {Date}.", count, date);
    }
}

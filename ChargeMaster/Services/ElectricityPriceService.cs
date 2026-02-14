using ChargeMaster.Data;
using Microsoft.EntityFrameworkCore;

namespace ChargeMaster.Services;

public class ElectricityPriceService(HttpClient httpClient, ApplicationDbContext context, ILogger<ElectricityPriceService> logger)
{
    private const string PriceClass = "SE3";

    public async Task FetchAndStorePricesForDateAsync(DateOnly date)
    {
        if (await HasPricesForDateAsync(date))
        {
            logger.LogInformation("Prices for {Date} already exist.", date);
            return;
        }

        var year = date.Year;
        var month = date.Month.ToString("00");
        var day = date.Day.ToString("00");

        var url = $"https://www.elprisetjustnu.se/api/v1/prices/{year}/{month}-{day}_{PriceClass}.json";

        try
        {
            logger.LogInformation("Fetching prices from {Url}", url);
            List<ElectricityPrice>? prices = await httpClient.GetFromJsonAsync<List<ElectricityPrice>>(url);

            if (prices != null && prices.Any())
            {
                // Normalize dates if necessary, though API sends ISO8601 usually.
                context.ElectricityPrices.AddRange(prices);
                await context.SaveChangesAsync();
                logger.LogInformation("Successfully stored {Count} prices for {Date}.", prices.Count, date);
            }
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Failed to fetch or store prices for {Date}.", date);
            throw; // Re-throw or handle? Background service should handle it.
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

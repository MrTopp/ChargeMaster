using ChargeMaster.Data;
using Microsoft.EntityFrameworkCore;

namespace ChargeMaster.Services.ElectricityPrice;

/// <summary>
/// Repository for accessing electricity price data from the database.
/// </summary>
public class ElectricityPriceRepository : IElectricityPriceRepository
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ElectricityPriceRepository> _logger;

    public ElectricityPriceRepository(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ElectricityPriceRepository> logger)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all electricity prices for a specific date.
    /// </summary>
    /// <param name="date">The date to retrieve prices for.</param>
    /// <returns>A list of electricity prices for the specified date, ordered by TimeStart.</returns>
    public async Task<List<Data.ElectricityPrice>> GetPricesForDateAsync(DateOnly date)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue);
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue);

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.ElectricityPrices
            .Where(p => p.TimeStart >= startOfDay && p.TimeStart <= endOfDay)
            .OrderBy(p => p.TimeStart)
            .ToListAsync();
    }

    /// <summary>
    /// Checks if prices exist for a specific date.
    /// </summary>
    /// <param name="date">The date to check.</param>
    /// <returns>True if prices exist for the date, false otherwise.</returns>
    public async Task<bool> HasPricesForDateAsync(DateOnly date)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue);
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue);

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.ElectricityPrices
            .AnyAsync(p => p.TimeStart >= startOfDay && p.TimeStart <= endOfDay);
    }

    /// <summary>
    /// Adds electricity prices to the database.
    /// </summary>
    /// <param name="prices">The prices to add.</param>
    public async Task AddPricesAsync(List<Data.ElectricityPrice> prices)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.ElectricityPrices.AddRange(prices);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes all electricity prices for a specific date.
    /// </summary>
    /// <param name="date">The date to delete prices for.</param>
    /// <returns>The number of prices deleted.</returns>
    public async Task<int> DeletePricesForDateAsync(DateOnly date)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue);
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue);

        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.ElectricityPrices
            .Where(p => p.TimeStart >= startOfDay && p.TimeStart <= endOfDay)
            .ExecuteDeleteAsync();
    }
}

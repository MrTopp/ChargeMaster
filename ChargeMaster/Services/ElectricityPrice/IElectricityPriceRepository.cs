namespace ChargeMaster.Services.ElectricityPrice;

/// <summary>
/// Abstracts database access for electricity price data.
/// </summary>
public interface IElectricityPriceRepository
{
    /// <summary>
    /// Gets all electricity prices for a specific date.
    /// </summary>
    /// <param name="date">The date to retrieve prices for.</param>
    /// <returns>A list of electricity prices for the specified date, ordered by TimeStart.</returns>
    Task<List<Data.ElectricityPrice>> GetPricesForDateAsync(DateOnly date);

    /// <summary>
    /// Checks if prices exist for a specific date.
    /// </summary>
    /// <param name="date">The date to check.</param>
    /// <returns>True if prices exist for the date, false otherwise.</returns>
    Task<bool> HasPricesForDateAsync(DateOnly date);

    /// <summary>
    /// Adds electricity prices to the database.
    /// </summary>
    /// <param name="prices">The prices to add.</param>
    Task AddPricesAsync(List<Data.ElectricityPrice> prices);

    /// <summary>
    /// Deletes all electricity prices for a specific date.
    /// </summary>
    /// <param name="date">The date to delete prices for.</param>
    /// <returns>The number of prices deleted.</returns>
    Task<int> DeletePricesForDateAsync(DateOnly date);
}

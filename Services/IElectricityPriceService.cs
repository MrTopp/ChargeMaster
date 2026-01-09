using ChargeMaster.Data;

namespace ChargeMaster.Services;

public interface IElectricityPriceService
{
    Task FetchAndStorePricesForDateAsync(DateOnly date);
    Task<List<ElectricityPrice>> GetPricesForDateAsync(DateOnly date);
    Task<bool> HasPricesForDateAsync(DateOnly date);
}

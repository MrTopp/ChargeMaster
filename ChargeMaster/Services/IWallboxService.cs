using ChargeMaster.Models;

namespace ChargeMaster.Services;

public interface IWallboxService
{
    Task<WallboxStatus?> GetStatusAsync();
    Task<DateTime?> GetTimeAsync();
    Task<bool> SetTimeAsync(DateTime dateTime);
    Task<bool> SetModeAsync(WallboxMode mode);
    Task<WallboxMeterInfo?> GetMeterInfoAsync();
    Task<IReadOnlyList<WallboxSchemaEntry>?> GetSchemaAsync();
    Task<WallboxConfig?> GetConfigAsync();
    Task<IReadOnlyList<WallboxSlaveConfig>?> GetSlavesAsync();
    Task<bool> SetSchemaAsync(WallboxSchemaEntry schemaEntry);
    Task<bool> DeleteSchemaAsync(int schemaId);
}

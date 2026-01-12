using ChargeMaster.Models;

namespace ChargeMaster.Services;

public interface IWallboxService
{
    Task<WallboxStatus?> GetStatusAsync();
}
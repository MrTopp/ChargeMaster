namespace ChargeMaster.Workers;

/// <summary>
/// Represents hourly energy usage data.
/// </summary>
public record HourlyEnergyUsage(DateTime Hour, long EnergyUsageWh);

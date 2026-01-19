namespace ChargeMaster.Models;

public sealed class VWStatusResponse
{
    public VWStatus? Status { get; set; }
}

public sealed class VWStatus
{
    public string? Name { get; set; }
    public string? Vin { get; set; }
    public DateTimeOffset? ChargingEstimatedDateReached { get; set; }
    public double ChargingPower { get; set; }
    public double ChargingRate { get; set; }
    public double ChargingSettingsMaximumCurrent { get; set; }
    public double ChargingSettingsTargetLevel { get; set; }
    public double BatteryLevel { get; set; }
    public double BatteryRange { get; set; }
    public double BatteryRangeEstimatedFull { get; set; }
    public DateTimeOffset? InspectionDueAt { get; set; }
    public double Odometer { get; set; }
    public string? Position { get; set; }
}

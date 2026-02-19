namespace ChargeMaster.Services.VolksWagen;

public sealed class VWVehiclesResponse
{
    public IReadOnlyList<VWVehicle>? Vehicles { get; set; }
}

public sealed class VWVehicle
{
    public string? Name { get; set; }
    public string? Vin { get; set; }
}

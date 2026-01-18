namespace ChargeMaster.Models;

public sealed class WallboxSchemaEntry
{
    public int SchemaId { get; set; }
    public string? Start { get; set; }
    public string? Stop { get; set; }
    public int Weekday { get; set; }
    public int ChargeLimit { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChargeMaster.Data;

public class WallboxMeterReading
{
    [Key]
    public int Id { get; set; }

    // The time reported by the meter (converted from milliseconds since epoch)
    public DateTime ReadAt { get; set; }

    // Raw JSON returned by the meter endpoint
    public string RawJson { get; set; } = string.Empty;

    // Convenience fields for queries
    public double AccEnergy { get; set; }
    public string? MeterSerial { get; set; }
    public double ApparentPower { get; set; }
}

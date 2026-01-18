using System.ComponentModel.DataAnnotations;

namespace ChargeMaster.Data;

/// <summary>
/// Represents a reading from a wallbox meter.
/// </summary>
public class WallboxMeterReading
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The time reported by the meter (converted from milliseconds since epoch)
    /// </summary>
    public DateTime ReadAt { get; set; }

    /// <summary>
    /// Raw JSON returned by the meter endpoint
    /// </summary>
    [MaxLength(1000)]
    public string RawJson { get; set; } = string.Empty;

    /// <summary>
    /// Convenience fields for queries
    /// </summary>
    public long AccEnergy { get; set; }

    /// <summary>
    /// Gets or sets the serial number of the meter.
    /// </summary>
    [MaxLength(100)]
    public string? MeterSerial { get; set; }


    public long ApparentPower { get; set; }
}

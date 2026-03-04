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
    /// Total accumulated energy in watt-hours (Wh) as reported by the meter.
    /// </summary>
    public long AccEnergy { get; set; }

    /// <summary>
    /// Serial number of the meter.
    /// </summary>
    [MaxLength(100)]
    public string? MeterSerial { get; set; }

    /// <summary>
    /// Aktuell strömförbrukning i kW rapporterat av wallbox.
    /// </summary>
    public long ApparentPower { get; set; }

}

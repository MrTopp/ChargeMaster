using System.ComponentModel.DataAnnotations;

namespace ChargeMaster.Data;

/// <summary>
/// Entity Framework-modell för lagring av temperaturmätningar från Shelly-enheter.
/// </summary>
public class ShellyTemperature
{
    /// <summary>
    /// Primärnyckel.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// ID för Shelly-enheten som skickade mätningen (t.ex. "sovrum", "hall", "arbetsrum").
    /// </summary>
    [MaxLength(30)]
    public required string DeviceId { get; set; }

    /// <summary>
    /// Temperaturen i Celsius.
    /// </summary>
    public double TemperatureCelsius { get; set; }

    /// <summary>
    /// Tidpunkten då temperaturen mättes.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ChargeMaster.Data;

/// <summary>
/// Elpriset för en kvart.
/// </summary>
public class ElectricityPrice
{
    public int Id { get; set; }

    [JsonPropertyName("SEK_per_kWh")]
    public decimal SekPerKwh { get; set; }

    [JsonPropertyName("EUR_per_kWh")]
    public decimal EurPerKwh { get; set; }

    [JsonPropertyName("EXR")]
    public decimal ExchangeRate { get; set; }

    [JsonPropertyName("time_start")]
    public DateTime TimeStart { get; set; }

    [JsonPropertyName("time_end")]
    public DateTime TimeEnd { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether charging is currently permitted based on the configured schedule and
    /// manual override.
    /// </summary>
    /// <remarks>Charging is not allowed on weekdays between 07:00 and 19:00 from November through March.
    /// Outside of these periods, the value reflects the most recent value set. Setting this property manually overrides
    /// the default schedule until changed again.</remarks>
    [NotMapped]
    public bool ChargingAllowed
    {
        get
        {
            // Ladda inte mellan november och mars vardagar 07-19
            if (TimeStart.Month is >= 11 or <= 3)
            {
                if (TimeStart.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday)
                {
                    if (TimeStart.Hour is >= 7 and < 19)
                    {
                        return false;
                    }
                }
            }
            return _chargingAllowed ?? true;
        }
        set => _chargingAllowed = value;
    }
    [NotMapped]
    private bool? _chargingAllowed;
}

using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ChargeMaster.Services.ElectricityPrice;

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
    /// Hämtar eller ställer in ett värde som anger om laddning är tillåten baserat på schemat och
    /// manuell åsidosättning.
    /// </summary>
    /// <remarks>Laddning är inte tillåten vardagar mellan 07:00 och 19:00 från november till mars.
    /// Utanför dessa perioder reflekterar värdet det senaste värdet som ställdes in. Att manuellt ställa denna egenskap åsidosätter
    /// standardschemat tills det ändras igen.</remarks>
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

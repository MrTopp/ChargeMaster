using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace ChargeMaster.Data;

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


    [NotMapped]
    public bool ChargingAllowed
    {
        get
        {
            if (TimeStart.Month >= 11 || TimeStart.Month <= 3)
            {
                if(TimeStart.Hour >= 7 && TimeStart.Hour < 19)
                {
                    return false;
                }
            }
            return true;
        }
    }
}

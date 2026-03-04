using System.Text.Json.Serialization;

namespace ChargeMaster.Services.Wallbox;

public class WallboxMeterInfo
{
    [JsonPropertyName("success")]
    public int Success { get; set; }

    /// <summary>
    /// Ackumulerad förbrukad energi i Wh
    /// </summary>
    [JsonPropertyName("accEnergy")]
    public long AccEnergy { get; set; }

    /// <summary>
    /// Aktuell effekt i tiondels ampere för fas 1
    /// </summary>
    [JsonPropertyName("phase1Current")]
    public double Phase1Current { get; set; }

    /// <summary>
    /// Aktuell effekt i tiondels ampere för fas 2
    /// </summary>
    [JsonPropertyName("phase2Current")]
    public double Phase2Current { get; set; }

    /// <summary>
    /// Aktuell effekt i tiondels ampere för fas 3
    /// </summary>
    [JsonPropertyName("phase3Current")]
    public double Phase3Current { get; set; }

    /// <summary>
    /// Effekt i kW fas 1
    /// </summary>
    [JsonPropertyName("phase1InstPower")]
    public double Phase1InstPower { get; set; }

    /// <summary>
    /// Effekt i kW fas 2
    /// </summary>
    [JsonPropertyName("phase2InstPower")]
    public double Phase2InstPower { get; set; }

    /// <summary>
    /// Effekt i kW fas 3
    /// </summary>
    [JsonPropertyName("phase3InstPower")]
    public double Phase3InstPower { get; set; }

    [JsonPropertyName("readTime")]
    public long ReadTime { get; set; }

    [JsonPropertyName("gridNetType")]
    public string? GridNetType { get; set; }

    [JsonPropertyName("meterSerial")]
    public string? MeterSerial { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("apparentPower")]
    public long ApparentPower { get; set; }

    /// <summary>
    /// Momentan effektförbrukning i W för fas 1
    /// </summary>
    public long Phase1CurrentEnergy =>
        (long)(Phase1Current*230/10);

    /// <summary>
    /// Momentan effektförbrukning i W för fas 2
    /// </summary>
    public long Phase2CurrentEnergy =>
        (long)(Phase2Current * 230 / 10);

    /// <summary>
    /// Momentan effektförbrukning i W för fas 3
    /// </summary>
    public long Phase3CurrentEnergy =>
        (long)(Phase3Current * 230 / 10);

    /// <summary>
    /// Momentan effektförbrukning i W för alla faser
    /// </summary>
    public long CurrentEnergy =>
        Phase1CurrentEnergy + Phase2CurrentEnergy + Phase3CurrentEnergy;

    /// <summary>
    /// Ackumulerad effektförbrukning i W för alla faser denna timme
    /// </summary>
    public double EffektTimmeNu { get; set; }

    public string EffektTimmeNuFormatted =>
        EffektTimmeNu > 0 ? EffektTimmeNu.ToString("F0") : "-";

    /// <summary>
    /// Uppskattad effektförbrukning för innevarande timme
    /// </summary>
    public double EffektTimmeTotal { get; set; }

    public string EffektTimmeTotalFormatted =>
        EffektTimmeTotal > 0 ? EffektTimmeTotal.ToString("F0") : "-";

}

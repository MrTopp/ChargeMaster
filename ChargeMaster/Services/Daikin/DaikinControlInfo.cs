namespace ChargeMaster.Services.Daikin;

/// <summary>
/// Styrinformation från Daikin-enhetens /aircon/get_control_info-slutpunkt.
/// </summary>
public class DaikinControlInfo
{
    /// <summary>
    /// Ström på/av (0 = av, 1 = på).
    /// </summary>
    public int Power { get; set; }

    /// <summary>
    /// Driftläge (0/1/7=Auto, 2=Torkning, 3=Kylning, 4=Uppvärmning, 6=Fläkt).
    /// </summary>
    public int Mode { get; set; }

    /// <summary>
    /// Börvärdestemperatur i °C.
    /// </summary>
    /// <remarks>
    /// 10.0–41.0, M=no temp, --=N/A
    /// </remarks>
    public double? TargetTemperature { get; set; }

    /// <summary>
    /// Målnivå för fuktighet.
    /// </summary>
    public string? TargetHumidity { get; set; }

    /// <summary>
    /// Fläkthastighet (A=Auto, B=Tyst, 3-7=Hastighet).
    /// </summary>
    public string? FanRate { get; set; }

    /// <summary>
    /// Fläktriktning (0=Av, 1=Vertikal, 2=Horisontell, 3=Båda).
    /// </summary>
    public int FanDirection { get; set; }

    /// <summary>
    /// Alertkod. 255=no alert
    /// </summary>
    public int? Alert { get; set; }

    /// <summary>
    /// Returnerar en läsbar beskrivning av driftläget.
    /// </summary>
    public string ModeDescription => Mode switch
    {
        0 or 1 or 7 => "Auto",
        2 => "Torkning",
        3 => "Kylning",
        4 => "Uppvärmning",
        6 => "Fläkt",
        _ => $"Okänt ({Mode})"
    };

    /// <summary>
    /// Returnerar om enheten är påslagen.
    /// </summary>
    public bool IsOn => Power == 1;
}

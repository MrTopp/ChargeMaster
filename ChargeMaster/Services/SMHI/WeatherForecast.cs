namespace ChargeMaster.Services.SMHI;

/// <summary>
/// Väderprognos för närmaste 12 timmarna.
/// </summary>
public class WeatherForecast
{
    /// <summary>
    /// Tid för prognosen
    /// </summary>
    public DateTime Time { get; set; }

    /// <summary>
    /// Aktuell temperatur (°C)
    /// </summary>
    public double Temperature { get; set; }

    /// <summary>
    /// Molnighet (0-100%)
    /// </summary>
    public double? CloudCoverage { get; set; }

    /// <summary>
    /// Nederbördsmängd (mm)
    /// </summary>
    public double? Precipitation { get; set; }

    /// <summary>
    /// Vindhastighet (m/s)
    /// </summary>
    public double? WindSpeed { get; set; }

    /// <summary>
    /// Vindriktning (grader)
    /// </summary>
    public int? WindDirection { get; set; }

    /// <summary>
    /// Luftfuktighet (%)
    /// </summary>
    public int? Luftfuktighet { get; set; }

    /// <summary>
    /// Lufttryck (hPa)
    /// </summary>
    public double? Lufttryck { get; set; }

    /// <summary>
    /// Sikt (km)
    /// </summary>
    public double? Sikt { get; set; }

    /// <summary>
    /// Max nederbörd (mm/3h)
    /// </summary>
    public double? MaxPrecipitation { get; set; }

    /// <summary>
    /// Medel nederbörd (mm/3h)
    /// </summary>
    public double? MeanPrecipitation { get; set; }

    /// <summary>
    /// Vindbyar (m/s)
    /// </summary>
    public double? WindGust { get; set; }
}

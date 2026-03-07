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
    /// Upplevd temperatur (°C)
    /// </summary>
    public double FeelsLike { get; set; }

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
}

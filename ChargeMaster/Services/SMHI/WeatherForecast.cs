using System.ComponentModel.DataAnnotations.Schema;

namespace ChargeMaster.Services.SMHI;

/// <summary>
/// Väderprognos för närmaste 12 timmarna.
/// </summary>
public class WeatherForecast
{
    /// <summary>
    /// Primär nyckel för databasen
    /// </summary>
    public int Id { get; set; }

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

    /// <summary>
    /// Åskornadsannolikhet (%)
    /// </summary>
    public int? ThunderstormProbability { get; set; }

    /// <summary>
    /// Mediannedbörds (kg/m²/h)
    /// </summary>
    public double? PrecipitationMedian { get; set; }

    /// <summary>
    /// Nederbördsannolikhet (%)
    /// </summary>
    public int? PrecipitationProbability { get; set; }

    /// <summary>
    /// Nederbördskategori (1-7)
    /// </summary>
    public int? PrecipitationCategory { get; set; }

    /// <summary>
    /// Vädsymbol för visualisering (1-27)
    /// </summary>
    public int? WeatherSymbol { get; set; }

    /// <summary>
    /// Total niederbörd (kg/m²)
    /// </summary>
    public double? TotalPrecipitation { get; set; }

    [NotMapped]
    public string WeatherDescription
    {
        get
        {
            if (WeatherSymbol == null) return "Okänd vädertyp";
            return WeatherSymbol switch
            {
                1 => "Klart väder",
                2 => "Nästan klart",
                3 => "Växlande molnighet",
                4 => "Halv molnigt",
                5 => "Molnigt",
                6 => "Mulet",
                7 => "Dimma",
                8 => "Lätt regnskur",
                9 => "Måttlig regnskur",
                10 => "Kraftig regnskur",
                11 => "Åskväder",
                12 => "Lätt snöblandat regn",
                13 => "Måttligt snöblandat regn",
                14 => "Kraftigt snöblandat regn",
                15 => "Lätt snöfall",
                16 => "Måttligt snöfall",
                17 => "Kraftigt snöfall",
                18 => "Regn",
                19 => "Åska",
                20 => "Snöblandat regn",
                21 => "Snö",
                22 => "Regn och åska",
                23 => "Snöblandat regn och åska",
                24 => "Snö och åska",
                25 => "Lätt regn och snö",
                26 => "Måttligt/kraftigt regn och snö",
                27 => "Hagelkorn",
                _ => "Okänd vädertyp"
            };
        }
    }

    [NotMapped]
    public string WeatherEmoji
    {
        get
        {
            if (WeatherSymbol == null) return "❓";
            return WeatherSymbol switch
            {
                1 => "☀️",    // Klart väder
                2 => "🌤️",   // Nästan klart
                3 => "⛅",    // Växlande molnighet
                4 => "🌥️",   // Halv molnigt
                5 => "☁️",    // Molnigt
                6 => "🌧️",   // Mulet
                7 => "🌫️",   // Dimma
                8 => "🌦️",   // Lätt regnskur
                9 => "🌧️",   // Måttlig regnskur
                10 => "⛈️",   // Kraftig regnskur
                11 => "⛈️",   // Åskväder
                12 => "🌧️",   // Lätt snöblandat regn
                13 => "🌧️",   // Måttligt snöblandat regn
                14 => "⛈️",   // Kraftigt snöblandat regn
                15 => "🌨️",   // Lätt snöfall
                16 => "❄️",    // Måttligt snöfall
                17 => "❄️",    // Kraftigt snöfall
                18 => "🌧️",   // Regn
                19 => "⚡",    // Åska
                20 => "🌨️",   // Snöblandat regn
                21 => "❄️",    // Snö
                22 => "⛈️",   // Regn och åska
                23 => "⛈️",   // Snöblandat regn och åska
                24 => "⛈️",   // Snö och åska
                25 => "🌨️",   // Lätt regn och snö
                26 => "🌨️",   // Måttligt/kraftigt regn och snö
                27 => "🧊",    // Hagelkorn
                _ => "❓"
            };
        }
    }
}

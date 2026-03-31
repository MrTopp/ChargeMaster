using System.Text.Json.Serialization;

namespace ChargeMaster.Services.SMHI;

/// <summary>
/// SMHI API response för väderprognos.
/// </summary>
public class SmhiWeatherResponse
{
    public List<SmhiTimeSeries>? TimeSeries { get; set; }
}

/// <summary>
/// En tidpunkt med väderdata från SMHI.
/// </summary>
public class SmhiTimeSeries
{
    public DateTime Time { get; set; }

    public SmhiTimeSeriesData? Data { get; set; }
}

/// <summary>
/// Väderdata för en tidpunkt från SMHI.
/// </summary>
public class SmhiTimeSeriesData
{
    [JsonPropertyName("air_temperature")]
    public double? AirTemperature { get; set; }

    [JsonPropertyName("wind_from_direction")]
    public double? WindFromDirection { get; set; }

    [JsonPropertyName("wind_speed")]
    public double? WindSpeed { get; set; }

    [JsonPropertyName("wind_speed_of_gust")]
    public double? WindSpeedOfGust { get; set; }

    [JsonPropertyName("relative_humidity")]
    public double? RelativeHumidity { get; set; }

    [JsonPropertyName("air_pressure_at_mean_sea_level")]
    public double? AirPressureAtMeanSeaLevel { get; set; }

    [JsonPropertyName("visibility_in_air")]
    public double? VisibilityInAir { get; set; }

    [JsonPropertyName("thunderstorm_probability")]
    public double? ThunderstormProbability { get; set; }

    [JsonPropertyName("cloud_area_fraction")]
    public double? CloudAreaFraction { get; set; }

    [JsonPropertyName("precipitation_amount_mean")]
    public double? PrecipitationAmountMean { get; set; }

    [JsonPropertyName("precipitation_amount_min")]
    public double? PrecipitationAmountMin { get; set; }

    [JsonPropertyName("precipitation_amount_max")]
    public double? PrecipitationAmountMax { get; set; }

    [JsonPropertyName("precipitation_amount_median")]
    public double? PrecipitationAmountMedian { get; set; }

    [JsonPropertyName("probability_of_precipitation")]
    public double? ProbabilityOfPrecipitation { get; set; }

    [JsonPropertyName("predominant_precipitation_type_at_surface")]
    public double? PredominantPrecipitationTypeAtSurface { get; set; }

    [JsonPropertyName("symbol_code")]
    public double? SymbolCode { get; set; }

    [JsonPropertyName("precipitation_amount_mean_deterministic")]
    public double? PrecipitationAmountMeanDeterministic { get; set; }

    [JsonPropertyName("probability_of_frozen_precipitation")]
    public double? ProbabilityOfFrozenPrecipitation { get; set; }
}

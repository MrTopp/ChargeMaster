namespace ChargeMaster.Services.Weather;

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
    [System.Text.Json.Serialization.JsonPropertyName("validTime")]
    public DateTime Time { get; set; }

    public List<SmhiParameter>? Parameters { get; set; }
}

/// <summary>
/// En väderparameter från SMHI.
/// </summary>
public class SmhiParameter
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("values")]
    public List<double>? Values { get; set; }
}

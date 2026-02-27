namespace ChargeMaster.Services.Daikin;

/// <summary>
/// Sensordata från Daikin-enhetens /aircon/get_sensor_info-slutpunkt.
/// </summary>
public class DaikinSensorInfo
{
    /// <summary>
    /// Inomhustemperatur i °C.
    /// </summary>
    public double? IndoorTemperature { get; set; }

    /// <summary>
    /// Utomhustemperatur i °C.
    /// </summary>
    public double? OutdoorTemperature { get; set; }

    /// <summary>
    /// Inomhusfuktighet i %.
    /// </summary>
    public double? IndoorHumidity { get; set; }

    /// <summary>
    /// Kompressorfrekvens. 999 = vilande/av, 0 = av.
    /// </summary>
    public int? CompressorFrequency { get; set; }

    /// <summary>
    /// Felkod (0 = inget fel).
    /// </summary>
    public int? ErrorCode { get; set; }
}

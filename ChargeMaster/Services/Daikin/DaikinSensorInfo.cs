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
    /// Kompressorfrekvens (om tillgänglig).
    /// </summary>
    public double? CompressorFrequency { get; set; }
}

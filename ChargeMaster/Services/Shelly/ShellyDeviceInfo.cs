namespace ChargeMaster.Services.Shelly;

/// <summary>
/// Information om en Shelly-enhet som lästs från MQTT.
/// </summary>
public class ShellyDeviceInfo
{
    /// <summary>
    /// Enhets-ID eller namn.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Enhetens typ (t.ex. "Shelly1", "ShellySwitch", "ShellyPlus").
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Firmware-version.
    /// </summary>
    public string? FirmwareVersion { get; set; }

    /// <summary>
    /// MQTT-ämnets basväg för denna enhet.
    /// </summary>
    public string? BaseTopic { get; set; }

    /// <summary>
    /// Senaste uppdateringstidpunkt.
    /// </summary>
    public DateTime LastUpdate { get; set; }

    /// <summary>
    /// Relästatus (för releströmbrytare).
    /// Key: Reläindex, Value: true = på, false = av.
    /// </summary>
    public Dictionary<int, bool> RelayStates { get; set; } = [];

    /// <summary>
    /// Kraft-/energimätningar.
    /// Key: "power", "energy", "voltage", Value: värdet som sträng.
    /// </summary>
    public Dictionary<string, string> PowerMetrics { get; set; } = [];

    /// <summary>
    /// Temperaturer från sensorer.
    /// Key: Sensor-ID, Value: Temperatur i °C.
    /// </summary>
    public Dictionary<string, double> Temperatures { get; set; } = [];

    /// <summary>
    /// Fuktighet från sensorer.
    /// Key: Sensor-ID, Value: Fuktighet i %.
    /// </summary>
    public Dictionary<string, double> Humidity { get; set; } = [];
}

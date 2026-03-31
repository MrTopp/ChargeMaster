namespace ChargeMaster.Services.Shelly;

/// <summary>
/// Event arguments för när temperaturen uppdateras från en Shelly-enhet.
/// </summary>
public class ShellyTemperatureChangedEventArgs(string deviceId, double temperature) : EventArgs
{
    /// <summary>
    /// ID för enheten som skickade temperaturmätningen.
    /// </summary>
    public string DeviceId { get; } = deviceId;

    /// <summary>
    /// Temperaturen i Celsius.
    /// </summary>
    public double TemperatureCelsius { get; } = temperature;

    /// <summary>
    /// Tidpunkten då temperaturen uppdaterades.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.Now;
}

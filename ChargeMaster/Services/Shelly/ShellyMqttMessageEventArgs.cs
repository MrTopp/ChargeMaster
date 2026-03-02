namespace ChargeMaster.Services.Shelly;

/// <summary>
/// Eventargument för MQTT-meddelanden från Shelly-enheter.
/// </summary>
public class ShellyMqttMessageEventArgs : EventArgs
{
    /// <summary>
    /// MQTT-ämnet som meddelandet kom från.
    /// </summary>
    public required string Topic { get; set; }

    /// <summary>
    /// Meddelandets innehål.
    /// </summary>
    public required string Payload { get; set; }

    /// <summary>
    /// Tidpunkt när meddelandet mottogs.
    /// </summary>
    public DateTime Timestamp { get; set; }
}

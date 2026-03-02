namespace ChargeMaster.Services.Shelly;

/// <summary>
/// Eventargument för MQTT-anslutningsförändringar.
/// </summary>
public class ShellyMqttConnectionStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Anger om tjänsten är ansluten till MQTT-servern.
    /// </summary>
    public required bool IsConnected { get; set; }

    /// <summary>
    /// Tidpunkt när anslutningsförändringen inträffade.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Anledning till anslutningsförändringen (när den är kopplad ifrån).
    /// </summary>
    public string? Reason { get; set; }
}

namespace ChargeMaster.Services.Shelly;

/// <summary>
/// Event arguments för när MQTT-anslutningen förändras.
/// </summary>
public class ShellyConnectionChangedEventArgs(bool isConnected) : EventArgs
{
    /// <summary>
    /// Sant om ansluten till MQTT-servern, falskt om frånkopplad.
    /// </summary>
    public bool IsConnected { get; } = isConnected;

    /// <summary>
    /// Tidpunkten då anslutningen förändrades.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.Now;
}

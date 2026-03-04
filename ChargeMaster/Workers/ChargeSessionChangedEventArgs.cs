using ChargeMaster.Services.Wallbox;

namespace ChargeMaster.Workers;

/// <summary>
/// Event arguments containing the previous charging session data when a new session starts.
/// </summary>
public class ChargeSessionChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the previous charging session data before the session change.
    /// </summary>
    public WallboxSessionData? PreviousSessionData { get; }

    /// <summary>
    /// Gets the timestamp when the change was detected.
    /// </summary>
    public DateTime Timestamp { get; } = DateTime.Now;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChargeSessionChangedEventArgs"/> class.
    /// </summary>
    /// <param name="previousSessionData">The session data from the previous reading.</param>
    public ChargeSessionChangedEventArgs(WallboxSessionData? previousSessionData)
    {
        PreviousSessionData = previousSessionData;
    }
}

namespace ChargeMaster.Services.Wallbox;

/// <summary>
/// Represents a charging session record extracted from WallboxStatus.MainCharger.
/// Contains session energy consumption and timing information.
/// </summary>
public class WallboxSessionData
{
    public WallboxSessionData(double accSessionEnergy, double sessionStartValue, long accSessionMillis, long? sessionStartTime)
    {
        AccSessionEnergy = accSessionEnergy;
        SessionStartValue = sessionStartValue;
        AccSessionMillis = accSessionMillis;
        SessionStartTime = sessionStartTime;
    }

    /// <summary>
    /// Accumulated energy for the current charging session in Wh.
    /// </summary>
    public double AccSessionEnergy { get; set; }

    /// <summary>
    /// Starting energy value when the session began in Wh.
    /// </summary>
    double SessionStartValue { get; set; }

    /// <summary>
    /// Duration of the charging session in milliseconds.
    /// </summary>
    public long AccSessionMillis { get; set; } = 0;

    /// <summary>
    /// Timestamp when the session started (Unix timestamp in seconds).
    /// </summary>
    public long? SessionStartTime { get; set; }
}
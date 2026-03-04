using System.ComponentModel.DataAnnotations.Schema;

namespace ChargeMaster.Services.Wallbox;

/// <summary>
/// Represents a charging session record extracted from WallboxStatus.MainCharger.
/// Contains session energy consumption and timing information.
/// </summary>
public class ChargeSessionData
{
    public ChargeSessionData(int? accSessionEnergy, long? sessionStartValue, long? accSessionMillis, long? sessionStartTime)
    {
        AccSessionEnergy = accSessionEnergy;
        SessionStartValue = sessionStartValue;
        AccSessionMillis = accSessionMillis;
        SessionStartTime = sessionStartTime;
    }

    /// <summary>
    /// Accumulated energy for the current charging session in Wh.
    /// </summary>
    public int? AccSessionEnergy { get; set; }

    /// <summary>
    /// Starting energy value when the session began in Wh.
    /// </summary>
    public long? SessionStartValue { get; set; }

    /// <summary>
    /// Duration of the charging session in milliseconds.
    /// </summary>
    public long? AccSessionMillis { get; set; }
    /// <summary>
    /// Timestamp when the session started (Unix timestamp in seconds).
    /// </summary>
    public long? SessionStartTime { get; set; }

    [NotMapped]
    public bool HasData=> AccSessionEnergy.HasValue && SessionStartValue.HasValue && AccSessionMillis.HasValue && SessionStartTime.HasValue;

}
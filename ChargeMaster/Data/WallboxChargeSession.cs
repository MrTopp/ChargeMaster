namespace ChargeMaster.Data;

/// <summary>
/// Entity Framework model for storing Wallbox charging session data.
/// </summary>
public class WallboxChargeSession
{
    /// <summary>
    /// Gets or sets the unique identifier for the charge session record.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the session data was recorded.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the energy consumed during this session in Wh.
    /// </summary>
    public int SessionEnergy { get; set; }

    /// <summary>
    /// Gets or sets the starting energy value for the session in Wh.
    /// </summary>
    public long SessionStartValue { get; set; }

    /// <summary>
    /// Gets or sets the Unix timestamp when the charging session started (in seconds).
    /// </summary>
    public long SessionStartTime { get; set; }

    /// <summary>
    /// Gets or sets the current charge level percentage (0-100).
    /// </summary>
    public int ChargeLevel { get; set; }

    /// <summary>
    /// Gets or sets the target charge level percentage (0-100).
    /// </summary>
    public int ChargeTarget { get; set; }

    /// <summary>
    /// Gets or sets the current state of the charging session (e.g., "CHARGING", "IDLE", "FINISHED").
    /// </summary>
    public string ChargeState { get; set; } = string.Empty;
}

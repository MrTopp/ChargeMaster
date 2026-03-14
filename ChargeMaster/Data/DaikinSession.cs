namespace ChargeMaster.Data;

/// <summary>
/// Entity Framework model for storing Daikin heat pump session data.
/// </summary>
public class DaikinSession
{
    /// <summary>
    /// Gets or sets the unique identifier for the Daikin session record.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the session data was recorded.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the target temperature set on the Daikin heat pump in °C.
    /// </summary>
    public double TargetTemperature { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether heating (true) or cooling (false) mode is active.
    /// </summary>
    public bool IsHeating { get; set; }

    /// <summary>
    /// Gets or sets the temperature measured in the office room (arbetsrum) in °C.
    /// </summary>
    public double? ArbetsrumTemperature { get; set; }

    /// <summary>
    /// Gets or sets the temperature measured in the hallway (hall) in °C.
    /// </summary>
    public double? HallTemperature { get; set; }

    /// <summary>
    /// Gets or sets the temperature measured in the bedroom (sovrum) in °C.
    /// </summary>
    public double? SovrumTemperature { get; set; }

    /// <summary>
    /// Gets or sets the current electricity price in SEK per kWh.
    /// </summary>
    public decimal? CurrentPrice { get; set; }
}

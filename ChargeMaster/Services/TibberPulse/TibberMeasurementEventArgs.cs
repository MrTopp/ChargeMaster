namespace ChargeMaster.Services.TibberPulse;

/// <summary>
/// Eventargument som innehåller mätdata från Tibber Pulse.
/// </summary>
public class TibberMeasurementEventArgs(TibberLiveMeasurement measurement) : EventArgs
{
    /// <summary>
    /// Mätdata från Tibber Pulse.
    /// </summary>
    public TibberLiveMeasurement Measurement { get; } = measurement;
}

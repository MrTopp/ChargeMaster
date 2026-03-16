using Tibber.Sdk;

namespace ChargeMaster.Services.TibberPulse;

/// <summary>
/// Eventargument som innehåller mätdata från Tibber Pulse.
/// </summary>
public class TibberMeasurementEventArgs(RealTimeMeasurement measurement) : EventArgs
{
    /// <summary>
    /// Mätdata från Tibber Pulse.
    /// </summary>
    public RealTimeMeasurement Measurement { get; } = measurement;
}

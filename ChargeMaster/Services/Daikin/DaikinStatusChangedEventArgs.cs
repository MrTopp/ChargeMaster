namespace ChargeMaster.Services.Daikin;

/// <summary>
/// Eventargument för när Daikin-status ändras.
/// </summary>
public class DaikinStatusChangedEventArgs : EventArgs
{
    /// <summary>
    /// Anger om innetemperaturen ändrades.
    /// </summary>
    public bool CurrentTemperatureChanged { get; set; }

    /// <summary>
    /// Anger om utetemperaturen ändrades.
    /// </summary>
    public bool OutdoorTemperatureChanged { get; set; }

    /// <summary>
    /// Anger om måltemperaturen ändrades.
    /// </summary>
    public bool TargetTemperatureChanged { get; set; }

    /// <summary>
    /// Anger om strömstatus ändrades.
    /// </summary>
    public bool PowerChanged { get; set; }

    /// <summary>
    /// Anger om läget ändrades.
    /// </summary>
    public bool ModeChanged { get; set; }

    /// <summary>
    /// Anger om kompressorfrekvensen ändrades.
    /// </summary>
    public bool CompressorFrequencyChanged { get; set; }

    /// <summary>
    /// Tid när statusändringen registrerades.
    /// </summary>
    public DateTime ChangedAt { get; } = DateTime.Now;

    /// <summary>
    /// Returnerar true om någon status ändrades.
    /// </summary>
    public bool HasChanges =>
        CurrentTemperatureChanged ||
        OutdoorTemperatureChanged ||
        TargetTemperatureChanged ||
        PowerChanged ||
        ModeChanged ||
        CompressorFrequencyChanged;
}

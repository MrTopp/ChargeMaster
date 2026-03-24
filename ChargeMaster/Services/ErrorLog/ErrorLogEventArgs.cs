namespace ChargeMaster.Services.ErrorLog;

/// <summary>
/// Event args för loggade fel.
/// </summary>
public class ErrorLogEventArgs : EventArgs
{
    /// <summary>
    /// Tidpunkt då felet loggades.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Felmeddelandet.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Exception om det finns en.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Källan/loggern som skickade felet.
    /// </summary>
    public string Source { get; set; } = string.Empty;
}

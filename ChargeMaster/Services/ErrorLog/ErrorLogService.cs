namespace ChargeMaster.Services.ErrorLog;

/// <summary>
/// Service för att fånga och publicera LogError-meddelanden.
/// </summary>
public class ErrorLogService
{
    private const int MaxErrors = 50; // Behålla senaste 50 fel
    private readonly List<ErrorLogEventArgs> _errors = [];

    /// <summary>
    /// Event som höjs när ett error loggades.
    /// </summary>
    public event EventHandler<ErrorLogEventArgs>? ErrorLogged;

    /// <summary>
    /// Lista över senaste fel (skrivskyddad).
    /// </summary>
    public IReadOnlyList<ErrorLogEventArgs> Errors => _errors.AsReadOnly();

    /// <summary>
    /// Kallas från ErrorEventSink när ett fel loggades.
    /// </summary>
    internal void OnErrorLogged(ErrorLogEventArgs args)
    {
        lock (_errors)
        {
            _errors.Add(args);

            // Behålla bara senaste MaxErrors
            while (_errors.Count > MaxErrors)
                _errors.RemoveAt(0);
        }

        ErrorLogged?.Invoke(this, args);
    }

    /// <summary>
    /// Rensa alla loggade fel.
    /// </summary>
    public void ClearErrors()
    {
        lock (_errors)
        {
            _errors.Clear();
        }
    }
}

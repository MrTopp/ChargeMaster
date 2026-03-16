namespace ChargeMaster.Services.TibberPulse;

/// <summary>
/// Konfigurationsalternativ för Tibber API.
/// </summary>
public class TibberPulseOptions
{
    /// <summary>
    /// Tibber API-token. Hämtas från https://developer.tibber.com/settings/access-token
    /// </summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>
    /// Unikt ID för ditt hem i Tibber-appen.
    /// </summary>
    public string HomeId { get; set; } = string.Empty;
}

namespace ChargeMaster.Services.TibberVehicle;

/// <summary>
/// Konfiguration för Tibber Vehicle API.
/// </summary>
public class TibberVehicleOptions
{
    /// <summary>
    /// Tibber home ID.
    /// </summary>
    public string HomeId { get; set; } = string.Empty;

    /// <summary>
    /// Tibber device ID för fordonet.
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Tibber API token för GraphQL-förfrågningar.
    /// </summary>
    public string ApiToken { get; set; } = string.Empty;
}

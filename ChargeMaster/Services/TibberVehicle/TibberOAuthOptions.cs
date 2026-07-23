namespace ChargeMaster.Services.TibberVehicle;

/// <summary>
/// Konfigurationsalternativ för Tibber OAuth2-autentisering.
/// </summary>
public class TibberOAuthOptions
{
    /// <summary>
    /// Tibber OAuth2 client ID.
    /// Must be configured in appsettings.json (Tibber:OAuth:ClientId).
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Tibber OAuth2 client secret.
    /// Must be configured in appsettings.json (Tibber:OAuth:ClientSecret).
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// URL för Tibber OAuth2-auktorisering.
    /// </summary>
    public string AuthorizeUrl { get; set; } = "https://thewall.tibber.com/connect/authorize";

    /// <summary>
    /// URL för Tibber OAuth2-tokenutbyte.
    /// </summary>
    public string TokenUrl { get; set; } = "https://thewall.tibber.com/connect/token";

    /// <summary>
    /// URL för Tibber API (GraphQL och REST-endpoints).
    /// </summary>
    public string ApiBaseUrl { get; set; } = "https://api.tibber.com";

    /// <summary>
    /// Redirect URI för OAuth2-callback (måste registreras hos Tibber).
    /// Environment-specific value that must be configured in appsettings.json (Tibber:OAuth:RedirectUri).
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// Scope för OAuth2-anropet.
    /// Default: "data-api-vehicles-read" för läsning av fordonsstatus.
    /// </summary>
    public string Scope { get; set; } = "data-api-vehicles-read";

    /// <summary>
    /// Validera att alla required URLs är absolute.
    /// </summary>
    public void Validate()
    {
        if (!Uri.TryCreate(TokenUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException($"Invalid TokenUrl: {TokenUrl}. Must be absolute URI.");

        if (!Uri.TryCreate(AuthorizeUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException($"Invalid AuthorizeUrl: {AuthorizeUrl}. Must be absolute URI.");

        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException($"Invalid ApiBaseUrl: {ApiBaseUrl}. Must be absolute URI.");
    }
}

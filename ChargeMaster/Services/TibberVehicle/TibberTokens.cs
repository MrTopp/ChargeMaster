namespace ChargeMaster.Services.TibberVehicle;

/// <summary>
/// Representerar lagrade OAuth2-tokens för Tibber.
/// </summary>
public class TibberTokens
{
    /// <summary>
    /// Access token för API-anrop.
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token för att få nya access tokens.
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Tidpunkt när access token löper ut.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Kontrollerar om access token har löpt ut.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>
    /// Kontrollerar om access token är giltigt (med 1 minuts buffert).
    /// </summary>
    public bool IsValid => DateTime.UtcNow.AddMinutes(1) < ExpiresAt;
}

/// <summary>
/// OAuth2 token response från Tibber.
/// </summary>
internal class TibberTokenResponse
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public string? TokenType { get; set; }
}

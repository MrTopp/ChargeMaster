using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ChargeMaster.Services.TibberVehicle;

/// <summary>
/// Hanterar OAuth2-flödet för Tibber-autentisering.
/// </summary>
public class TibberOAuthService(
    IOptions<TibberOAuthOptions> options,
    HttpClient httpClient,
    TibberTokenStorage tokenStorage,
    ILogger<TibberOAuthService> logger)
{
    private readonly TibberOAuthOptions _options = options.Value;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Genererar auktoriserings-URL med state-parameter för CSRF-skydd.
    /// </summary>
    public (string AuthorizeUrl, string StateParameter) GenerateAuthorizationUrl()
    {
        var stateParameter = GenerateRandomState();

        logger.LogInformation("Generating authorization URL with parameters:");
        logger.LogInformation("  ClientId: {ClientId}", _options.ClientId);
        logger.LogInformation("  RedirectUri: {RedirectUri}", _options.RedirectUri);
        logger.LogInformation("  Scope: {Scope}", _options.Scope);
        logger.LogInformation("  AuthorizeUrl endpoint: {AuthorizeUrl}", _options.AuthorizeUrl);

        var parameters = new Dictionary<string, string>
        {
            { "client_id", _options.ClientId },
            { "response_type", "code" },
            { "scope", _options.Scope },
            { "redirect_uri", _options.RedirectUri },
            { "state", stateParameter }
        };

        var query = string.Join("&", parameters.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
        var authorizeUrl = $"{_options.AuthorizeUrl}?{query}";

        logger.LogInformation("Generated authorization URL: {AuthorizeUrl}", authorizeUrl);

        return (authorizeUrl, stateParameter);
    }

    /// <summary>
    /// Utbyter auktoriseringskod för access token och sparar tokens.
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> ExchangeCodeForTokenAsync(string authorizationCode)
    {
        try
        {
            logger.LogInformation("Exchanging authorization code for token");
            logger.LogInformation("TokenUrl: {TokenUrl}", _options.TokenUrl);
            logger.LogInformation("ClientId: {ClientId}", _options.ClientId);
            logger.LogInformation("RedirectUri: {RedirectUri}", _options.RedirectUri);
            logger.LogInformation("HttpClient BaseAddress: {BaseAddress}", httpClient.BaseAddress?.ToString() ?? "null");

            var requestBody = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", authorizationCode },
                { "redirect_uri", _options.RedirectUri },
                { "client_id", _options.ClientId },
                { "client_secret", _options.ClientSecret }
            };

            using var content = new FormUrlEncodedContent(requestBody);

            // Create absolute URI to ensure it's used correctly
            var tokenUri = new Uri(_options.TokenUrl);
            logger.LogInformation("Posting to absolute URI: {Uri}", tokenUri.AbsoluteUri);

            var response = await httpClient.PostAsync(tokenUri, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError("Token exchange failed. Status: {Status}", response.StatusCode);
                logger.LogError("Error response: {ErrorContent}", errorContent);

                var errorMessage = $"Token exchange failed: {response.StatusCode}";
                try
                {
                    var errorJson = JsonSerializer.Deserialize<JsonElement>(errorContent, JsonOptions);
                    if (errorJson.TryGetProperty("error", out var error))
                    {
                        errorMessage = error.GetString() ?? errorMessage;
                    }
                    if (errorJson.TryGetProperty("error_description", out var description))
                    {
                        errorMessage += $" - {description.GetString()}";
                    }
                }
                catch { /* Ignore JSON parsing errors */ }

                return (false, errorMessage);
            }

            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TibberTokenResponse>(json, JsonOptions);

            if (tokenResponse?.AccessToken == null)
            {
                logger.LogError("Invalid token response from Tibber");
                return (false, "Invalid token response from Tibber");
            }

            var tokens = new TibberTokens
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken ?? string.Empty,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
            };

            await tokenStorage.SaveAsync(tokens);
            logger.LogInformation("Tibber tokens obtained and saved successfully");

            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during token exchange");
            return (false, $"Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Uppdaterar access token med refresh token om det är giltigt.
    /// </summary>
    public async Task<bool> RefreshTokenAsync()
    {
        try
        {
            var tokens = await tokenStorage.LoadAsync();
            if (tokens?.RefreshToken == null)
            {
                logger.LogWarning("Ingen refresh token tillgänglig");
                return false;
            }

            var requestBody = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", tokens.RefreshToken },
                { "client_id", _options.ClientId },
                { "client_secret", _options.ClientSecret }
            };

            using var content = new FormUrlEncodedContent(requestBody);
            var response = await httpClient.PostAsync(_options.TokenUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError("Fel vid token-uppdatering. Status: {Status}", response.StatusCode);
                logger.LogError("Error response: {ErrorContent}", errorContent);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TibberTokenResponse>(json, JsonOptions);

            if (tokenResponse?.AccessToken == null)
            {
                logger.LogError("Ogiltigt token-svar vid uppdatering");
                return false;
            }

            tokens.AccessToken = tokenResponse.AccessToken;
            if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
            {
                tokens.RefreshToken = tokenResponse.RefreshToken;
            }
            tokens.ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn);

            await tokenStorage.SaveAsync(tokens);
            logger.LogInformation("Tibber-tokens uppdaterade");

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid token-uppdatering");
            return false;
        }
    }

    /// <summary>
    /// Hämtar lagrad access token, uppdaterar vid behov.
    /// </summary>
    public async Task<string?> GetValidAccessTokenAsync()
    {
        try
        {
            var tokens = await tokenStorage.LoadAsync();

            if (tokens == null)
            {
                logger.LogWarning("Ingen token lagrad");
                return null;
            }

            if (tokens.IsValid)
            {
                return tokens.AccessToken;
            }

            // Token har löpt ut, försök uppdatera
            logger.LogInformation("Access token löpt ut, försöker uppdatera");
            var refreshed = await RefreshTokenAsync();

            if (refreshed)
            {
                tokens = await tokenStorage.LoadAsync();
                return tokens?.AccessToken;
            }

            logger.LogError("Kunde inte uppdatera tokens");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av access token");
            return null;
        }
    }

    /// <summary>
    /// Raderar lagrade tokens (logout).
    /// </summary>
    public async Task LogoutAsync()
    {
        await tokenStorage.DeleteAsync();
    }

    private static string GenerateRandomState()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

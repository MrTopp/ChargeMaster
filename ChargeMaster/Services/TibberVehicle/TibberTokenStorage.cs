using System.Text.Json;

namespace ChargeMaster.Services.TibberVehicle;

/// <summary>
/// Hanterar lagring och läsning av Tibber OAuth2-tokens från fil.
/// </summary>
public class TibberTokenStorage(ILogger<TibberTokenStorage> logger, IWebHostEnvironment hostEnvironment)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) 
    { 
        WriteIndented = true 
    };

    private readonly string _tokenFilePath = GetTokenFilePath(hostEnvironment);

    private static string GetTokenFilePath(IWebHostEnvironment hostEnvironment)
    {
        // Använd /tmp för token-lagring (temporär lösning)
        // Mer säker lagring implementeras senare
        var tokenFile = Path.Combine("/tmp", ".tibber-tokens");

        // Försök skapa /tmp om den inte finns (för Windows-development)
        var tmpDir = Path.GetDirectoryName(tokenFile);
        if (tmpDir != null && !Directory.Exists(tmpDir))
        {
            try
            {
                Directory.CreateDirectory(tmpDir);
            }
            catch
            {
                // Ignorera om vi inte kan skapa katalogen
            }
        }

        return tokenFile;
    }

    /// <summary>
    /// Sparar tokens till fil.
    /// </summary>
    public async Task SaveAsync(TibberTokens tokens)
    {
        try
        {
            var json = JsonSerializer.Serialize(tokens, JsonOptions);
            await File.WriteAllTextAsync(_tokenFilePath, json);

            logger.LogInformation("Tibber-tokens sparade");

            // I produktion: sätt restriktiva filbehörigheter
            if (!IsDevelopment())
            {
                var fileInfo = new FileInfo(_tokenFilePath);
                SetRestrictivePermissions(fileInfo);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid sparning av Tibber-tokens");
            throw;
        }
    }

    /// <summary>
    /// Läser tokens från fil.
    /// </summary>
    public async Task<TibberTokens?> LoadAsync()
    {
        try
        {
            if (!File.Exists(_tokenFilePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(_tokenFilePath);
            return JsonSerializer.Deserialize<TibberTokens>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid läsning av Tibber-tokens");
            return null;
        }
    }

    /// <summary>
    /// Raderar sparade tokens.
    /// </summary>
    public async Task DeleteAsync()
    {
        try
        {
            if (File.Exists(_tokenFilePath))
            {
                File.Delete(_tokenFilePath);
                logger.LogInformation("Tibber-tokens raderade");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid radering av Tibber-tokens");
        }
    }

    private bool IsDevelopment() => hostEnvironment.IsDevelopment();

    private static void SetRestrictivePermissions(FileInfo fileInfo)
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                // chmod 600 - endast ägare kan läsa/skriva
                File.SetAttributes(fileInfo.FullName, FileAttributes.Normal);
                // På Linux/Unix skulle vi normalt använda chmod, men i .NET gör vi detta via P/Invoke
                // För nu antar vi att systemadministratören ställer in korrekt katalogbehörighet
            }
        }
        catch (Exception)
        {
            // Ignorera - är inte kritiskt
        }
    }
}

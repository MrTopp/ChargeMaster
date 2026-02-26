using System.Web;

namespace ChargeMaster.Services.Daikin;

/// <summary>
/// Tjänst för kommunikation med Daikin luftvärmepump via lokalt REST API.
/// Daikins API returnerar data i formatet key=value separerade med komma.
/// </summary>
/// <param name="httpClient">HTTP-klienten konfigurerad med basadressen till Daikin-enheten.</param>
/// <param name="logger">Logger för diagnostik och felrapportering.</param>
public class DaikinService(HttpClient httpClient, ILogger<DaikinService> logger)
{
    /// <summary>
    /// Hämtar basinformation om Daikin-enheten (namn, MAC-adress, firmware).
    /// </summary>
    /// <returns>Basinformation eller null vid fel.</returns>
    public async Task<DaikinBasicInfo?> GetBasicInfoAsync()
    {
        try
        {
            var response = await httpClient.GetStringAsync("/common/basic_info");
            var data = ParseResponse(response);

            return new DaikinBasicInfo
            {
                Name = data.TryGetValue("name", out var name)
                    ? HttpUtility.UrlDecode(name)
                    : null,
                MacAddress = data.GetValueOrDefault("mac"),
                FirmwareVersion = data.GetValueOrDefault("ver")
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av Daikin basinformation");
            return null;
        }
    }

    /// <summary>
    /// Hämtar aktuella sensorvärden (temperaturer, fuktighet, kompressorfrekvens).
    /// </summary>
    /// <returns>Sensordata eller null vid fel.</returns>
    public async Task<DaikinSensorInfo?> GetSensorInfoAsync()
    {
        try
        {
            var response = await httpClient.GetStringAsync("/aircon/get_sensor_info");
            var data = ParseResponse(response);

            return new DaikinSensorInfo
            {
                IndoorTemperature = ParseDouble(data.GetValueOrDefault("htemp")),
                OutdoorTemperature = ParseDouble(data.GetValueOrDefault("otemp")),
                IndoorHumidity = ParseDouble(data.GetValueOrDefault("hhum")),
                CompressorFrequency = ParseDouble(data.GetValueOrDefault("cmpfreq"))
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av Daikin sensordata");
            return null;
        }
    }

    /// <summary>
    /// Hämtar aktuella styrinställningar (läge, temperatur, fläkthastighet).
    /// </summary>
    /// <returns>Styrinformation eller null vid fel.</returns>
    public async Task<DaikinControlInfo?> GetControlInfoAsync()
    {
        try
        {
            var response = await httpClient.GetStringAsync("/aircon/get_control_info");
            var data = ParseResponse(response);

            return new DaikinControlInfo
            {
                Power = ParseInt(data.GetValueOrDefault("pow")) ?? 0,
                Mode = ParseInt(data.GetValueOrDefault("mode")) ?? 0,
                TargetTemperature = ParseDouble(data.GetValueOrDefault("stemp")),
                FanRate = data.GetValueOrDefault("f_rate"),
                FanDirection = ParseInt(data.GetValueOrDefault("f_dir")) ?? 0
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av Daikin styrinformation");
            return null;
        }
    }

    /// <summary>
    /// Ställer in styrinställningar på Daikin-enheten.
    /// Kräver att alla parametrar skickas; hämta först aktuella värden med <see cref="GetControlInfoAsync"/>
    /// och ändra bara de värden som ska ändras.
    /// </summary>
    /// <param name="controlInfo">Styrinformation att skicka till enheten.</param>
    /// <returns>True om inställningen lyckades, annars false.</returns>
    public async Task<bool> SetControlInfoAsync(DaikinControlInfo controlInfo)
    {
        try
        {
            var stemp = controlInfo.TargetTemperature?.ToString("F1",
                System.Globalization.CultureInfo.InvariantCulture) ?? "M";

            var url = $"/aircon/set_control_info" +
                      $"?pow={controlInfo.Power}" +
                      $"&mode={controlInfo.Mode}" +
                      $"&stemp={stemp}" +
                      $"&shum=0" +
                      $"&f_rate={controlInfo.FanRate ?? "A"}" +
                      $"&f_dir={controlInfo.FanDirection}";

            var response = await httpClient.GetStringAsync(url);
            var data = ParseResponse(response);

            var success = data.GetValueOrDefault("ret") == "OK";
            if (!success)
            {
                logger.LogWarning("Daikin set_control_info returnerade: {Response}", response);
            }

            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid inställning av Daikin styrinformation");
            return false;
        }
    }

    /// <summary>
    /// Slår på Daikin-enheten utan att ändra övriga inställningar.
    /// </summary>
    /// <returns>True om kommandot lyckades.</returns>
    public async Task<bool> TurnOnAsync()
    {
        var controlInfo = await GetControlInfoAsync();
        if (controlInfo is null) return false;

        controlInfo.Power = 1;
        return await SetControlInfoAsync(controlInfo);
    }

    /// <summary>
    /// Stänger av Daikin-enheten utan att ändra övriga inställningar.
    /// </summary>
    /// <returns>True om kommandot lyckades.</returns>
    public async Task<bool> TurnOffAsync()
    {
        var controlInfo = await GetControlInfoAsync();
        if (controlInfo is null) return false;

        controlInfo.Power = 0;
        return await SetControlInfoAsync(controlInfo);
    }

    /// <summary>
    /// Ställer in börvärdestemperaturen utan att ändra övriga inställningar.
    /// </summary>
    /// <param name="temperature">Önskad temperatur i °C.</param>
    /// <returns>True om kommandot lyckades.</returns>
    public async Task<bool> SetTargetTemperatureAsync(double temperature)
    {
        var controlInfo = await GetControlInfoAsync();
        if (controlInfo is null) return false;

        controlInfo.TargetTemperature = temperature;
        return await SetControlInfoAsync(controlInfo);
    }

    /// <summary>
    /// Parsar Daikins nyckel=värde-responsformat till en dictionary.
    /// Daikin returnerar data som: ret=OK,pow=1,mode=4,stemp=22.0,...
    /// </summary>
    /// <param name="response">Råsträngen från Daikin-enheten.</param>
    /// <returns>Dictionary med nyckel-värde-par.</returns>
    private static Dictionary<string, string> ParseResponse(string response)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in response.Split(','))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                result[parts[0].Trim()] = parts[1].Trim();
            }
        }

        return result;
    }

    /// <summary>
    /// Parsar en sträng till double, hanterar Daikins specialvärden (t.ex. "-" eller "M" för saknade värden).
    /// </summary>
    private static double? ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "-" || value == "M")
            return null;

        return double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    /// <summary>
    /// Parsar en sträng till int, hanterar saknade värden.
    /// </summary>
    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "-")
            return null;

        return int.TryParse(value, out var result) ? result : null;
    }
}

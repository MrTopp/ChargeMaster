namespace ChargeMaster.Services.Shelly;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Parser for Shelly RPC messages received via MQTT.
/// </summary>
public static class ShellyRpcMessageParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>
    /// Parses a JSON string into a ShellyRpcMessage record.
    /// </summary>
    /// <param name="json">The JSON payload string to parse.</param>
    /// <returns>A ShellyRpcMessage record, or null if parsing fails.</returns>
    public static ShellyRpcMessage? Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ShellyRpcMessage>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            return null;
        }
    }

    /// <summary>
    /// Attempts to parse a JSON string into a ShellyRpcMessage record.
    /// </summary>
    /// <param name="json">The JSON payload string to parse.</param>
    /// <param name="message">The parsed message, or null if parsing fails.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParse(string json, out ShellyRpcMessage? message)
    {
        message = Parse(json);
        return message is not null;
    }
}

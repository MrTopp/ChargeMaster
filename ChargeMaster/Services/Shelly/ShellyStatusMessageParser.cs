namespace ChargeMaster.Services.Shelly;

using System.Text.Json;

/// <summary>
/// Representerar en temperatururläsning parsad från ett Shelly MQTT status-meddelande.
/// </summary>
/// <param name="DeviceId">Enhetens ID, t.ex. "hall" (extraherat från topic-prefixet "shelly-hall").</param>
/// <param name="TemperatureCelsius">Temperaturen i Celsius (fältet "tC" i payload).</param>
public record ShellyStatusTemperatureMessage(string DeviceId, double TemperatureCelsius);

/// <summary>
/// Parser för Shelly MQTT status-meddelanden på formatet:
/// topic "shelly-hall/status/temperature:0", payload {"id": 0, "tC": 22.4, "tF": 72.4}.
/// </summary>
public static class ShellyTemperatureMessageParser
{
    /// <summary>
    /// Parsar ett Shelly status-meddelande och returnerar temperaturinformation
    /// om topicet avser en temperatursensor.
    /// </summary>
    /// <param name="topic">MQTT-topicet, t.ex. "shelly-hall/status/temperature:0".</param>
    /// <param name="payload">JSON-payload, t.ex. {"id": 0, "tC": 22.4, "tF": 72.4}.</param>
    /// <returns>
    /// Ett <see cref="ShellyStatusTemperatureMessage"/> med enhetens ID och temperatur,
    /// eller null om meddelandet inte är ett temperaturmeddelande eller om parsningen misslyckas.
    /// </returns>
    public static ShellyStatusTemperatureMessage? Parse(string topic, string payload)
    {
        var topicParts = topic.Split('/');
        if (topicParts.Length < 2 || !topicParts[^1].StartsWith("temperature:"))
            return null;

        var deviceId = topicParts[0].Replace("shelly-", "");
        if (string.IsNullOrEmpty(deviceId))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("tC", out var tCElement))
                return null;

            return new ShellyStatusTemperatureMessage(deviceId, tCElement.GetDouble());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Försöker parsa ett Shelly status-meddelande.
    /// </summary>
    /// <param name="topic">MQTT-topicet.</param>
    /// <param name="payload">JSON-payload.</param>
    /// <param name="message">Det parsade meddelandet, eller null om parsningen misslyckas.</param>
    /// <returns>True om parsningen lyckades, annars false.</returns>
    public static bool TryParse(string topic, string payload, out ShellyStatusTemperatureMessage? message)
    {
        message = Parse(topic, payload);
        return message is not null;
    }
}

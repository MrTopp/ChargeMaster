using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChargeMaster.Models;

public sealed class WallboxSchemaEntry
{
    public int SchemaId { get; set; }
    public string? Start { get; set; }
    public string? Stop { get; set; }

    [JsonConverter(typeof(WallboxWeekdayJsonConverter))]
    public string? Weekday { get; set; }

    public int ChargeLimit { get; set; }
}

public sealed class WallboxWeekdayJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt32(out var i) ? i.ToString() : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.Null => null,
            _ => reader.GetString()
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
// ReSharper disable NonReadonlyMemberInGetHashCode

namespace ChargeMaster.Services.Wallbox;

public sealed class WallboxSchemaEntry : IEquatable<WallboxSchemaEntry>
{
    public int SchemaId { get; set; }
    public string? Start { get; set; }

    /// <summary>
    /// stop time for slot
    /// </summary>
    /// <remarks>
    /// Max value is "24:00:00" when writing to wallbox and "00:00:00"
    /// when reading
    /// </remarks>
    public string? Stop { get; set; }

    /// <summary>
    /// Weekday, 1 is monday. Read as string and write as int.
    /// </summary>
    [JsonConverter(typeof(WallboxWeekdayJsonConverter))]
    public string? Weekday { get; set; }

    public int ChargeLimit { get; set; }


    public bool Equals(WallboxSchemaEntry? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return string.Equals(Start, other.Start, StringComparison.Ordinal)
            && string.Equals(
                Stop == "24:00:00" ?
                    "00:00:00" : Stop,
                other.Stop == "24:00:00" ?
                "00:00:00" : other.Stop, StringComparison.Ordinal)
            && string.Equals(Weekday, other.Weekday, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as WallboxSchemaEntry);

    public override int GetHashCode() => HashCode.Combine(
        Start is null ? 0 : StringComparer.Ordinal.GetHashCode(Start),
        Stop is null ? 0 : StringComparer.Ordinal.GetHashCode(Stop == "24:00:00" ? "00:00:00" : Stop),
        Weekday is null ? 0 : StringComparer.Ordinal.GetHashCode(Weekday));
}

/// <summary>
/// Handle the type difference for 
/// </summary>
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

    /// <summary>
    /// Serialiserar ett strängvärde till JSON, eller skriver null om värdet saknas.
    /// </summary>
    /// <param name="writer">JSON-skrivaren.</param>
    /// <param name="value">Värdet som ska serialiseras.</param>
    /// <param name="options">Serialiseringsalternativ.</param>
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

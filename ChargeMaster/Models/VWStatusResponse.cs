using System.Text.Json;
using System.Text.Json.Serialization;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable RedundantSwitchExpressionArms

namespace ChargeMaster.Models;

public enum VWVehicleState
{
    Unknown,
    Offline,
    Parked,
    IgnitionOn,
    Driving,
    Invalid
}

public sealed class VWStatusResponse
{
    [JsonPropertyName("status")]
    public VWStatus? Status { get; set; }
}

public sealed class VWStatus
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("vin")]
    public string? Vin { get; set; }
    [JsonPropertyName("vehicle_state")]
    [JsonConverter(typeof(VWVehicleStateJsonConverter))]
    public VWVehicleState VehicleState { get; set; }
    [JsonPropertyName("charging_estimated_date_reached")]
    public DateTimeOffset? ChargingEstimatedDateReached { get; set; }
    [JsonPropertyName("charging_power")]
    public double? ChargingPower { get; set; }
    [JsonPropertyName("charging_rate")]
    public double? ChargingRate { get; set; }
    [JsonPropertyName("charging_settings_maximum_current")]
    public double? ChargingSettingsMaximumCurrent { get; set; }
    [JsonPropertyName("charging_settings_target_level")]
    public double? ChargingSettingsTargetLevel { get; set; }
    [JsonPropertyName("battery_level")]
    public double? BatteryLevel { get; set; }
    [JsonPropertyName("battery_range")]
    public double? BatteryRange { get; set; }
    [JsonPropertyName("battery_range_estimated_full")]
    public double? BatteryRangeEstimatedFull { get; set; }
    [JsonPropertyName("inspection_due_at")]
    public DateTimeOffset? InspectionDueAt { get; set; }
    [JsonPropertyName("odometer")]
    public double? Odometer { get; set; }
    [JsonPropertyName("position")]
    public string? Position { get; set; }
}

public sealed class VWVehicleStateJsonConverter : JsonConverter<VWVehicleState>
{
    public override VWVehicleState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        if (string.IsNullOrWhiteSpace(s)) return VWVehicleState.Unknown;

        return s.Trim().ToLowerInvariant() switch
        {
            "offline" => VWVehicleState.Offline,
            "parked" => VWVehicleState.Parked,
            "ignition_on" => VWVehicleState.IgnitionOn,
            "driving" => VWVehicleState.Driving,
            "invalid" => VWVehicleState.Invalid,
            "unknown vehicle state" => VWVehicleState.Unknown,
            _ => VWVehicleState.Unknown
        };
    }

    public override void Write(Utf8JsonWriter writer, VWVehicleState value, JsonSerializerOptions options)
    {
        var s = value switch
        {
            VWVehicleState.Offline => "offline",
            VWVehicleState.Parked => "parked",
            VWVehicleState.IgnitionOn => "ignition_on",
            VWVehicleState.Driving => "driving",
            VWVehicleState.Invalid => "invalid",
            _ => "unknown vehicle state"
        };

        writer.WriteStringValue(s);
    }
}

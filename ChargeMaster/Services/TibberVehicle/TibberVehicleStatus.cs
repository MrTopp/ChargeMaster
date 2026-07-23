using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChargeMaster.Services.TibberVehicle;

/// <summary>
/// Representerar ett fordon från Tibber Data API.
/// </summary>
public class TibberVehicleDevice
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("info")]
    public DeviceInfo? Info { get; set; }

    [JsonPropertyName("status")]
    public DeviceStatus? Status { get; set; }

    [JsonPropertyName("attributes")]
    public DeviceAttribute[]? Attributes { get; set; }

    [JsonPropertyName("capabilities")]
    public DeviceCapability[]? Capabilities { get; set; }
}

public class DeviceInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }
}

public class DeviceStatus
{
    [JsonPropertyName("lastSeen")]
    public DateTime? LastSeen { get; set; }
}

public class DeviceAttribute
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("value")]
    public JsonElement? Value { get; set; }
}

public class DeviceCapability
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("value")]
    public JsonElement? Value { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("availableValues")]
    public JsonElement[]? AvailableValues { get; set; }
}

/// <summary>
/// Status för Tibber-fordon, extraherad från device-data för UI-visning.
/// </summary>
public class TibberVehicleStatus
{
    /// <summary>
    /// Fordonets namn/modell.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Fordonets märke.
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    /// Fordonets modell.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Fordonets externa ID (VIN).
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Batterinivå i procent.
    /// </summary>
    public double? BatteryLevel { get; set; }

    /// <summary>
    /// Målet för laddningsnivå i procent.
    /// </summary>
    public double? ChargingSettingsTargetLevel { get; set; }

    /// <summary>
    /// Räckvidd i meter.
    /// </summary>
    public double? RangeRemaining { get; set; }

    /// <summary>
    /// Beräknad tid till full laddning i timmar.
    /// </summary>
    public double? TimeToFullyCharged { get; set; }

    /// <summary>
    /// Laddarens anslutningsstatus.
    /// </summary>
    public string? ConnectorStatus { get; set; }

    /// <summary>
    /// Fordonets laddningsstatus.
    /// </summary>
    public string? ChargingStatus { get; set; }

    /// <summary>
    /// Är fordonet online?
    /// </summary>
    public bool? IsOnline { get; set; }

    /// <summary>
    /// Är fordonet för närvarande laddat?
    /// </summary>
    public bool? IsCharging => ChargingStatus == "charging";

    /// <summary>
    /// Aktuell laddningseffekt i kW (beräknad från andra värden om inte tillgänglig).
    /// </summary>
    public double? ChargingPower { get; set; }

    /// <summary>
    /// Skapar en TibberVehicleStatus från en TibberVehicleDevice.
    /// </summary>
    public static TibberVehicleStatus FromDevice(TibberVehicleDevice device)
    {
        var status = new TibberVehicleStatus
        {
            Name = device.Info?.Name,
            Brand = device.Info?.Brand,
            Model = device.Info?.Model,
            ExternalId = device.ExternalId,
        };

        // Extrahera värden från capabilities
        if (device.Capabilities != null)
        {
            foreach (var capability in device.Capabilities)
            {
                if (capability.Value == null)
                    continue;

                switch (capability.Id)
                {
                    case "storage.stateOfCharge":
                        if (capability.Value.Value.TryGetDouble(out var batteryLevel))
                            status.BatteryLevel = batteryLevel;
                        break;

                    case "storage.targetStateOfCharge":
                        if (capability.Value.Value.TryGetDouble(out var targetLevel))
                            status.ChargingSettingsTargetLevel = targetLevel;
                        break;

                    case "range.remaining":
                        if (capability.Value.Value.TryGetDouble(out var range))
                            status.RangeRemaining = range;
                        break;

                    case "charging.timeToFullyCharged":
                        if (capability.Value.Value.TryGetDouble(out var timeToCharged))
                            status.TimeToFullyCharged = timeToCharged;
                        break;

                    case "connector.status":
                        status.ConnectorStatus = capability.Value.Value.GetString();
                        break;

                    case "charging.status":
                        status.ChargingStatus = capability.Value.Value.GetString();
                        break;
                }
            }
        }

        // Extrahera värden från attributes
        if (device.Attributes != null)
        {
            foreach (var attr in device.Attributes)
            {
                if (attr.Value == null)
                    continue;

                switch (attr.Id)
                {
                    case "isOnline":
                        if (attr.Value.Value.ValueKind == System.Text.Json.JsonValueKind.True ||
                            attr.Value.Value.ValueKind == System.Text.Json.JsonValueKind.False)
                        {
                            status.IsOnline = attr.Value.Value.GetBoolean();
                        }
                        break;
                }
            }
        }

        return status;
    }
}

/// <summary>
/// API-svar innehållande enhetsstatus från Tibber Data API.
/// </summary>
public class TibberVehicleStatusResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("info")]
    public DeviceInfo? Info { get; set; }

    [JsonPropertyName("status")]
    public DeviceStatus? Status { get; set; }

    [JsonPropertyName("attributes")]
    public DeviceAttribute[]? Attributes { get; set; }

    [JsonPropertyName("capabilities")]
    public DeviceCapability[]? Capabilities { get; set; }
}

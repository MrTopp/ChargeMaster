// ReSharper disable InconsistentNaming
namespace ChargeMaster.Services.Shelly;

/// <summary>
/// Represents a Shelly RPC message received via MQTT.
/// This is the root structure for NotifyFullStatus and other RPC methods.
/// </summary>
public record ShellyRpcMessage(
    string? src,
    string? dst,
    string? method,
    ShellyRpcParams? @params
);

/// <summary>
/// Parameters payload of a Shelly RPC message.
/// </summary>
public record ShellyRpcParams(
    double ts,
    ShellyBle ble,
    ShellyCloud cloud,
    [property: System.Text.Json.Serialization.JsonPropertyName("devicepower:0")]
    ShellyDevicePower? DevicePower0,
    [property: System.Text.Json.Serialization.JsonPropertyName("ht_ui")]
    ShellyHtUi? HtUi,
    [property: System.Text.Json.Serialization.JsonPropertyName("humidity:0")]
    ShellyHumidity? Humidity0,
    ShellyMqtt mqtt,
    ShellySys sys,
    [property: System.Text.Json.Serialization.JsonPropertyName("temperature:0")]
    ShellyTemperature? Temperature0,
    ShellyWifi wifi,
    ShellyWebSocket ws
);

public record ShellyBle;

public record ShellyCloud(bool connected);

public record ShellyDevicePower(
    int id,
    ShellyBattery battery,
    ShellyExternal external
);

public record ShellyBattery(
    double V,
    int percent
);

public record ShellyExternal(bool present);

public record ShellyHtUi;

public record ShellyHumidity(
    int id,
    [property: System.Text.Json.Serialization.JsonPropertyName("rh")]
    double RelativeHumidity
);

public record ShellyMqtt(bool connected);

public record ShellySys(
    string mac,
    bool restart_required,
    string? time,
    long? unixtime,
    long? last_sync_ts,
    int uptime,
    int ram_size,
    int ram_free,
    int ram_min_free,
    int fs_size,
    int fs_free,
    int cfg_rev,
    int kvs_rev,
    int webhook_rev,
    ShellyAvailableUpdates available_updates,
    ShellyWakeupReason wakeup_reason,
    int wakeup_period,
    int reset_reason,
    int utc_offset
);

public record ShellyAvailableUpdates;

public record ShellyWakeupReason(
    string boot,
    string cause
);

public record ShellyTemperature(
    int id,
    [property: System.Text.Json.Serialization.JsonPropertyName("tC")]
    double TemperatureCelsius,
    [property: System.Text.Json.Serialization.JsonPropertyName("tF")]
    double TemperatureFahrenheit
);

public record ShellyWifi(
    [property: System.Text.Json.Serialization.JsonPropertyName("sta_ip")]
    string StationIp,
    string status,
    string ssid,
    string bssid,
    int rssi,
    [property: System.Text.Json.Serialization.JsonPropertyName("sta_ip6")]
    string? StationIp6
);

public record ShellyWebSocket(bool connected);

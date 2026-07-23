using System.Text.Json.Serialization;

namespace ChargeMaster.Services.TibberVehicle;

/// <summary>
/// Status för Tibber-fordon, motsvarar VWStatus.
/// </summary>
public class TibberVehicleStatus
{
    /// <summary>
    /// Fordonets namn/modell.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Fordonets VIN.
    /// </summary>
    [JsonPropertyName("vin")]
    public string? Vin { get; set; }

    /// <summary>
    /// Batterinivå i procent.
    /// </summary>
    [JsonPropertyName("battery_level")]
    public double? BatteryLevel { get; set; }

    /// <summary>
    /// Räckvidd i kilometer.
    /// </summary>
    [JsonPropertyName("range")]
    public double? Range { get; set; }

    /// <summary>
    /// Aktuell laddningseffekt i kW.
    /// </summary>
    [JsonPropertyName("charging_power")]
    public double? ChargingPower { get; set; }

    /// <summary>
    /// Laddningshastighet i km/min.
    /// </summary>
    [JsonPropertyName("charging_rate")]
    public double? ChargingRate { get; set; }

    /// <summary>
    /// Målet för laddningsnivå i procent.
    /// </summary>
    [JsonPropertyName("charging_target_level")]
    public double? ChargingSettingsTargetLevel { get; set; }

    /// <summary>
    /// Maximalt strömstyrka för laddning.
    /// </summary>
    [JsonPropertyName("maximum_current")]
    public double? ChargingSettingsMaximumCurrent { get; set; }

    /// <summary>
    /// Är fordonet för närvarande laddat?
    /// </summary>
    [JsonPropertyName("is_charging")]
    public bool? IsCharging { get; set; }

    /// <summary>
    /// Beräknad tid till full laddning.
    /// </summary>
    [JsonPropertyName("estimated_charge_completion")]
    public DateTimeOffset? ChargingEstimatedDateReached { get; set; }

    /// <summary>
    /// Fordonets position (GPS).
    /// </summary>
    [JsonPropertyName("position")]
    public string? Position { get; set; }

    /// <summary>
    /// Körsträcka i kilometer.
    /// </summary>
    [JsonPropertyName("odometer")]
    public double? Odometer { get; set; }
}

/// <summary>
/// Representerar ett fordon från Tibber.
/// </summary>
public class TibberVehicle
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("vin")]
    public string? Vin { get; set; }

    [JsonPropertyName("make")]
    public string? Make { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }
}

/// <summary>
/// API-svar innehållande fordonsinfo.
/// </summary>
public class TibberVehiclesResponse
{
    [JsonPropertyName("vehicles")]
    public TibberVehicle[]? Vehicles { get; set; }
}

/// <summary>
/// API-svar innehållande fordonsstatus.
/// </summary>
public class TibberVehicleStatusResponse
{
    [JsonPropertyName("status")]
    public TibberVehicleStatus? Status { get; set; }
}

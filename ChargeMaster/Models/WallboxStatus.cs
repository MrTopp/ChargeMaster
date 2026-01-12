using System.Text.Json.Serialization;

namespace ChargeMaster.Models;

public record WallboxStatus(
    string Serial,
    string Status,
    double CurrentLimit,
    double AccEnergy,
    int Phase1Current,
    int Phase2Current,
    int Phase3Current,
    DateTime? CurrentTime,
    [property: JsonPropertyName("currentChargingPower")] double CurrentPower,
    string Mode
);
using System.Text.Json.Serialization;

namespace ChargeMaster.Models;

public record WallboxMeterInfo(
    [property: JsonPropertyName("success")] int Success,
    [property: JsonPropertyName("accEnergy")] long AccEnergy,
    [property: JsonPropertyName("phase1Current")] double Phase1Current,
    [property: JsonPropertyName("phase2Current")] double Phase2Current,
    [property: JsonPropertyName("phase3Current")] double Phase3Current,
    [property: JsonPropertyName("phase1InstPower")] double Phase1InstPower,
    [property: JsonPropertyName("phase2InstPower")] double Phase2InstPower,
    [property: JsonPropertyName("phase3InstPower")] double Phase3InstPower,
    [property: JsonPropertyName("readTime")] long ReadTime,
    [property: JsonPropertyName("gridNetType")] string? GridNetType,
    [property: JsonPropertyName("meterSerial")] string? MeterSerial,
    [property: JsonPropertyName("type")] int Type,
    [property: JsonPropertyName("apparentPower")] long ApparentPower
);

using System.Text.Json.Serialization;
// ReSharper disable NotAccessedPositionalProperty.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace ChargeMaster.Models;

public record WallboxStatus(
    // 1070144
    [property: JsonPropertyName("serialNumber")] long Serial,
    [property: JsonPropertyName("ocppState")] string? OcppState,
    [property: JsonPropertyName("connectedToInternet")] bool ConnectedToInternet,
    [property: JsonPropertyName("freeCharging")] bool FreeCharging,
    [property: JsonPropertyName("ocppConnectionState")] string? OcppConnectionState,
    // "CHARGING_PAUSED", CONNECTED, CHARGING, DISABLED, CHARGING_FINISHED
    [property: JsonPropertyName("connector")] string Connector,
    // "ALWAYS_OFF", ALWAYS_ON, SCHEMA
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("currentLimit")] double CurrentLimit,
    [property: JsonPropertyName("factoryCurrentLimit")] double FactoryCurrentLimit,
    [property: JsonPropertyName("switchCurrentLimit")] double SwitchCurrentLimit,
    [property: JsonPropertyName("powerMode")] string PowerMode,
    [property: JsonPropertyName("currentChargingCurrent")] double CurrentChargingCurrent,
    [property: JsonPropertyName("currentChargingPower")] double CurrentChargingPower,
    [property: JsonPropertyName("accSessionEnergy")] double AccSessionEnergy,
    [property: JsonPropertyName("sessionStartTime")] double? SessionStartTime,
    // HH:MM
    [property: JsonPropertyName("chargeboxTime")] string? ChargeboxTime,
    [property: JsonPropertyName("accSessionMillis")] long AccSessionMillis,
    [property: JsonPropertyName("latestReading")] double LatestReading,
    [property: JsonPropertyName("chargeStatus")] int ChargeStatus,
    [property: JsonPropertyName("updateStatus")] UpdateStatus? UpdateStatus,
    [property: JsonPropertyName("currentTemperature")] int CurrentTemperature,
    [property: JsonPropertyName("sessionStartValue")] double SessionStartValue,
    [property: JsonPropertyName("nrOfPhases")] int NrOfPhases,
    [property: JsonPropertyName("slaveControlWarning")] bool SlaveControlWarning,
    [property: JsonPropertyName("supportConnectionEnabled")] bool SupportConnectionEnabled,
    [property: JsonPropertyName("datetimeConfigured")] bool DatetimeConfigured,
    [property: JsonPropertyName("pilotLevel")] int PilotLevel,
    [property: JsonPropertyName("mainCharger")] MainCharger? MainCharger,
    [property: JsonPropertyName("twinCharger")] MainCharger? TwinCharger
);

public record UpdateStatus(
    [property: JsonPropertyName("serialsToUpdate")] List<string> SerialsToUpdate,
    [property: JsonPropertyName("serialsUpdated")] List<string> SerialsUpdated,
    [property: JsonPropertyName("currentlyUpdating")] int CurrentlyUpdating,
    [property: JsonPropertyName("currentProgress")] int CurrentProgress,
    [property: JsonPropertyName("failedUpdate")] string FailedUpdate
);

public record MainCharger(
    [property: JsonPropertyName("reference")] string? Reference,
    [property: JsonPropertyName("serialNumber")] long SerialNumber,
    [property: JsonPropertyName("lastContact")] long LastContact,
    [property: JsonPropertyName("online")] bool Online,
    [property: JsonPropertyName("loadBalanced")] bool LoadBalanced,
    [property: JsonPropertyName("phase")] int Phase,
    [property: JsonPropertyName("productId")] int ProductId,
    [property: JsonPropertyName("meterStatus")] int MeterStatus,
    [property: JsonPropertyName("meterSerial")] string MeterSerial,
    [property: JsonPropertyName("chargeStatus")] int ChargeStatus,
    [property: JsonPropertyName("pilotLevel")] int PilotLevel,
    [property: JsonPropertyName("accEnergy")] double AccEnergy,
    [property: JsonPropertyName("firmwareVersion")] int FirmwareVersion,
    [property: JsonPropertyName("firmwareRevision")] int FirmwareRevision,
    [property: JsonPropertyName("wifiCardStatus")] int WifiCardStatus,
    [property: JsonPropertyName("connector")] string Connector,
    [property: JsonPropertyName("accSessionEnergy")] double AccSessionEnergy,
    [property: JsonPropertyName("sessionStartValue")] double SessionStartValue,
    [property: JsonPropertyName("accSessionMillis")] long AccSessionMillis,
    [property: JsonPropertyName("sessionStartTime")] long? SessionStartTime,
    [property: JsonPropertyName("currentChargingCurrent")] double CurrentChargingCurrent,
    [property: JsonPropertyName("currentChargingPower")] double CurrentChargingPower,
    [property: JsonPropertyName("nrOfPhases")] int NrOfPhases,
    [property: JsonPropertyName("twinSerial")] long TwinSerial,
    [property: JsonPropertyName("cableLockMode")] int CableLockMode,
    [property: JsonPropertyName("minCurrentLimit")] int MinCurrentLimit,
    [property: JsonPropertyName("dipSwitchSettings")] int DipSwitchSettings,
    [property: JsonPropertyName("cpuType")] long CpuType,
    [property: JsonPropertyName("updateable")] bool Updateable
);
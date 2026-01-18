namespace ChargeMaster.Models;

public sealed class WallboxConfig
{
    public bool OcppConnected { get; set; }
    public int MaxChargeCurrent { get; set; }
    public string? ProductId { get; set; }
    public string? ProgramVersion { get; set; }
    public int FirmwareVersion { get; set; }
    public int FirmwareRevision { get; set; }
    public bool LbVersion2 { get; set; }
    public long SerialNumber { get; set; }
    public string? MeterSerialNumber { get; set; }
    public int MeterType { get; set; }
    public int FactoryChargeLimit { get; set; }
    public int SwitchChargeLimit { get; set; }
    public bool RfidReaderPresent { get; set; }
    public string? RfidMode { get; set; }
    public int PowerbackupStatus { get; set; }
    public int LastTemperature { get; set; }
    public int WarningTemperature { get; set; }
    public int CutoffTemperature { get; set; }
    public bool ReducedIntervalsEnabled { get; set; }
    public IReadOnlyList<object>? ReducedCurrentIntervals { get; set; }
    public int SoftwareVersion { get; set; }
    public int AvailableVersion { get; set; }
    public string? UpdateUrl { get; set; }
    public int NetworkMode { get; set; }
    public int NetworkType { get; set; }
    public string? NetworkSSID { get; set; }
    public string? WebNetworkPassword { get; set; }
    public int NetworkAPChannel { get; set; }
    public int EthNetworkMode { get; set; }
    public long? GcConfigTimestamp { get; set; }
    public bool GcloudActivated { get; set; }
    public long? GcActivatedFrom { get; set; }
    public string? EthGateway { get; set; }
    public string? EthDNS { get; set; }
    public string? EthIP { get; set; }
    public int EthMask { get; set; }
    public bool LocalLoadBalanced { get; set; }
    public bool GroupLoadBalanced { get; set; }
    public bool GroupLoadBalanced101 { get; set; }
    public bool EnergyReportEnabled { get; set; }
    public bool Master { get; set; }
    public string? Timezone { get; set; }
    public IReadOnlyList<WallboxSlaveConfig>? SlaveList { get; set; }
    public string? GridNetType { get; set; }
    public bool SlaveControlAvailable { get; set; }
    public int CurrentMultiplier { get; set; }
    public int RfPower { get; set; }
    public long TwinSerial { get; set; }
    public int TwinSwitchLimit { get; set; }
    public IReadOnlyList<string>? EnergySerials { get; set; }
    public string? PackageVersion { get; set; }
    public bool CableAutoUnlocked { get; set; }
    public bool InternetSharingEnabled { get; set; }
    public string? WebAPPassword { get; set; }
    public bool Standalone { get; set; }
    public bool Castra { get; set; }
    public long CpuType { get; set; }
    public string? BuildDetails { get; set; }
}

public sealed class WallboxSlaveConfig
{
    public string? Reference { get; set; }
    public long SerialNumber { get; set; }
    public long LastContact { get; set; }
    public bool Online { get; set; }
    public bool LoadBalanced { get; set; }
    public int Phase { get; set; }
    public int ProductId { get; set; }
    public int MeterStatus { get; set; }
    public string? MeterSerial { get; set; }
    public int ChargeStatus { get; set; }
    public int PilotLevel { get; set; }
    public double AccEnergy { get; set; }
    public int FirmwareVersion { get; set; }
    public int FirmwareRevision { get; set; }
    public int WifiCardStatus { get; set; }
    public string? Connector { get; set; }
    public double AccSessionEnergy { get; set; }
    public double SessionStartValue { get; set; }
    public long AccSessionMillis { get; set; }
    public long SessionStartTime { get; set; }
    public int CurrentChargingCurrent { get; set; }
    public int CurrentChargingPower { get; set; }
    public int NrOfPhases { get; set; }
    public long TwinSerial { get; set; }
    public int CableLockMode { get; set; }
    public int MinCurrentLimit { get; set; }
    public int DipSwitchSettings { get; set; }
    public long CpuType { get; set; }
    public bool Updateable { get; set; }
}

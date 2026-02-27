namespace ChargeMaster.Services.Daikin;

/// <summary>
/// Basinformation från Daikin-enhetens /common/basic_info-slutpunkt.
/// </summary>
public class DaikinBasicInfo
{
    /// <summary>
    /// Enhetstyp (t.ex. "aircon").
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Region (t.ex. "eu").
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Sommartid aktiv (1 = ja, 0 = nej).
    /// </summary>
    public int? DaylightSaving { get; set; }

    /// <summary>
    /// Firmware-version.
    /// </summary>
    public string? FirmwareVersion { get; set; }

    /// <summary>
    /// Firmware-revision.
    /// </summary>
    public string? Revision { get; set; }

    /// <summary>
    /// Ström på/av (0 = av, 1 = på).
    /// </summary>
    public int? Power { get; set; }

    /// <summary>
    /// Felkod (0 = inget fel).
    /// </summary>
    public int? ErrorCode { get; set; }

    /// <summary>
    /// Enhetens namn (URL-avkodat).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Kommunikationsmetod (t.ex. "polling").
    /// </summary>
    public string? Method { get; set; }

    /// <summary>
    /// Kommunikationsport.
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Unikt enhets-ID.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Adaptertyp (t.ex. 3).
    /// </summary>
    public int? AdapterKind { get; set; }

    /// <summary>
    /// Protokollversion.
    /// </summary>
    public string? ProtocolVersion { get; set; }

    /// <summary>
    /// Kontrollprotokollversion.
    /// </summary>
    public string? ControlProtocolVersion { get; set; }

    /// <summary>
    /// LED-indikator aktiv (1 = ja, 0 = nej).
    /// </summary>
    public int? Led { get; set; }

    /// <summary>
    /// MAC-adress.
    /// </summary>
    public string? MacAddress { get; set; }

    /// <summary>
    /// Enhetens SSID (accesspunktens namn).
    /// </summary>
    public string? Ssid { get; set; }

    /// <summary>
    /// Adapterläge (t.ex. "run").
    /// </summary>
    public string? AdapterMode { get; set; }

    /// <summary>
    /// WiFi-signalstyrka (RSSI).
    /// </summary>
    public int? WifiSignal { get; set; }

    /// <summary>
    /// Anslutet WiFi-nätverk (URL-avkodat).
    /// </summary>
    public string? ConnectedSsid { get; set; }

    /// <summary>
    /// Säkerhetstyp för WiFi-anslutningen (t.ex. "WPA2").
    /// </summary>
    public string? SecurityType { get; set; }

    /// <summary>
    /// Semesterläge aktivt (0 = av, 1 = på).
    /// </summary>
    public int? HolidayMode { get; set; }
}

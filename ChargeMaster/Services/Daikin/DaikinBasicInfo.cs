namespace ChargeMaster.Services.Daikin;

/// <summary>
/// Basinformation från Daikin-enhetens /common/basic_info-slutpunkt.
/// </summary>
public class DaikinBasicInfo
{
    /// <summary>
    /// Enhetens namn (URL-avkodat).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// MAC-adress.
    /// </summary>
    public string? MacAddress { get; set; }

    /// <summary>
    /// Firmware-version.
    /// </summary>
    public string? FirmwareVersion { get; set; }
}

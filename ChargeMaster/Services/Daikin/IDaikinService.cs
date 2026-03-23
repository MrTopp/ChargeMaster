namespace ChargeMaster.Services.Daikin;

/// <summary>
/// Interface för kommunikation med Daikin värmepump.
/// Definierar de metoder som krävs för att kommunicera med en Daikin värmepump.
/// </summary>
public interface IDaikinService
{
    /// <summary>
    /// Hämtar aktuell sensor-information från värmepumpen.
    /// </summary>
    /// <returns>Sensor-information eller null om hämtningen misslyckades.</returns>
    Task<DaikinSensorInfo?> GetSensorInfoAsync();

    /// <summary>
    /// Hämtar aktuell kontroll-information från värmepumpen.
    /// </summary>
    /// <returns>Kontroll-information eller null om hämtningen misslyckades.</returns>
    Task<DaikinControlInfo?> GetControlInfoAsync();

    /// <summary>
    /// Ställer in måltemperatur på värmepumpen.
    /// </summary>
    /// <param name="temperature">Måltemperatur i °C.</param>
    /// <param name="heat">Om true, använd värmemodus; om false, använd kylmodus.</param>
    /// <returns>True om inställningen lyckades, annars false.</returns>
    Task<bool> SetTargetTemperatureAsync(double temperature, bool heat);

    /// <summary>
    /// Slår på värmepumpen.
    /// </summary>
    /// <returns>True om påslaging lyckades, annars false.</returns>
    Task<bool> TurnOnAsync();

    /// <summary>
    /// Stänger av värmepumpen.
    /// </summary>
    /// <returns>True om avstängning lyckades, annars false.</returns>
    Task<bool> TurnOffAsync();

    /// <summary>
    /// Ställer in värmepumpens läge.
    /// </summary>
    /// <param name="mode">Läge enligt DaikinMode enum (0=Auto, 1=Auto, 2=Dry, 3=Cool, 4=Heat, 6=Fan, 7=Auto).</param>
    /// <returns>True om byte av läge lyckades, annars false.</returns>
    Task<bool> SetModeAsync(int mode);
}

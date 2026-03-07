namespace ChargeMaster.Services.Daikin;

/// <summary>
/// Facade för förenklad kontroll av Daikin värmepump.
/// Tillhandahåller hjälpfunktioner och cachelagrar status lokalt.
/// </summary>
public class DaikinFacade(DaikinService daikinService, ILogger<DaikinFacade> logger)
{
    private double? _currentTemperature;
    private double? _outdoorTemperature;
    private double? _targetTemperature;
    private int _power; // 0 = Off, 1 = On
    private DaikinMode _mode; // 0=Auto, 1=Auto, 2=Dry, 3=Cool, 4=Heat, 6=Fan, 7=Auto

    /// <summary>
    /// Event som triggas när Daikin-status ändras.
    /// </summary>
    public event EventHandler<DaikinStatusChangedEventArgs>? StatusChanged;

    /// <summary>
    /// Aktuell innetemperatur (°C).
    /// </summary>
    public double? CurrentTemperature => _currentTemperature;

    /// <summary>
    /// Aktuell utetemperatur (°C).
    /// </summary>
    public double? OutdoorTemperature => _outdoorTemperature;

    /// <summary>
    /// Måltemperatur (°C).
    /// </summary>
    public double? TargetTemperature => _targetTemperature;

    /// <summary>
    /// Läge: Auto, Dry, Cool, Heat, Fan.
    /// </summary>
    public DaikinMode Mode => _mode;

    /// <summary>
    /// Enkel property för att kontrollera om värmepumpen är på.
    /// </summary>
    public bool IsOn => _power != 0;

    /// <summary>
    /// Läser upp aktuell status från Daikin värmepump vid uppstart.
    /// </summary>
    public async Task InitializeAsync(bool forceEvent = false)
    {
        try
        {
            await UpdateStatusAsync();
            logger.LogDebug(
                "Daikin-status: Inne: {Current}°C, Ute: {Outdoor}°C, Måltemperatur: {Target}°C, Läge: {Mode}, Status: {Status}",
                _currentTemperature, _outdoorTemperature, _targetTemperature, _mode, IsOn ? "PÅ" : "AV");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid initialisering av Daikin-status");
        }
    }

    /// <summary>
    /// Privat hjälpfunktion som hämtar och uppdaterar enhetens status.
    /// </summary>
    private async Task UpdateStatusAsync(bool forceEvent = false)
    {
        var sensorInfo = await daikinService.GetSensorInfoAsync();
        var controlInfo = await daikinService.GetControlInfoAsync();

        var changes = new DaikinStatusChangedEventArgs();

        if (sensorInfo != null)
        {
            if (Math.Abs( (_currentTemperature ?? 0) - (sensorInfo.IndoorTemperature ?? 0)) > 0.01)
            {
                _currentTemperature = sensorInfo.IndoorTemperature;
                changes.CurrentTemperatureChanged = true;
            }

            if (Math.Abs( (_outdoorTemperature ?? 0) - (sensorInfo.OutdoorTemperature ?? 0)) > 0.01)
            {
                _outdoorTemperature = sensorInfo.OutdoorTemperature;
                changes.OutdoorTemperatureChanged = true;
            }
        }

        if (controlInfo != null)
        {
            if (Math.Abs( (_targetTemperature ?? 0) - (controlInfo.TargetTemperature ?? 0)) > 0.01)
            {
                _targetTemperature = controlInfo.TargetTemperature;
                changes.TargetTemperatureChanged = true;
            }

            if (_power != controlInfo.Power)
            {
                _power = controlInfo.Power;
                changes.PowerChanged = true;
            }

            var newMode = (DaikinMode)controlInfo.Mode;
            if (_mode != newMode)
            {
                _mode = newMode;
                changes.ModeChanged = true;
            }
        }

        // Raisa event endast om något ändrades
        if (forceEvent || changes.HasChanges)
        {
            StatusChanged?.Invoke(this, changes);
        }
    }

    /// <summary>
    /// Ställer in måltemperatur och ange om värme eller kyla skall användas.
    /// </summary>
    public async Task<bool> SetTargetTemperatureAsync(double temperature, bool heat)
    {
        try
        {
            bool result = await daikinService.SetTargetTemperatureAsync(temperature, heat);
            if (result)
            {
                await UpdateStatusAsync();
                logger.LogInformation("Måltemperatur inställd till {Temperature}°C", _targetTemperature);
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid inställning av måltemperatur till {Temperature}°C", temperature);
            return false;
        }
    }

    /// <summary>
    /// Slår på värmepumpen.
    /// </summary>
    public async Task<bool> TurnOnAsync()
    {
        try
        {
            bool result = await daikinService.TurnOnAsync();
            if (result)
            {
                await UpdateStatusAsync();
                logger.LogInformation("Daikin värmepump påslagen");
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid påslaging av värmepump");
            return false;
        }
    }

    /// <summary>
    /// Stänger av värmepumpen.
    /// </summary>
    public async Task<bool> TurnOffAsync()
    {
        try
        {
            bool result = await daikinService.TurnOffAsync();
            if (result)
            {
                await UpdateStatusAsync();
                logger.LogInformation("Daikin värmepump avstängd");
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid avstängning av värmepump");
            return false;
        }
    }

    /// <summary>
    /// Ställer in värmepumpens läge (Cool, Heat, etc).
    /// </summary>
    public async Task<bool> SetModeAsync(DaikinMode mode)
    {
        try
        {
            bool result = await daikinService.SetModeAsync((int)mode);
            if (result)
            {
                await UpdateStatusAsync();
                logger.LogInformation("Daikin läge ändrat till {Mode}", _mode);
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid byte av värmepumpläge till {Mode}", mode);
            return false;
        }
    }
}

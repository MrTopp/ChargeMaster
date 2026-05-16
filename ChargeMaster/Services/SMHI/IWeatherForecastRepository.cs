namespace ChargeMaster.Services.SMHI;

/// <summary>
/// Interface för repository som hanterar väderprognos-data i databasen.
/// </summary>
public interface IWeatherForecastRepository
{
    /// <summary>
    /// Sparar eller uppdaterar väderprognos i databasen.
    /// </summary>
    /// <param name="forecasts">Lista med väderprognos-data att spara</param>
    /// <param name="cancellationToken">Token för att avbryta operationen</param>
    /// <returns>Task som representerar den asynkrona operationen</returns>
    Task SaveForecastsAsync(List<WeatherForecast> forecasts, CancellationToken cancellationToken);
}

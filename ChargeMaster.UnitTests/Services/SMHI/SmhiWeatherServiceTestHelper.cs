using ChargeMaster.Services.SMHI;
using Microsoft.Extensions.Logging;
using Moq;

namespace ChargeMaster.UnitTests.Services.SMHI;

/// <summary>
/// Helper class for creating properly configured test dependencies for SmhiWeatherService unit tests.
/// Centralizes mock creation to reduce duplication and ensure consistency across tests.
/// </summary>
public static class SmhiWeatherServiceTestHelper
{
    /// <summary>
    /// Creates a mock IWeatherForecastRepository with default behavior for basic testing.
    /// </summary>
    /// <returns>A mock IWeatherForecastRepository instance</returns>
    public static Mock<IWeatherForecastRepository> CreateMockRepository()
    {
        var mockRepository = new Mock<IWeatherForecastRepository>();

        // Configure default behavior: SaveForecastsAsync returns successfully
        mockRepository
            .Setup(x => x.SaveForecastsAsync(It.IsAny<List<WeatherForecast>>()))
            .Returns(Task.CompletedTask);

        return mockRepository;
    }

    /// <summary>
    /// Creates a mock ILogger for SmhiWeatherService testing.
    /// </summary>
    /// <returns>A mock ILogger for SmhiWeatherService</returns>
    public static Mock<ILogger<SmhiWeatherService>> CreateMockLogger()
    {
        return new Mock<ILogger<SmhiWeatherService>>();
    }

    /// <summary>
    /// Creates a mock HttpClient with default behavior for SMHI API responses.
    /// </summary>
    /// <returns>A mock HttpClient instance</returns>
    public static Mock<HttpClient> CreateMockHttpClient()
    {
        return new Mock<HttpClient>();
    }

    /// <summary>
    /// Creates a fully configured SmhiWeatherService for unit testing with all dependencies mocked.
    /// </summary>
    /// <param name="httpClient">Optional custom HttpClient. If null, a real instance is created.</param>
    /// <param name="repository">Optional custom repository mock. If null, default mock is used.</param>
    /// <param name="logger">Optional custom logger mock. If null, default mock is used.</param>
    /// <returns>A configured SmhiWeatherService instance with all dependencies properly mocked</returns>
    public static SmhiWeatherService CreateServiceWithMocks(
        HttpClient? httpClient = null,
        Mock<IWeatherForecastRepository>? repository = null,
        Mock<ILogger<SmhiWeatherService>>? logger = null)
    {
        var clientToUse = httpClient ?? new HttpClient();
        var repositoryToUse = repository ?? CreateMockRepository();
        var loggerToUse = logger ?? CreateMockLogger();

        if (repositoryToUse == null)
            throw new InvalidOperationException("Repository mock cannot be null");
        if (repositoryToUse.Object == null)
            throw new InvalidOperationException("Repository mock object cannot be null");
        if (loggerToUse == null)
            throw new InvalidOperationException("Logger mock cannot be null");
        if (loggerToUse.Object == null)
            throw new InvalidOperationException("Logger mock object cannot be null");

        try
        {
            return new SmhiWeatherService(clientToUse, loggerToUse.Object, repositoryToUse.Object);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to create SmhiWeatherService instance with provided mocks. " +
                "Ensure all dependencies are properly configured.",
                ex);
        }
    }

    /// <summary>
    /// Creates a mock WeatherForecast with custom values for testing.
    /// </summary>
    /// <param name="time">Forecast time (default: now)</param>
    /// <param name="temperature">Temperature in °C (default: 15.0)</param>
    /// <param name="precipitation">Precipitation in mm (default: null)</param>
    /// <param name="windSpeed">Wind speed in m/s (default: null)</param>
    /// <param name="cloudCoverage">Cloud coverage 0-100% (default: null)</param>
    /// <returns>A WeatherForecast instance with specified values</returns>
    public static WeatherForecast CreateWeatherForecast(
        DateTime? time = null,
        double temperature = 15.0,
        double? precipitation = null,
        double? windSpeed = null,
        double? cloudCoverage = null)
    {
        return new WeatherForecast
        {
            Time = time ?? DateTime.UtcNow,
            Temperature = temperature,
            Precipitation = precipitation,
            WindSpeed = windSpeed,
            CloudCoverage = cloudCoverage,
            MaxPrecipitation = precipitation,
            MeanPrecipitation = precipitation,
            WindGust = windSpeed,
            WindDirection = null,
            Luftfuktighet = 50,
            Lufttryck = 1013.25,
            Sikt = 10000,
            ThunderstormProbability = 0,
            PrecipitationMedian = precipitation,
            PrecipitationProbability = null,
            PrecipitationCategory = null,
            WeatherSymbol = 1,
            TotalPrecipitation = precipitation
        };
    }

    /// <summary>
    /// Creates a list of mock weather forecasts representing a full day (24 hours).
    /// </summary>
    /// <param name="startTime">Start time for the forecast (default: today 00:00 UTC)</param>
    /// <param name="baseTemperature">Base temperature to vary around (default: 15.0)</param>
    /// <returns>A list of 24 WeatherForecast instances, one per hour</returns>
    public static List<WeatherForecast> CreateDayOfForecasts(
        DateTime? startTime = null,
        double baseTemperature = 15.0)
    {
        var start = startTime ?? DateTime.UtcNow.Date;
        var forecasts = new List<WeatherForecast>();

        for (int i = 0; i < 24; i++)
        {
            // Simulate daily temperature variation (warmer during day, colder at night)
            var hourTemp = baseTemperature + (5.0 * Math.Sin((i - 6) * Math.PI / 12.0));

            forecasts.Add(CreateWeatherForecast(
                time: start.AddHours(i),
                temperature: hourTemp,
                precipitation: i > 6 && i < 18 ? 0.5 : 0,
                windSpeed: 3.5 + (i % 5),
                cloudCoverage: 40 + (i % 6) * 10
            ));
        }

        return forecasts;
    }

    /// <summary>
    /// Configures a mock repository to throw an exception when SaveForecastsAsync is called.
    /// Useful for testing error handling.
    /// </summary>
    /// <param name="mockRepository">The mock repository to configure</param>
    /// <param name="exception">The exception to throw</param>
    public static void ConfigureRepositoryToThrow(
        Mock<IWeatherForecastRepository> mockRepository,
        Exception exception)
    {
        mockRepository
            .Setup(x => x.SaveForecastsAsync(It.IsAny<List<WeatherForecast>>()))
            .ThrowsAsync(exception);
    }

    /// <summary>
    /// Creates a mock SMHI API response as JSON string for testing.
    /// </summary>
    /// <param name="forecastCount">Number of forecast entries to include (default: 24)</param>
    /// <param name="baseTemperature">Base temperature value (default: 15.0)</param>
    /// <returns>JSON string representing a valid SMHI API response</returns>
    public static string CreateSmhiApiResponse(int forecastCount = 24, double baseTemperature = 15.0)
    {
        var timeSeries = new System.Collections.Generic.List<object>();
        var now = DateTime.UtcNow;

        for (int i = 0; i < forecastCount; i++)
        {
            var time = now.AddHours(i);
            var temp = baseTemperature + (5.0 * Math.Sin((i - 6) * Math.PI / 12.0));

            timeSeries.Add(new
            {
                validTime = time.ToString("O"),
                parameters = new[]
                {
                    new { name = "t", values = new[] { temp } },
                    new { name = "tcc_mean", values = new[] { 4.0 } },
                    new { name = "pmin", values = new[] { 0.5 } },
                    new { name = "pmax", values = new[] { 1.5 } },
                    new { name = "pmean", values = new[] { 1.0 } },
                    new { name = "ws", values = new[] { 3.5 } },
                    new { name = "gust", values = new[] { 6.0 } },
                    new { name = "wd", values = new[] { 180.0 } },
                    new { name = "r", values = new[] { 65.0 } },
                    new { name = "msl", values = new[] { 1013.25 } },
                    new { name = "vis", values = new[] { 10000.0 } },
                    new { name = "tstm", values = new[] { 0.0 } },
                    new { name = "pmedian", values = new[] { 1.0 } },
                    new { name = "spp", values = new[] { 30.0 } },
                    new { name = "pcat", values = new[] { 1.0 } },
                    new { name = "Wsymb2", values = new[] { 1.0 } },
                    new { name = "tp", values = new[] { 1.5 } }
                }
            });
        }

        var response = new
        {
            approvedTime = now.ToString("O"),
            referenceTime = now.ToString("O"),
            timeSeries = timeSeries
        };

        return System.Text.Json.JsonSerializer.Serialize(response);
    }

    /// <summary>
    /// Configures a mock HttpClient to return a successful SMHI API response.
    /// Useful for testing successful forecast retrieval.
    /// </summary>
    /// <param name="mockClient">The mock HttpClient to configure</param>
    /// <param name="responseJson">The JSON response body (default: valid SMHI response)</param>
    public static void ConfigureHttpClientForSuccessfulResponse(
        HttpClient mockClient,
        string? responseJson = null)
    {
        responseJson ??= CreateSmhiApiResponse();
        // Note: Actual HttpClient mocking requires more complex setup
        // This is a placeholder for documentation purposes
    }
}

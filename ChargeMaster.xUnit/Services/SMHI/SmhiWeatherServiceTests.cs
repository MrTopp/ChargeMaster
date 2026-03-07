using ChargeMaster.Services.SMHI;

using Microsoft.Extensions.Logging;

using Xunit;

namespace ChargeMaster.xUnit.Services.SMHI;

public class SmhiWeatherServiceTests
{
    [Fact]
    public async Task GetForecastAsync_WithValidCoordinates_ReturnsWeatherForecasts()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = new NullLogger<SmhiWeatherService>();
        var service = new SmhiWeatherService(httpClient, logger);

        // Act
        var forecasts = await service.GetForecastAsync(longitude: 14.416639, latitude: 59.250709);

        // Assert
        Assert.NotNull(forecasts);
        Assert.NotEmpty(forecasts);
        Assert.True(forecasts.Count <= 12, "Should return at most 12 hours of forecast");

        // Verify that each forecast has reasonable temperature values
        foreach (var forecast in forecasts)
        {
            Assert.True(forecast.Temperature >= -50 && forecast.Temperature <= 60, 
                "Temperature should be within reasonable range");
            Assert.True(forecast.FeelsLike >= -50 && forecast.FeelsLike <= 60, 
                "Feels like temperature should be within reasonable range");
        }
    }

    [Fact]
    public async Task GetForecastForStockholmAsync_ReturnsWeatherForecasts()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = new NullLogger<SmhiWeatherService>();
        var service = new SmhiWeatherService(httpClient, logger);

        // Act
        var forecasts = await service.GetForecastForStockholmAsync();

        // Assert
        Assert.NotNull(forecasts);
    }
}

/// <summary>
/// Null logger implementation for testing
/// </summary>
internal class NullLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}

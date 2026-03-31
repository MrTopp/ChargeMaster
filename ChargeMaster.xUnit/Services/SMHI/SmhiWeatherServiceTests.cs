using ChargeMaster.Services.SMHI;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ChargeMaster.xUnit.Services.SMHI;

public class SmhiWeatherServiceTests
{
    private static IServiceScopeFactory CreateMockedScopeFactory()
    {
        var mockRepository = new Mock<IWeatherForecastRepository>();
        mockRepository
            .Setup(r => r.SaveForecastsAsync(It.IsAny<List<WeatherForecast>>()))
            .Returns(Task.CompletedTask);

        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IWeatherForecastRepository)))
            .Returns(mockRepository.Object);

        var mockScope = new Mock<IServiceScope>();
        mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);

        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

        return mockScopeFactory.Object;
    }

    [Fact]
    public async Task GetForecastAsync_WithValidCoordinates_ReturnsWeatherForecasts()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = new NullLogger<SmhiWeatherService>();
        var scopeFactory = CreateMockedScopeFactory();
        var service = new SmhiWeatherService(httpClient, logger, scopeFactory);

        // Act
        var forecasts = await service.GetForecastAsync(longitude: 14.416639, latitude: 59.250709);

        // Assert
        Assert.NotNull(forecasts);
        Assert.NotEmpty(forecasts);
        Assert.True(forecasts.Count >= 12, "Should return at least 12 hours of forecast");

        // Verify that each forecast has reasonable temperature values
        foreach (var forecast in forecasts)
        {
            Assert.True(forecast.Temperature >= -50 && forecast.Temperature <= 60, 
                "Temperature should be within reasonable range");
        }
    }

    [Fact(Skip="Only for interactive testing")]
    public async Task GetForecastForStromtorpAsync_ReturnsWeatherForecasts()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = new NullLogger<SmhiWeatherService>();
        var scopeFactory = CreateMockedScopeFactory();
        var service = new SmhiWeatherService(httpClient, logger, scopeFactory);

        // Act
        var forecasts = await service.GetForecastAsync();

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

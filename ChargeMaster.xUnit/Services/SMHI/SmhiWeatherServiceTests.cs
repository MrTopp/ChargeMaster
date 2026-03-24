using ChargeMaster.Services.SMHI;
using ChargeMaster.Data;

using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace ChargeMaster.xUnit.Services.SMHI;

public class SmhiWeatherServiceTests
{
    private static IWeatherForecastRepository CreateWeatherForecastRepository()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql("Host=localhost;Database=chargemaster_test;Username=postgres;Password=postgres"));
        services.AddLogging();
        services.AddScoped<IWeatherForecastRepository, WeatherForecastRepository>();

        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IWeatherForecastRepository>();
    }

    private static ApplicationDbContext CreateTestDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=chargemaster_test;Username=postgres;Password=postgres")
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetForecastAsync_WithValidCoordinates_ReturnsWeatherForecasts()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = new NullLogger<SmhiWeatherService>();
        var repository = CreateWeatherForecastRepository();
        var service = new SmhiWeatherService(httpClient, logger, new Mock<IServiceScopeFactory>().Object);

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

    [Fact]
    public async Task GetForecastForStromtorpAsync_ReturnsWeatherForecasts()
    {
        // Arrange
        var httpClient = new HttpClient();
        var logger = new NullLogger<SmhiWeatherService>();
        var repository = CreateWeatherForecastRepository();
        var service = new SmhiWeatherService(httpClient, logger, new Mock<IServiceScopeFactory>().Object);

        // Act
        var forecasts = await service.GetForecast();

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

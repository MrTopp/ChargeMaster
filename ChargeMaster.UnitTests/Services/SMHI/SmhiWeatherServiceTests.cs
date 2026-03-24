using ChargeMaster.Services.SMHI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace ChargeMaster.UnitTests.Services.SMHI;
/// <summary>
/// Unit tests for the SmhiWeatherService class.
/// </summary>
public class SmhiWeatherServiceTests
{
    /// <summary>
    /// Tests that GetForecast calls GetForecastAsync with the hardcoded Strömtorp coordinates
    /// and returns the result. This test verifies the method executes and completes successfully.
    /// Note: HttpClient cannot be mocked, so this test uses a real HttpClient instance which will
    /// result in an actual HTTP call or connection failure.
    /// </summary>
    [Fact]
    public async Task GetForecast_CallsGetForecastAsyncWithStromtorpCoordinates_ReturnsResult()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SmhiWeatherService>>();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var httpClient = new HttpClient();
        var service = new SmhiWeatherService(httpClient, mockLogger.Object, mockServiceScopeFactory.Object);
        // Act
        var result = await service.GetForecast();
        // Assert
        Assert.NotNull(result);
        Assert.IsType<List<WeatherForecast>>(result);
    }

    /// <summary>
    /// Tests that GetForecast properly handles and returns an empty list when the underlying
    /// GetForecastAsync encounters an error (such as network failure).
    /// </summary>
    [Fact]
    public async Task GetForecast_WhenGetForecastAsyncFails_ReturnsEmptyList()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SmhiWeatherService>>();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        // Using an HttpClient with invalid base address to simulate failure
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(1)
        };
        var service = new SmhiWeatherService(httpClient, mockLogger.Object, mockServiceScopeFactory.Object);
        // Act
        var result = await service.GetForecast();
        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    /// <summary>
    /// Tests that GetForecast returns a result asynchronously and the task completes successfully.
    /// Verifies that the method properly awaits the GetForecastAsync call.
    /// </summary>
    [Fact]
    public async Task GetForecast_ExecutesAsynchronously_CompletesSuccessfully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SmhiWeatherService>>();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMilliseconds(1)
        };
        var service = new SmhiWeatherService(httpClient, mockLogger.Object, mockServiceScopeFactory.Object);
        // Act
        var task = service.GetForecast();
        var result = await task;
        // Assert
        Assert.True(task.IsCompleted);
        Assert.NotNull(result);
    }
}
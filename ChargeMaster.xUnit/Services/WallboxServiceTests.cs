using ChargeMaster.Services;

namespace ChargeMaster.xUnit.Services;

public class WallboxServiceTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly WallboxService _service;

    public WallboxServiceTests()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://192.168.1.205:8080/serialweb/")
        };
        _service = new WallboxService(_httpClient);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsStatus()
    {
        // Act
        var result = await _service.GetStatusAsync();

        // Assert
        // This test requires the wallbox to be reachable at the configured IP
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Serial));
    }

    [Fact]
    public async Task GetTimeAsync_ReturnsTime()
    {
        // Act
        var result = await _service.GetTimeAsync();

        // Assert
        // This test requires the wallbox to be reachable
        Assert.NotNull(result);
    }

    [Fact]
    public async Task SetTimeAsync_ReturnsTrue_OnSuccess()
    {
        // Act
        var result = await _service.SetTimeAsync(DateTime.Now);

        // Assert
        // This test requires the wallbox to be reachable and accepting time updates
        Assert.True(result);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsNull_WhenWallboxIsUnreachable()
    {
        // Arrange - use a non-existent IP to simulate unreachability
        using var tempClient = new HttpClient { BaseAddress = new Uri("http://192.168.1.254:8080/serialweb/") };
        tempClient.Timeout = TimeSpan.FromSeconds(2);
        var unreachableService = new WallboxService(tempClient);

        // Act
        var result = await unreachableService.GetStatusAsync();

        // Assert
        Assert.Null(result);
    }
}

using Microsoft.Extensions.Logging;
using ChargeMaster.Services.Wallbox;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace ChargeMaster.xUnit.Services.Wallbox;

public class WallboxServiceTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly WallboxService _service;

    public WallboxServiceTests()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://192.168.1.205:8080/")
        };
        _service = new WallboxService(_httpClient, new Logger<WallboxService>(new LoggerFactory()));
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    [Fact]
    public async Task GetStatusAsync_OK()
    {
        // Act
        var result = await _service.GetStatusAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Serial > 0);
    }

    [Fact]
    public async Task GetTimeAsync_ok()
    {
        // Act
        var result = await _service.GetTimeAsync();

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task SetTimeAsync_OK()
    {
        // Act
        var t = DateTime.Now;
        // convert to minute precision
        var time = new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0);

        var result = await _service.SetTimeAsync(time);

        // Assert
        Assert.True(result);

        var result1 = await _service.GetTimeAsync();
        Assert.Equal(time, result1);
    }

    [Theory]
    [InlineData(WallboxMode.Available, "ALWAYS_ON")]
    [InlineData(WallboxMode.NotAvailable, "ALWAYS_OFF")]
    [InlineData(WallboxMode.TimerControlled, "SCHEMA")]
    public async Task SetModeAsync_OK(WallboxMode mode, string expectedMode)
    {
        // read starting status
        var startingStat = await _service.GetStatusAsync();

        // Act
        var result = await _service.SetModeAsync(mode);

        var stat = await _service.GetStatusAsync();
        Assert.NotNull(stat);
        Assert.Equal(expectedMode, stat.Mode);

        // Assert
        Assert.True(result);

        // reset starting mode
        await _service.SetModeAsync((WallboxMode)Enum.Parse(typeof(WallboxMode), startingStat!.Mode switch
        {
            "ALWAYS_ON" => "Available",
            "ALWAYS_OFF" => "NotAvailable",
            "SCHEMA" => "TimerControlled",
            _ => throw new InvalidOperationException("Unknown mode")
        }));
    }

    [Fact]
    public async Task GetMeterInfoAsync_OK()
    {
        // Act
        var result = await _service.GetMeterInfoAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.AccEnergy > 64067100);
    }

    [Fact]
    public async Task GetSchemaAsync_OK()
    {
        // Act
        var result = await _service.GetSchemaAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.All(result, r =>
        {
            Assert.True(r.SchemaId > 0);
            Assert.InRange(Int32.Parse(r.Weekday!), 1, 7);
            Assert.False(string.IsNullOrWhiteSpace(r.Start));
            Assert.False(string.IsNullOrWhiteSpace(r.Stop));
        });
    }

    [Fact]
    public async Task GetConfigAsync_OK()
    {
        // Act
        var result = await _service.GetConfigAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.SerialNumber > 0);
        Assert.False(string.IsNullOrWhiteSpace(result.ProgramVersion));
    }

    [Fact]
    public async Task GetSlavesAsync_OK()
    {
        // Act
        var result = await _service.GetSlavesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.All(result, r => Assert.True(r.SerialNumber > 0));
    }
}

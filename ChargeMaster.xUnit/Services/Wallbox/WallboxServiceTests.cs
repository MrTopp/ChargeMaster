using Microsoft.Extensions.Logging;
using ChargeMaster.Services.Wallbox;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace ChargeMaster.xUnit.Services.Wallbox;

public class WallboxServiceTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly WallboxService _service;
    private readonly ITestOutputHelper _output;

    public WallboxServiceTests(ITestOutputHelper output)
    {
        _output = output;
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

    [Fact(Skip="Only for interactive testing")]
    public async Task GetStatusAsync_OK()
    {
        // Act
        var result = await _service.GetStatusAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Serial > 0);
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

    [Fact(Skip="Only for interactive testing")]
    public async Task GetMeterInfoAsync_OK()
    {
        // Act
        var result = await _service.GetMeterInfoAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.AccEnergy > 64067100);
    }

    [Fact(Skip="Only for interactive testing")]
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

    [Fact(Skip="Only for interactive testing")]
    public async Task GetConfigAsync_OK()
    {
        // Act
        var result = await _service.GetConfigAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.SerialNumber > 0);
        Assert.False(string.IsNullOrWhiteSpace(result.ProgramVersion));
    }

    [Fact(Skip="Only for interactive testing")]
    public async Task GetSlavesAsync_OK()
    {
        // Act
        var result = await _service.GetSlavesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.All(result, r => Assert.True(r.SerialNumber > 0));
    }

    [Fact(Skip="Skriver ut effekten i laddboxen många gånger, inget riktigt testfall")]
    public async Task GetMeterInfoAsyncListning_OK()
    {
        for (int i = 0; i < 200; i++)
        {
            var result = await _service.GetMeterInfoAsync();
            if (result == null) continue;   
            // Skriv ut resultatet för att se att det uppdateras
            _output.WriteLine($"{i}. {result.Phase1CurrentEnergy} {result.Phase2CurrentEnergy} {result.Phase3CurrentEnergy} {result.AccEnergy}");
            await Task.Delay(1000, TestContext.Current.CancellationToken);
        }
    }

}

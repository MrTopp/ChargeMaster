using ChargeMaster.Services.VolksWagen;

using Microsoft.Extensions.Logging;

namespace ChargeMaster.xUnit.Services.VolksWagen;

public class VWServiceTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly VWService _service;

    public VWServiceTests()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://127.0.0.1:5211/")
        };
        var logger = new LoggerFactory().CreateLogger<VWService>();
        _service = new VWService(_httpClient, logger);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    [Fact(Skip="Only for interactive testing")]
    public async Task GetStatus_OK()
    {
        var result = await _service.GetStatusAsync();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Vin));
        Assert.NotEqual(VWVehicleState.Unknown, result.VehicleState);
    }

    [Fact(Skip="Only for interactive testing")]
    public async Task GetVehicles_OK()
    {
        var result = await _service.GetVehiclesAsync();
        Assert.NotNull(result);
        Assert.NotNull(result.Vehicles);
        Assert.NotEmpty(result.Vehicles);
        Assert.All(result.Vehicles, v => Assert.False(string.IsNullOrWhiteSpace(v.Vin)));
    }

    [Fact(Skip="Only for interactive testing")]
    public async Task StartCharging_OK()
    {
        var result = await _service.StartChargingAsync();
        Assert.True(result);
    }

    [Fact(Skip="Only for interactive testing")]
    public async Task StopCharging_OK()
    {
        var result = await _service.StopChargingAsync();
        Assert.True(result);
    }

    [Fact(Skip="Only for interactive testing")]
    public async Task StartClimatization_OK()
    {
        var result = await _service.StartClimatizationAsync();
        Assert.True(result);
    }

    [Fact(Skip="Only for interactive testing")]
    public async Task StopClimatization_OK()
    {
        var result = await _service.StopClimatizationAsync();
        Assert.True(result);
    }
}

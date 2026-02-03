using ChargeMaster.Services;
using ChargeMaster.Models;

namespace ChargeMaster.xUnit.Services;

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
        _service = new VWService(_httpClient);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    [Fact]
    public async Task GetStatus_OK()
    {
        var result = await _service.GetStatus();
        Assert.NotNull(result);
        Assert.NotNull(result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Status.Vin));
        Assert.NotEqual(VWVehicleState.Unknown, result.Status.VehicleState);
    }

    [Fact]
    public async Task GetVehicles_OK()
    {
        var result = await _service.GetVehiclesAsync();
        Assert.NotNull(result);
        Assert.NotNull(result.Vehicles);
        Assert.NotEmpty(result.Vehicles);
        Assert.All(result.Vehicles, v => Assert.False(string.IsNullOrWhiteSpace(v.Vin)));
    }

    [Fact]
    public async Task StartCharging_OK()
    {
        var result = await _service.StartChargingAsync();
        Assert.True(result);
    }

    [Fact]
    public async Task StopCharging_OK()
    {
        var result = await _service.StopChargingAsync();
        Assert.True(result);
    }

    [Fact]
    public async Task StartClimatization_OK()
    {
        var result = await _service.StartClimatizationAsync();
        Assert.True(result);
    }

    [Fact]
    public async Task StopClimatization_OK()
    {
        var result = await _service.StopClimatizationAsync();
        Assert.True(result);
    }
}

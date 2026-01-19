using ChargeMaster.Workers;

namespace ChargeMaster.xUnit.Workers;

public class VWServiceTests : IDisposable
{
    private readonly HttpClient httpClient;
    private readonly VWService service;

    public VWServiceTests()
    {
        httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://127.0.0.1:5211")
        };
        service = new VWService(httpClient);
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }

    [Fact]
    public async Task GetStatus_OK()
    {
        var result = await service.GetStatus();
        Assert.NotNull(result);
        Assert.NotNull(result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Status.Vin));
    }

    [Fact]
    public async Task GetVehicles_OK()
    {
        var result = await service.GetVehiclesAsync();
        Assert.NotNull(result);
        Assert.NotNull(result.Vehicles);
        Assert.NotEmpty(result.Vehicles);
        Assert.All(result.Vehicles, v => Assert.False(string.IsNullOrWhiteSpace(v.Vin)));
    }

    [Fact]
    public async Task StartCharging_OK()
    {
        var result = await service.StartChargingAsync();
        Assert.True(result);
    }

    [Fact]
    public async Task StopCharging_OK()
    {
        var result = await service.StopChargingAsync();
        Assert.True(result);
    }

    [Fact]
    public async Task StartClimatization_OK()
    {
        var result = await service.StartClimatizationAsync();
        Assert.True(result);
    }

    [Fact]
    public async Task StopClimatization_OK()
    {
        var result = await service.StopClimatizationAsync();
        Assert.True(result);
    }
}

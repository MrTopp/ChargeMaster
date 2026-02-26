using ChargeMaster.Services.Daikin;

using Microsoft.Extensions.Logging;

namespace ChargeMaster.xUnit.Services.Daikin;

public class DaikinServiceTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly DaikinService _service;

    public DaikinServiceTests()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://192.168.1.156/"),
            Timeout = TimeSpan.FromSeconds(10)
        };
        _service = new DaikinService(_httpClient, new Logger<DaikinService>(new LoggerFactory()));
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    [Fact]
    public async Task GetBasicInfoAsync_ReturnsDeviceInfo()
    {
        // Act
        var result = await _service.GetBasicInfoAsync();

        // Assert
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Name));
        Assert.False(string.IsNullOrWhiteSpace(result.MacAddress));
    }

    [Fact]
    public async Task GetSensorInfoAsync_ReturnsTemperatures()
    {
        // Act
        var result = await _service.GetSensorInfoAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.IndoorTemperature);
        Assert.InRange(result.IndoorTemperature.Value, -20, 50);
        Assert.NotNull(result.OutdoorTemperature);
        Assert.InRange(result.OutdoorTemperature.Value, -40, 60);
    }

    [Fact]
    public async Task GetControlInfoAsync_ReturnsSettings()
    {
        // Act
        var result = await _service.GetControlInfoAsync();

        // Assert
        Assert.NotNull(result);
        Assert.InRange(result.Power, 0, 1);
        Assert.InRange(result.Mode, 0, 7);
        Assert.False(string.IsNullOrWhiteSpace(result.ModeDescription));
    }

    [Fact]
    public async Task SetTargetTemperatureAsync_RestoresOriginal()
    {
        // Arrange - spara originalvärden
        var original = await _service.GetControlInfoAsync();
        Assert.NotNull(original);

        var originalTemp = original.TargetTemperature;
        var testTemp = originalTemp.HasValue ? originalTemp.Value + 1 : 22.0;

        try
        {
            // Act - ändra temperatur
            var result = await _service.SetTargetTemperatureAsync(testTemp);

            // Assert
            Assert.True(result);

            var updated = await _service.GetControlInfoAsync();
            Assert.NotNull(updated);
            Assert.Equal(testTemp, updated.TargetTemperature);
        }
        finally
        {
            // Återställ originalvärde
            if (originalTemp.HasValue)
            {
                await _service.SetTargetTemperatureAsync(originalTemp.Value);
            }
        }
    }

    [Fact]
    public async Task TurnOffAndOn_RestoresOriginalState()
    {
        // Arrange - spara originalläge
        var original = await _service.GetControlInfoAsync();
        Assert.NotNull(original);

        var originalPower = original.Power;

        try
        {
            // Act - stäng av
            var offResult = await _service.TurnOffAsync();
            Assert.True(offResult);

            var offState = await _service.GetControlInfoAsync();
            Assert.NotNull(offState);
            Assert.False(offState.IsOn);

            // Act - slå på
            var onResult = await _service.TurnOnAsync();
            Assert.True(onResult);

            var onState = await _service.GetControlInfoAsync();
            Assert.NotNull(onState);
            Assert.True(onState.IsOn);
        }
        finally
        {
            // Återställ originalläge
            await _service.SetControlInfoAsync(original);
        }
    }

    [Fact]
    public async Task SetControlInfoAsync_RestoresOriginal()
    {
        // Arrange - spara originalvärden
        var original = await _service.GetControlInfoAsync();
        Assert.NotNull(original);

        var modified = new DaikinControlInfo
        {
            Power = original.Power,
            Mode = original.Mode,
            TargetTemperature = original.TargetTemperature.HasValue
                ? original.TargetTemperature.Value + 1
                : 22.0,
            FanRate = original.FanRate,
            FanDirection = original.FanDirection
        };

        try
        {
            // Act
            var result = await _service.SetControlInfoAsync(modified);

            // Assert
            Assert.True(result);

            var updated = await _service.GetControlInfoAsync();
            Assert.NotNull(updated);
            Assert.Equal(modified.TargetTemperature, updated.TargetTemperature);
        }
        finally
        {
            // Återställ originalvärden
            await _service.SetControlInfoAsync(original);
        }
    }
}

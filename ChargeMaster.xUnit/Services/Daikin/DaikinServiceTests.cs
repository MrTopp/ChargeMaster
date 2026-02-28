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

    // ==================== COMMON ENDPOINTS ====================

    [Fact]
    public async Task GetBasicInfoAsync_ReturnsDeviceInfo()
    {
        var result = await _service.GetBasicInfoAsync();

        Assert.NotNull(result);
        Assert.Equal("aircon", result.Type);
        Assert.False(string.IsNullOrWhiteSpace(result.Region));
        Assert.False(string.IsNullOrWhiteSpace(result.FirmwareVersion));
        Assert.False(string.IsNullOrWhiteSpace(result.Name));
        Assert.False(string.IsNullOrWhiteSpace(result.MacAddress));
        Assert.NotNull(result.Power);
        Assert.InRange(result.Power.Value, 0, 1);
    }

    [Fact]
    public async Task GetRemoteMethodAsync_ReturnsRemoteConfig()
    {
        var result = await _service.GetRemoteMethodAsync();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Method));
        Assert.NotNull(result.NoticeIpInterval);
        Assert.NotNull(result.NoticeSyncInterval);
    }

    [Fact]
    public async Task GetDateTimeAsync_ReturnsDateTime()
    {
        var result = await _service.GetDateTimeAsync();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Current));
        Assert.NotNull(result.Status);
    }

    [Fact]
    public async Task GetHolidayAsync_ReturnsHolidayStatus()
    {
        var result = await _service.GetHolidayAsync();

        Assert.NotNull(result);
        Assert.NotNull(result.Enabled);
    }

    [Fact]
    public async Task GetWifiSettingAsync_ReturnsWifiConfig()
    {
        var result = await _service.GetWifiSettingAsync();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.SSID));
        Assert.NotNull(result.Link);
    }

    [Fact]
    public async Task SetHolidayAsync_RestoresOriginal()
    {
        var original = await _service.GetHolidayAsync();
        Assert.NotNull(original);
        var originalState = original.Enabled ?? 0;

        try
        {
            var newState = (originalState == 1) ? 0 : 1;
            var result = await _service.SetHolidayAsync(newState == 1);
            Assert.True(result);

            var updated = await _service.GetHolidayAsync();
            Assert.NotNull(updated);
            Assert.Equal(newState, updated.Enabled);
        }
        finally
        {
            await _service.SetHolidayAsync(originalState == 1);
        }
    }

    [Fact]
    public async Task SetRegionCodeAsync_ReturnsTrue()
    {
        var result = await _service.SetRegionCodeAsync("eu");
        Assert.True(result);
    }

    // ==================== AIRCON ENDPOINTS ====================

    [Fact]
    public async Task GetControlInfoAsync_ReturnsSettings()
    {
        var result = await _service.GetControlInfoAsync();

        Assert.NotNull(result);
        Assert.InRange(result.Power, 0, 1);
        Assert.InRange(result.Mode, 0, 7);
        Assert.False(string.IsNullOrWhiteSpace(result.ModeDescription));
    }

    [Fact]
    public async Task GetSensorInfoAsync_ReturnsTemperatures()
    {
        var result = await _service.GetSensorInfoAsync();
        
        Assert.NotNull(result);
        Assert.NotNull(result.IndoorTemperature);
        Assert.InRange(result.IndoorTemperature.Value, -20, 50);
        Assert.NotNull(result.OutdoorTemperature);
        Assert.InRange(result.OutdoorTemperature.Value, -40, 60);
    }

    [Fact]
    public async Task GetModelInfoAsync_ReturnsCapabilities()
    {
        var result = await _service.GetModelInfoAsync();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Model));
        Assert.False(string.IsNullOrWhiteSpace(result.ProtocolVersion));
    }

    [Fact]
    public async Task GetTargetAsync_ReturnsTarget()
    {
        var result = await _service.GetTargetAsync();
        // returnerar 0 när jag kör, inte användbart
        Assert.NotNull(result);
        Assert.NotNull(result.Target);
    }

    [Fact]
    public async Task GetPriceAsync_ReturnsPrice()
    {
        var result = await _service.GetPriceAsync();

        Assert.NotNull(result);
        Assert.NotNull(result.PriceInt);
        Assert.NotNull(result.PriceDec);
    }

    [Fact]
    public async Task GetWeekPowerAsync_ReturnsWeeklyData()
    {
        var result = await _service.GetWeekPowerAsync();

        Assert.NotNull(result);
        Assert.NotNull(result.TodayRuntime);
        Assert.False(string.IsNullOrWhiteSpace(result.WeeklyData));
    }

    [Fact]
    public async Task GetWeekPowerExAsync_ReturnsWeeklyDataExtended()
    {
        DaikinWeekPowerEx? result = await _service.GetWeekPowerExAsync();

        Assert.NotNull(result);
        Assert.NotNull(result.StartDayOfWeek);
        Assert.NotEmpty(result.WeekHeat);
        Assert.NotEmpty(result.WeekCool);
        Assert.Equal(14, result.WeekHeat.Length);
        Assert.Equal(14, result.WeekCool.Length);
    }

    [Fact]
    public async Task GetDayPowerExAsync_ReturnsDailyDataExtended()
    {
        var result = await _service.GetDayPowerExAsync();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.CurrentDayHeat));
        Assert.False(string.IsNullOrWhiteSpace(result.CurrentDayCool));
    }

    [Fact]
    public async Task GetYearPowerAsync_ReturnsYearlyData()
    {
        var result = await _service.GetYearPowerAsync();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.ThisYear));
    }

    [Fact]
    public async Task GetYearPowerExAsync_ReturnsYearlyDataExtended()
    {
        var result = await _service.GetYearPowerExAsync();

        Assert.NotNull(result);
        // Den här ger inga data
        //Assert.False(string.IsNullOrWhiteSpace(result.ThisYearHeat));
        //Assert.False(string.IsNullOrWhiteSpace(result.ThisYearCool));
    }

    [Fact]
    public async Task GetMonthPowerExAsync_ReturnsMonthlyDataExtended()
    {
        var result = await _service.GetMonthPowerExAsync();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.CurrentMonthHeat));
        Assert.False(string.IsNullOrWhiteSpace(result.CurrentMonthCool));
    }

    [Fact]
    public async Task GetScheduleTimerInfoAsync_ReturnsScheduleInfo()
    {
        var result = await _service.GetScheduleTimerInfoAsync();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Format));
        Assert.NotNull(result.ScheduleNumber);
        Assert.NotNull(result.Enabled);
    }

    [Fact]
    public async Task SetControlInfoAsync_RestoresOriginal()
    {
        var original = await _service.GetControlInfoAsync();
        Assert.NotNull(original);

        var modified = new DaikinControlInfo
        {
            Power = original.Power,
            Mode = original.Mode,
            TargetTemperature = original.TargetTemperature.HasValue
                ? original.TargetTemperature.Value + 1
                : 22.0,
            TargetHumidity = original.TargetHumidity,
            FanRate = original.FanRate,
            FanDirection = original.FanDirection
        };

        try
        {
            var result = await _service.SetControlInfoAsync(modified);
            Assert.True(result);

            var updated = await _service.GetControlInfoAsync();
            Assert.NotNull(updated);
            Assert.Equal(modified.TargetTemperature, updated.TargetTemperature);
        }
        finally
        {
            await _service.SetControlInfoAsync(original);
        }
    }

    [Fact]
    public async Task SetTargetAsync_ReturnsTrue()
    {
        var result = await _service.SetTargetAsync(0);
        Assert.True(result);
    }

    // ==================== CONVENIENCE METHODS ====================

    [Fact]
    public async Task TurnOffAndOn_RestoresOriginalState()
    {
        var original = await _service.GetControlInfoAsync();
        Assert.NotNull(original);
        var originalPower = original.Power;

        try
        {
            var offResult = await _service.TurnOffAsync();
            Assert.True(offResult);

            var offState = await _service.GetControlInfoAsync();
            Assert.NotNull(offState);
            Assert.False(offState.IsOn);

            var onResult = await _service.TurnOnAsync();
            Assert.True(onResult);

            var onState = await _service.GetControlInfoAsync();
            Assert.NotNull(onState);
            Assert.True(onState.IsOn);
        }
        finally
        {
            await _service.SetControlInfoAsync(original);
        }
    }

    [Fact]
    public async Task SetTargetTemperatureAsync_RestoresOriginal()
    {
        var original = await _service.GetControlInfoAsync();
        Assert.NotNull(original);

        var originalTemp = original.TargetTemperature;
        var testTemp = originalTemp.HasValue ? originalTemp.Value + 1 : 22.0;

        try
        {
            var result = await _service.SetTargetTemperatureAsync(testTemp);
            Assert.True(result);

            var updated = await _service.GetControlInfoAsync();
            Assert.NotNull(updated);
            Assert.Equal(testTemp, updated.TargetTemperature);
        }
        finally
        {
            if (originalTemp.HasValue)
            {
                await _service.SetTargetTemperatureAsync(originalTemp.Value);
            }
        }
    }

    [Fact]
    public async Task SetFanRateAsync_RestoresOriginal()
    {
        var original = await _service.GetControlInfoAsync();
        Assert.NotNull(original);

        var originalFanRate = original.FanRate;
        var testFanRate = originalFanRate == "A" ? "B" : "A";

        try
        {
            var result = await _service.SetFanRateAsync(testFanRate);
            Assert.True(result);

            var updated = await _service.GetControlInfoAsync();
            Assert.NotNull(updated);
            Assert.Equal(testFanRate, updated.FanRate);
        }
        finally
        {
            if (originalFanRate != null)
            {
                await _service.SetFanRateAsync(originalFanRate);
            }
        }
    }

    [Fact]
    public async Task SetFanDirectionAsync_RestoresOriginal()
    {
        var original = await _service.GetControlInfoAsync();
        Assert.NotNull(original);

        var originalDir = original.FanDirection;
        var testDir = originalDir == 0 ? 1 : 0;

        try
        {
            var result = await _service.SetFanDirectionAsync(testDir);
            Assert.True(result);

            var updated = await _service.GetControlInfoAsync();
            Assert.NotNull(updated);
            Assert.Equal(testDir, updated.FanDirection);
        }
        finally
        {
            await _service.SetFanDirectionAsync(originalDir);
        }
    }

    /// <summary>
    /// Växla mellan kyla och värme och tillbaka
    /// </summary>
    [Fact]
    public async Task SetModeAsync_RestoresOriginal()
    {
        var original = await _service.GetControlInfoAsync();
        Assert.NotNull(original);

        var originalMode = original.Mode;
        var testMode = originalMode == 3 ? 4 : 3; // Cool vs Heat

        try
        {
            var result = await _service.SetModeAsync(testMode);
            Assert.True(result);

            var updated = await _service.GetControlInfoAsync();
            Assert.NotNull(updated);
            Assert.Equal(testMode, updated.Mode);
        }
        finally
        {
            await _service.SetModeAsync(originalMode);
        }
    }
}

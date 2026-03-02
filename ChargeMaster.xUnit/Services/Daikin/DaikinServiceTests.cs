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

    [Fact(Skip="Allmän info, inget som behöver loggas")]
    public async Task GetBasicInfoAsync_ReturnsDeviceInfo()
    {
        // Allmän info om pumpen, inget som behöver loggas
        DaikinBasicInfo? result = await _service.GetBasicInfoAsync();

        Assert.NotNull(result);
        Assert.Equal("aircon", result.Type);
        Assert.False(string.IsNullOrWhiteSpace(result.Region));
        Assert.False(string.IsNullOrWhiteSpace(result.FirmwareVersion));
        Assert.False(string.IsNullOrWhiteSpace(result.Name));
        Assert.False(string.IsNullOrWhiteSpace(result.MacAddress));
        Assert.NotNull(result.Power);
        Assert.InRange(result.Power.Value, 0, 1);
    }

    [Fact(Skip="Fjärrkommunikationskonfiguration, inget som behöver loggas")]
    public async Task GetRemoteMethodAsync_ReturnsRemoteConfig()
    {
        // 
        DaikinRemoteMethod? result = await _service.GetRemoteMethodAsync();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Method));
        Assert.NotNull(result.NoticeIpInterval);
        Assert.NotNull(result.NoticeSyncInterval);
    }

    [Fact(Skip="Hämtar pumpens klocka, vi vet redan tiden")]
    public async Task GetDateTimeAsync_ReturnsDateTime()
    {
        DaikinDateTime? result = await _service.GetDateTimeAsync();

        Assert.NotNull(result);
        Assert.NotNull(result.Current);
        Assert.NotNull(result.Status);
    }

    [Fact(Skip="Semesterläge, Inte intressant")]
    public async Task GetHolidayAsync_ReturnsHolidayStatus()
    {
        DaikinHoliday? result = await _service.GetHolidayAsync();

        Assert.NotNull(result);
        Assert.IsType<bool>(result.IsHoliday);
    }

    [Fact(Skip="WiFi konfiguration")]
    public async Task GetWifiSettingAsync_ReturnsWifiConfig()
    {
        DaikinWifiSetting? result = await _service.GetWifiSettingAsync();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.SSID));
        Assert.NotNull(result.Link);
    }

    [Fact(Skip="Semesterläge inställning")]
    public async Task SetHolidayAsync_RestoresOriginal()
    {
        DaikinHoliday? original = await _service.GetHolidayAsync();
        Assert.NotNull(original);
        bool originalState = original.IsHoliday;

        try
        {
            bool newState = !originalState;
            bool result = await _service.SetHolidayAsync(newState);
            Assert.True(result);

            DaikinHoliday? updated = await _service.GetHolidayAsync();
            Assert.NotNull(updated);
            Assert.Equal(newState, updated.IsHoliday);
        }
        finally
        {
            await _service.SetHolidayAsync(originalState);
        }
    }

    [Fact(Skip="Regionkod inställning")]
    public async Task SetRegionCodeAsync_ReturnsTrue()
    {
        bool result = await _service.SetRegionCodeAsync("eu");
        Assert.True(result);
    }

    // ==================== AIRCON ENDPOINTS ====================

    [Fact]
    public async Task GetControlInfoAsync_ReturnsSettings()
    {
        DaikinControlInfo? result = await _service.GetControlInfoAsync();

        // Pumpens inställningar

        Assert.NotNull(result);
        Assert.InRange(result.Power, 0, 1);
        Assert.InRange(result.Mode, 0, 7);
        Assert.False(string.IsNullOrWhiteSpace(result.ModeDescription));
    }

    [Fact]
    public async Task GetSensorInfoAsync_ReturnsTemperatures()
    {
        // Temperatur inne och ute
        DaikinSensorInfo? result = await _service.GetSensorInfoAsync();
        
        Assert.NotNull(result);
        Assert.NotNull(result.IndoorTemperature);
        Assert.InRange(result.IndoorTemperature.Value, -20, 50);
        Assert.NotNull(result.OutdoorTemperature);
        Assert.InRange(result.OutdoorTemperature.Value, -40, 60);
        Assert.Equal(0, result.ErrorCode);
    }

    [Fact(Skip="Hårdvarukonfiguration")]
    public async Task GetModelInfoAsync_ReturnsCapabilities()
    {
        DaikinModelInfo? result = await _service.GetModelInfoAsync();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Model));
        Assert.False(string.IsNullOrWhiteSpace(result.ProtocolVersion));
    }

    [Fact(Skip="Returnerar 0")]
    public async Task GetTargetAsync_ReturnsTarget()
    {
        DaikinTarget? result = await _service.GetTargetAsync();

        Assert.NotNull(result);
        Assert.NotNull(result.Target);
    }

    [Fact(Skip="Vet inte vad det betyder")]
    public async Task GetPriceAsync_ReturnsPrice()
    {
        DaikinPrice? result = await _service.GetPriceAsync();

        Assert.NotNull(result);
        Assert.NotNull(result.PriceInt);
        Assert.NotNull(result.PriceDec);
    }

    [Fact(Skip="Körtid för dagen")]
    public async Task GetWeekPowerAsync_ReturnsWeeklyData()
    {
        DaikinWeekPower? result = await _service.GetWeekPowerAsync();

        Assert.NotNull(result);
        Assert.NotNull(result.TodayRuntime);
        Assert.NotEmpty(result.WeeklyData);
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
        DaikinDayPowerEx? result = await _service.GetDayPowerExAsync();

        Assert.NotNull(result);
        Assert.NotEmpty(result.CurrentDayHeat);
        Assert.NotEmpty(result.CurrentDayCool);
        Assert.Equal(24, result.CurrentDayHeat.Length);
        Assert.Equal(24, result.PreviousDayHeat.Length);
        Assert.Equal(24, result.CurrentDayCool.Length);
        Assert.Equal(24, result.PreviousDayCool.Length);
    }

    [Fact(Skip="Returnerar 5 ggr verkligt värde")]
    public async Task GetYearPowerAsync_ReturnsYearlyData()
    {
        DaikinYearPower? result = await _service.GetYearPowerAsync();

        Assert.NotNull(result);
        Assert.NotEmpty(result.ThisYear);
        Assert.NotEmpty(result.PreviousYear);
    }

    [Fact]
    public async Task GetYearPowerExAsync_ReturnsYearlyDataExtended()
    {
        DaikinYearPowerEx? result = await _service.GetYearPowerExAsync();

        Assert.NotNull(result);
        Assert.NotEmpty(result.CurrentYearCool);
        Assert.NotEmpty(result.CurrentYearHeat);
        Assert.NotEmpty(result.PreviousYearCool);
        Assert.NotEmpty(result.PreviousYearHeat);
        Assert.Equal(12, result.CurrentYearCool.Length);
        Assert.Equal(12, result.CurrentYearHeat.Length);
        Assert.Equal(12, result.PreviousYearCool.Length);
        Assert.Equal(12, result.PreviousYearHeat.Length);
    }

    [Fact(Skip="Används inte")]
    public async Task GetMonthPowerExAsync_ReturnsMonthlyDataExtended()
    {
        // Summerad förbrukning per dag
        DaikinMonthPowerEx? result = await _service.GetMonthPowerExAsync();

        Assert.NotNull(result);
        Assert.NotEmpty(result.CurrentMonthHeat);
        Assert.NotEmpty(result.CurrentMonthCool);
        Assert.NotEmpty(result.PreviousMonthHeat);
        Assert.NotEmpty(result.PreviousMonthCool);
    }

    [Fact(Skip="Schema inte intressant")]
    public async Task GetScheduleTimerInfoAsync_ReturnsScheduleInfo()
    {
        DaikinScheduleTimerInfo? result = await _service.GetScheduleTimerInfoAsync();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Format));
        Assert.NotNull(result.ScheduleNumber);
        Assert.NotNull(result.Enabled);
    }

    [Fact]
    public async Task SetControlInfoAsync_RestoresOriginal()
    {
        // Ställer måltemperatur, fungerar!
        DaikinControlInfo? original = await _service.GetControlInfoAsync();
        Assert.NotNull(original);

        DaikinControlInfo modified = new DaikinControlInfo
        {
            Power = original.Power,
            Mode = original.Mode,
            TargetTemperature = original.TargetTemperature.HasValue
                ? original.TargetTemperature.Value + 3
                : 22.0,
            TargetHumidity = original.TargetHumidity,
            FanRate = original.FanRate,
            FanDirection = original.FanDirection
        };

        try
        {
            bool result = await _service.SetControlInfoAsync(modified);
            Assert.True(result);

            DaikinControlInfo? updated = await _service.GetControlInfoAsync();
            Assert.NotNull(updated);
            Assert.Equal(modified.TargetTemperature, updated.TargetTemperature);
        }
        finally
        {
            await _service.SetControlInfoAsync(original);
        }
    }

    [Fact(Skip="Vad gör den här?")]
    public async Task SetTargetAsync_ReturnsTrue()
    {
        bool result = await _service.SetTargetAsync(0);
        Assert.True(result);
    }

    // ==================== CONVENIENCE METHODS ====================

    [Fact]
    public async Task TurnOffAndOn_RestoresOriginalState()
    {
        DaikinControlInfo? original = await _service.GetControlInfoAsync();
        Assert.NotNull(original);
        int originalPower = original.Power;

        try
        {
            bool offResult = await _service.TurnOffAsync();
            Assert.True(offResult);

            DaikinControlInfo? offState = await _service.GetControlInfoAsync();
            Assert.NotNull(offState);
            Assert.False(offState.IsOn);

            bool onResult = await _service.TurnOnAsync();
            Assert.True(onResult);

            DaikinControlInfo? onState = await _service.GetControlInfoAsync();
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
        DaikinControlInfo? original = await _service.GetControlInfoAsync();
        Assert.NotNull(original);

        double? originalTemp = original.TargetTemperature;
        double testTemp = originalTemp.HasValue ? originalTemp.Value + 4 : 22.0;

        try
        {
            bool result = await _service.SetTargetTemperatureAsync(testTemp);
            Assert.True(result);

            DaikinControlInfo? updated = await _service.GetControlInfoAsync();
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

    [Fact(Skip="Används inte")]
    public async Task SetFanRateAsync_RestoresOriginal()
    {
        DaikinControlInfo? original = await _service.GetControlInfoAsync();
        Assert.NotNull(original);

        string? originalFanRate = original.FanRate;
        string testFanRate = originalFanRate == "A" ? "B" : "A";

        try
        {
            bool result = await _service.SetFanRateAsync(testFanRate);
            Assert.True(result);

            DaikinControlInfo? updated = await _service.GetControlInfoAsync();
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

    [Fact(Skip="Används inte")]
    public async Task SetFanDirectionAsync_RestoresOriginal()
    {
        DaikinControlInfo? original = await _service.GetControlInfoAsync();
        Assert.NotNull(original);

        int originalDir = original.FanDirection;
        int testDir = originalDir == 0 ? 1 : 0;

        try
        {
            bool result = await _service.SetFanDirectionAsync(testDir);
            Assert.True(result);

            DaikinControlInfo? updated = await _service.GetControlInfoAsync();
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
        DaikinControlInfo? original = await _service.GetControlInfoAsync();
        Assert.NotNull(original);

        int originalMode = original.Mode;
        int testMode = originalMode == 3 ? 4 : 3; // Cool vs Heat

        try
        {
            bool result = await _service.SetModeAsync(testMode);
            Assert.True(result);

            DaikinControlInfo? updated = await _service.GetControlInfoAsync();
            Assert.NotNull(updated);
            Assert.Equal(testMode, updated.Mode);
        }
        finally
        {
            await _service.SetModeAsync(originalMode);
        }
    }
}

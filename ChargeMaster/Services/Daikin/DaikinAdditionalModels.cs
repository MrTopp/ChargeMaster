namespace ChargeMaster.Services.Daikin;

public class DaikinRemoteMethod
{
    public string? Method { get; set; }
    public int? NoticeIpInterval { get; set; }
    public int? NoticeSyncInterval { get; set; }
}

public class DaikinDateTime
{
    public int? Status { get; set; }
    public string? Current { get; set; }
    public string? Region { get; set; }
    public int? DaylightSaving { get; set; }
    public int? TimeZone { get; set; }
}

public class DaikinHoliday
{
    public int? Enabled { get; set; }
}

public class DaikinWifiSetting
{
    public string? SSID { get; set; }
    public string? Security { get; set; }
    public int? Link { get; set; }
}

public class DaikinModelInfo
{
    public string? Model { get; set; }
    public string? Type { get; set; }
    public string ProtocolVersion { get; set; }
    public int? ControlProtocolVersion { get; set; }
    public int HasHumidity { get; set; }
    public int HasHumiditySensor { get; set; }
    public int HasTemperatureSensor { get; set; }
    public int HasScheduleTimer { get; set; }
    public int HasFanRateControl { get; set; }
    public int HasFanDirectionControl { get; set; }
    public int HasOnOffTimer { get; set; }
}

public class DaikinTarget
{
    public int? Target { get; set; }
}

public class DaikinPrice
{
    public int? PriceInt { get; set; }
    public int? PriceDec { get; set; }
}

public class DaikinWeekPower
{
    public int? TodayRuntime { get; set; }
    public string? WeeklyData { get; set; }
}

public class DaikinWeekPowerEx
{
    /// <summary>
    /// Startdag för veckan (0=Sön, 1=Mån, ... 6=Lör).
    /// </summary>
    public int? StartDayOfWeek { get; set; }

    /// <summary>
    /// Daglig energiförbrukning för uppvärmning i 100W-enheter (dela med 10 för kWh).
    /// Första värdet är idag, därefter bakåt.
    /// </summary>
    public int[] WeekHeat { get; set; } = [];

    /// <summary>
    /// Daglig energiförbrukning för kylning i 100W-enheter (dela med 10 för kWh).
    /// Första värdet är idag, därefter bakåt.
    /// </summary>
    public int[] WeekCool { get; set; } = [];
}

public class DaikinDayPowerEx
{
    public string? CurrentDayHeat { get; set; }
    public string? PreviousDayHeat { get; set; }
    public string? CurrentDayCool { get; set; }
    public string? PreviousDayCool { get; set; }
}

public class DaikinYearPower
{
    public string? PreviousYear { get; set; }
    public string? ThisYear { get; set; }
}

public class DaikinYearPowerEx
{
    public string? PreviousYearHeat { get; set; }
    public string? PreviousYearCool { get; set; }
    public string? ThisYearHeat { get; set; }
    public string? ThisYearCool { get; set; }
}

public class DaikinMonthPowerEx
{
    public string? CurrentMonthHeat { get; set; }
    public string? PreviousMonthHeat { get; set; }
    public string? CurrentMonthCool { get; set; }
    public string? PreviousMonthCool { get; set; }
}

public class DaikinScheduleTimerInfo
{
    public string? Format { get; set; }
    public int? ScheduleNumber { get; set; }
    public int? SchedulePerDay { get; set; }
    public int? Enabled { get; set; }
    public int? ActiveNumber { get; set; }
}

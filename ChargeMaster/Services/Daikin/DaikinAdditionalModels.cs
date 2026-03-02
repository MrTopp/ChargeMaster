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
    public DateTime? Current { get; set; }
    public string? Region { get; set; }
    public int? DaylightSaving { get; set; }
    public int? TimeZone { get; set; }
}

public class DaikinHoliday
{
    public bool IsHoliday { get; set; }
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
    /// <summary>
    /// Dagens körtid i minuter.
    /// </summary>
    public int? TodayRuntime { get; set; }

    /// <summary>
    /// Daglig energiförbrukning i 100W-enheter (dela med 10 för kWh).
    /// Första värdet är idag, därefter bakåt.
    /// </summary>
    public int[] WeeklyData { get; set; } = [];
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
    /// <summary>
    /// Innevarande dags timförbrukning för uppvärmning i 100W-enheter (dela med 10 för kWh).
    /// 24 värden för varje timme av dagen.
    /// </summary>
    public int[] CurrentDayHeat { get; set; } = [];

    /// <summary>
    /// Förra dags timförbrukning för uppvärmning i 100W-enheter (dela med 10 för kWh).
    /// 24 värden för varje timme av dagen.
    /// </summary>
    public int[] PreviousDayHeat { get; set; } = [];

    /// <summary>
    /// Innevarande dags timförbrukning för kylning i 100W-enheter (dela med 10 för kWh).
    /// 24 värden för varje timme av dagen.
    /// </summary>
    public int[] CurrentDayCool { get; set; } = [];

    /// <summary>
    /// Förra dags timförbrukning för kylning i 100W-enheter (dela med 10 för kWh).
    /// 24 värden för varje timme av dagen.
    /// </summary>
    public int[] PreviousDayCool { get; set; } = [];
}

public class DaikinYearPower
{
    /// <summary>
    /// Förra årets månatlig energiförbrukning i 100W-enheter (dela med 10 för kWh).
    /// 12 värden för varje månad.
    /// </summary>
    public int[] PreviousYear { get; set; } = [];

    /// <summary>
    /// Innevarande årets månatlig energiförbrukning i 100W-enheter (dela med 10 för kWh).
    /// Värden för de månader som har passerat.
    /// </summary>
    public int[] ThisYear { get; set; } = [];
}

public class DaikinYearPowerEx
{
    /// <summary>
    /// Förra årets månatlig energiförbrukning för uppvärmning i 100W-enheter.
    /// </summary>
    public int[] PreviousYearHeat { get; set; } = [];

    /// <summary>
    /// Förra årets månatlig energiförbrukning för kylning i 100W-enheter.
    /// </summary>
    public int[] PreviousYearCool { get; set; } = [];

    /// <summary>
    /// Innevarande årets månatlig energiförbrukning för uppvärmning i 100W-enheter.
    /// </summary>
    public int[] CurrentYearHeat { get; set; } = [];

    /// <summary>
    /// Innevarande årets månatlig energiförbrukning för kylning i 100W-enheter.
    /// </summary>
    public int[] CurrentYearCool { get; set; } = [];
}

public class DaikinMonthPowerEx
{
    /// <summary>
    /// Innevarande månads daglig energiförbrukning för uppvärmning i 100W-enheter.
    /// </summary>
    public int[] CurrentMonthHeat { get; set; } = [];

    /// <summary>
    /// Förra månads daglig energiförbrukning för uppvärmning i 100W-enheter.
    /// </summary>
    public int[] PreviousMonthHeat { get; set; } = [];

    /// <summary>
    /// Innevarande månads daglig energiförbrukning för kylning i 100W-enheter.
    /// </summary>
    public int[] CurrentMonthCool { get; set; } = [];

    /// <summary>
    /// Förra månads daglig energiförbrukning för kylning i 100W-enheter.
    /// </summary>
    public int[] PreviousMonthCool { get; set; } = [];
}

public class DaikinScheduleTimerInfo
{
    public string? Format { get; set; }
    public int? ScheduleNumber { get; set; }
    public int? SchedulePerDay { get; set; }
    public int? Enabled { get; set; }
    public int? ActiveNumber { get; set; }
}

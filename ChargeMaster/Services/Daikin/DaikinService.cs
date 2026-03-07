using System.Web;

namespace ChargeMaster.Services.Daikin;

/// <summary>
/// Tjänst för kommunikation med Daikin luftvärmepump via lokalt REST API.
/// Daikins API returnerar data i formatet key=value separerade med komma.
/// Baserad på dokumentation för BRP069C4x WiFi-modul med firmware &lt; 2.8.0.
/// </summary>
/// <param name="httpClient">HTTP-klienten konfigurerad med basadressen till Daikin-enheten.</param>
/// <param name="logger">Logger för diagnostik och felrapportering.</param>
public class DaikinService(HttpClient httpClient, ILogger<DaikinService> logger)
{
    // ==================== COMMON ENDPOINTS ====================

    /// <summary>
    /// Hämtar basinformation om Daikin-enheten (namn, MAC-adress, firmware).
    /// </summary>
    public async Task<DaikinBasicInfo?> GetBasicInfoAsync()
    {
        try
        {
            var response = await httpClient.GetStringAsync("/common/basic_info");
            var data = ParseResponse(response);

            return new DaikinBasicInfo
            {
                Type = data.GetValueOrDefault("type"),
                Region = data.GetValueOrDefault("reg"),
                DaylightSaving = ParseInt(data.GetValueOrDefault("dst")),
                FirmwareVersion = data.GetValueOrDefault("ver"),
                Revision = data.GetValueOrDefault("rev"),
                Power = ParseInt(data.GetValueOrDefault("pow")),
                ErrorCode = ParseInt(data.GetValueOrDefault("err")),
                Name = data.TryGetValue("name", out var name)
                    ? HttpUtility.UrlDecode(name)
                    : null,
                Method = data.GetValueOrDefault("method"),
                Port = ParseInt(data.GetValueOrDefault("port")),
                Id = data.GetValueOrDefault("id"),
                AdapterKind = ParseInt(data.GetValueOrDefault("adp_kind")),
                ProtocolVersion = data.GetValueOrDefault("pv"),
                ControlProtocolVersion = data.GetValueOrDefault("cpv"),
                Led = ParseInt(data.GetValueOrDefault("led")),
                MacAddress = data.GetValueOrDefault("mac"),
                Ssid = data.GetValueOrDefault("ssid"),
                AdapterMode = data.GetValueOrDefault("adp_mode"),
                WifiSignal = ParseInt(data.GetValueOrDefault("radio1")),
                ConnectedSsid = data.TryGetValue("ssid1", out var ssid1)
                    ? HttpUtility.UrlDecode(ssid1)
                    : null,
                SecurityType = data.GetValueOrDefault("sec_type"),
                HolidayMode = ParseInt(data.GetValueOrDefault("en_hol"))
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av Daikin basinformation");
            return null;
        }
    }

    /// <summary>
    /// Hämtar fjärrkommunikationskonfiguration.
    /// </summary>
    public async Task<DaikinRemoteMethod?> GetRemoteMethodAsync()
    {
        try
        {
            var response = await httpClient.GetStringAsync("/common/get_remote_method");
            var data = ParseResponse(response);

            return new DaikinRemoteMethod
            {
                Method = data.GetValueOrDefault("method"),
                NoticeIpInterval = ParseInt(data.GetValueOrDefault("notice_ip_int")),
                NoticeSyncInterval = ParseInt(data.GetValueOrDefault("notice_sync_int"))
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av remote method");
            return null;
        }
    }

    /// <summary>
    /// Hämtar enhetens klocka och timezone.
    /// </summary>
    public async Task<DaikinDateTime?> GetDateTimeAsync()
    {
        try
        {
            // ret=OK,sta=2,cur=2026/3/2 9:1:13,reg=eu,dst=1,zone=343
            var response = await httpClient.GetStringAsync("/common/get_datetime");
            var data = ParseResponse(response);

            return new DaikinDateTime
            {
                Status = ParseInt(data.GetValueOrDefault("sta")),
                Current = ParseDateTime(data.GetValueOrDefault("cur")),
                Region = data.GetValueOrDefault("reg"),
                DaylightSaving = ParseInt(data.GetValueOrDefault("dst")),
                TimeZone = ParseInt(data.GetValueOrDefault("zone"))
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av datum/tid");
            return null;
        }
    }

    /// <summary>
    /// Hämtar semesterläge-status.
    /// </summary>
    public async Task<DaikinHoliday?> GetHolidayAsync()
    {
        try
        {
            // ret=OK,en_hol=0
            var response = await httpClient.GetStringAsync("/common/get_holiday");
            var data = ParseResponse(response);

            return new DaikinHoliday
            {
                IsHoliday = ParseInt(data.GetValueOrDefault("en_hol")) != 0
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av semesterläge");
            return null;
        }
    }

    /// <summary>
    /// Hämtar WiFi-inställningar.
    /// </summary>
    public async Task<DaikinWifiSetting?> GetWifiSettingAsync()
    {
        try
        {
            // ret=OK,ssid=%43%61%72%74%6f%6f%6e,security=mixed,key=,link=1
            var response = await httpClient.GetStringAsync("/common/get_wifi_setting");
            var data = ParseResponse(response);

            return new DaikinWifiSetting
            {
                SSID = data.TryGetValue("ssid", out var name)
                    ? HttpUtility.UrlDecode(name)
                    : null,
                Security = data.GetValueOrDefault("security"),
                Link = ParseInt(data.GetValueOrDefault("link"))
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av WiFi-inställningar");
            return null;
        }
    }

    /// <summary>
    /// Ställer in semesterläge.
    /// </summary>
    public async Task<bool> SetHolidayAsync(bool enabled)
    {
        try
        {
            var url = $"/common/set_holiday?en_hol={(enabled ? 1 : 0)}";
            var response = await httpClient.GetStringAsync(url);
            var data = ParseResponse(response);
            return data.GetValueOrDefault("ret") == "OK";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid inställning av semesterläge");
            return false;
        }
    }

    /// <summary>
    /// Ställer in regionkod.
    /// </summary>
    public async Task<bool> SetRegionCodeAsync(string region)
    {
        try
        {
            var url = $"/common/set_regioncode?reg={region}";
            var response = await httpClient.GetStringAsync(url);
            var data = ParseResponse(response);
            return data.GetValueOrDefault("ret") == "OK";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid inställning av regionkod");
            return false;
        }
    }

    // ==================== AIRCON ENDPOINTS ====================

    /// <summary>
    /// Hämtar aktuella styrinställningar (läge, temperatur, fläkthastighet).
    /// </summary>
    public async Task<DaikinControlInfo?> GetControlInfoAsync()
    {
        try
        {
            // ret=OK,pow=1,mode=4,adv=,stemp=21.5,shum=0,dt1=25.0,dt2=M,dt3=25.0,dt4=21.5,dt5=21.5,dt7=25.0,dh1=AUTO,dh2=50,dh3=0,dh4=0,dh5=0,dh7=AUTO,dhh=50,b_mode=4,b_stemp=21.5,b_shum=0,alert=255,f_rate=A,b_f_rate=A,dfr1=A,dfr2=A,dfr3=A,dfr4=A,dfr5=A,dfr6=A,dfr7=A,dfrh=A,f_dir=0,b_f_dir=0,dfd1=0,dfd2=0,dfd3=0,dfd4=0,dfd5=0,dfd6=0,dfd7=0,dfdh=0,dmnd_run=0,en_demand=1
            var response = await httpClient.GetStringAsync("/aircon/get_control_info");
            var data = ParseResponse(response);

            return new DaikinControlInfo
            {
                Power = ParseInt(data.GetValueOrDefault("pow")) ?? 0,
                Mode = ParseInt(data.GetValueOrDefault("mode")) ?? 0,
                TargetTemperature = ParseDouble(data.GetValueOrDefault("stemp")),
                TargetHumidity = data.GetValueOrDefault("shum"),
                FanRate = data.GetValueOrDefault("f_rate"),
                FanDirection = ParseInt(data.GetValueOrDefault("f_dir")) ?? 0,
                Alert = ParseInt(data.GetValueOrDefault("alert"))
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av Daikin styrinformation");
            return null;
        }
    }

    /// <summary>
    /// Hämtar aktuella sensorvärden (temperaturer, fuktighet, kompressorfrekvens).
    /// </summary>
    /// <remarks>Fuktmätare inte tillgänglig i min enhet</remarks>
    public async Task<DaikinSensorInfo?> GetSensorInfoAsync()
    {
        try
        {
            // ret=OK,htemp=23.0,hhum=-,otemp=0.0,err=0,cmpfreq=22,mompow=5
            var response = await httpClient.GetStringAsync("/aircon/get_sensor_info");
            var data = ParseResponse(response);

            return new DaikinSensorInfo
            {
                IndoorTemperature = ParseDouble(data.GetValueOrDefault("htemp")),
                OutdoorTemperature = ParseDouble(data.GetValueOrDefault("otemp")),
                IndoorHumidity = ParseDouble(data.GetValueOrDefault("hhum")),
                CompressorFrequency = ParseInt(data.GetValueOrDefault("cmpfreq")),
                ErrorCode = ParseInt(data.GetValueOrDefault("err"))
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av Daikin sensordata");
            return null;
        }
    }

    /// <summary>
    /// Hämtar enhetens hårdvaru-kapaciteter och funktioner.
    /// </summary>
    public async Task<DaikinModelInfo?> GetModelInfoAsync()
    {
        try
        {
            // ret=OK,model=1481,type=N,pv=3.2,cpv=3,cpv_minor=20,mid=NA,humd=0,s_humd=0,acled=0,land=0,elec=1,temp=1,temp_rng=0,m_dtct=0,ac_dst=--,disp_dry=0,dmnd=1,en_scdltmr=1,en_frate=1,en_rtemp_a=0,en_spmode=3,en_ipw_sep=1,en_mompow=1,en_patrol=0,en_filter_sign=0,shtyp=0,en_cjlink=1,en_onofftmr=1,hmlmt_l=10.0,en_fdir=1,s_fdir=3
            var response = await httpClient.GetStringAsync("/aircon/get_model_info");
            var data = ParseResponse(response);

            return new DaikinModelInfo
            {
                Model = data.GetValueOrDefault("model"),
                Type = data.GetValueOrDefault("type"),
                ProtocolVersion = data.GetValueOrDefault("pv") ?? "",   // ex 3.2
                ControlProtocolVersion = ParseInt(data.GetValueOrDefault("cpv")),
                HasHumidity = ParseInt(data.GetValueOrDefault("humd")) ?? 0,
                HasHumiditySensor = ParseInt(data.GetValueOrDefault("s_humd")) ?? 0,
                HasTemperatureSensor = ParseInt(data.GetValueOrDefault("temp")) ?? 0,
                HasScheduleTimer = ParseInt(data.GetValueOrDefault("en_scdltmr")) ?? 0,
                HasFanRateControl = ParseInt(data.GetValueOrDefault("en_frate")) ?? 0,
                HasFanDirectionControl = ParseInt(data.GetValueOrDefault("en_fdir")) ?? 0,
                HasOnOffTimer = ParseInt(data.GetValueOrDefault("en_onofftmr")) ?? 0
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av modell-info");
            return null;
        }
    }

    /// <summary>
    /// Hämtar måltemperaturinställningar.
    /// </summary>
    public async Task<DaikinTarget?> GetTargetAsync()
    {
        try
        {
            var response = await httpClient.GetStringAsync("/aircon/get_target");
            var data = ParseResponse(response);

            return new DaikinTarget
            {
                Target = ParseInt(data.GetValueOrDefault("target"))
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av måltemperatur");
            return null;
        }
    }

    /// <summary>
    /// Hämtar energiprisinställningar.
    /// </summary>
    public async Task<DaikinPrice?> GetPriceAsync()
    {
        try
        {
            // ret=OK,price_int=27,price_dec=0
            var response = await httpClient.GetStringAsync("/aircon/get_price");
            var data = ParseResponse(response);

            return new DaikinPrice
            {
                PriceInt = ParseInt(data.GetValueOrDefault("price_int")),
                PriceDec = ParseInt(data.GetValueOrDefault("price_dec"))
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av prisinställningar");
            return null;
        }
    }

    /// <summary>
    /// Hämtar veckans energiförbrukning och daglig körtid.
    /// </summary>
    public async Task<DaikinWeekPower?> GetWeekPowerAsync()
    {
        try
        {
            // ret=OK,today_runtime=473,datas=500/400/500/0/500/400/200
            var response = await httpClient.GetStringAsync("/aircon/get_week_power");
            var data = ParseResponse(response);

            return new DaikinWeekPower
            {
                TodayRuntime = ParseInt(data.GetValueOrDefault("today_runtime")),
                WeeklyData = ParseIntArray(data.GetValueOrDefault("datas"))
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av veckans energi");
            return null;
        }
    }

    /// <summary>
    /// Hämtar veckans energiförbrukning uppdelad på värme och kylning.
    /// </summary>
    public async Task<DaikinWeekPowerEx?> GetWeekPowerExAsync()
    {
        try
        {
            var response = await httpClient.GetStringAsync("/aircon/get_week_power_ex");
            var data = ParseResponse(response);

            return new DaikinWeekPowerEx
            {
                StartDayOfWeek = ParseInt(data.GetValueOrDefault("s_dayw")),
                WeekHeat = ParseIntArray(data.GetValueOrDefault("week_heat")),
                WeekCool = ParseIntArray(data.GetValueOrDefault("week_cool"))
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av veckans energi (extended)");
            return null;
        }
    }

    /// <summary>
    /// Hämtar dagens och igårs timförbrukning uppdelad på värme och kylning.
    /// </summary>
    public async Task<DaikinDayPowerEx?> GetDayPowerExAsync()
    {
        try
        {
            // ret=OK,curr_day_heat=5/7/5/7/6/5/9/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0,prev_1day_heat=2/3/4/5/5/5/5/4/5/4/4/4/5/4/4/3/4/4/5/4/4/5/5/5,curr_day_cool=0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0,prev_1day_cool=0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0
            var response = await httpClient.GetStringAsync("/aircon/get_day_power_ex");
            var data = ParseResponse(response);

            return new DaikinDayPowerEx
            {
                CurrentDayHeat = ParseIntArray(data.GetValueOrDefault("curr_day_heat")),
                PreviousDayHeat = ParseIntArray(data.GetValueOrDefault("prev_1day_heat")),
                CurrentDayCool = ParseIntArray(data.GetValueOrDefault("curr_day_cool")),
                PreviousDayCool = ParseIntArray(data.GetValueOrDefault("prev_1day_cool"))
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av dagens energi (extended)");
            return null;
        }
    }

    /// <summary>
    /// Hämtar årets energiförbrukning.
    /// </summary>
    public async Task<DaikinYearPower?> GetYearPowerAsync()
    {
        try
        {
            // ret=OK,previous_year=14/12/12/10/8/3/2/2/4/9/10/13,this_year=13/13/0
            var response = await httpClient.GetStringAsync("/aircon/get_year_power");
            var data = ParseResponse(response);

            return new DaikinYearPower
            {
                PreviousYear = ParseIntArray(data.GetValueOrDefault("previous_year")),
                ThisYear = ParseIntArray(data.GetValueOrDefault("this_year"))
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av årets energi");
            return null;
        }
    }

    /// <summary>
    /// Hämtar årets energiförbrukning uppdelad på värme och kylning.
    /// </summary>
    public async Task<DaikinYearPowerEx?> GetYearPowerExAsync()
    {
        try
        {
            // ret=OK,curr_year_heat=6422/6173/154/0/0/0/0/0/0/0/0/0,prev_year_heat=5171/4471/3535/2625/1848/568/118/299/793/2228/3270/4060,curr_year_cool=0/0/0/0/0/0/0/0/0/0/0/0,prev_year_cool=0/0/0/0/0/0/222/3/0/0/0/0
            var response = await httpClient.GetStringAsync("/aircon/get_year_power_ex");
            var data = ParseResponse(response);

            return new DaikinYearPowerEx
            {
                PreviousYearHeat = ParseIntArray(data.GetValueOrDefault("prev_year_heat")),
                PreviousYearCool = ParseIntArray(data.GetValueOrDefault("prev_year_cool")),
                CurrentYearHeat = ParseIntArray(data.GetValueOrDefault("curr_year_heat")),
                CurrentYearCool = ParseIntArray(data.GetValueOrDefault("curr_year_cool"))
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av årets energi (extended)");
            return null;
        }
    }

    /// <summary>
    /// Hämtar månatlig energiförbrukning uppdelad på värme och kylning.
    /// </summary>
    public async Task<DaikinMonthPowerEx?> GetMonthPowerExAsync()
    {
        try
        {
            // ret=OK,curr_month_heat=102/52/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0,prev_month_heat=199/222/278/279/265/272/220/226/271/237/277/260/286/288/242/240/238/269/206/227/179/196/174/147/171/128/77/99,curr_month_cool=0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0,prev_month_cool=0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0
            var response = await httpClient.GetStringAsync("/aircon/get_month_power_ex");
            var data = ParseResponse(response);

            return new DaikinMonthPowerEx
            {
                CurrentMonthHeat = ParseIntArray(data.GetValueOrDefault("curr_month_heat")),
                PreviousMonthHeat = ParseIntArray(data.GetValueOrDefault("prev_month_heat")),
                CurrentMonthCool = ParseIntArray(data.GetValueOrDefault("curr_month_cool")),
                PreviousMonthCool = ParseIntArray(data.GetValueOrDefault("prev_month_cool"))
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av månaders energi");
            return null;
        }
    }

    /// <summary>
    /// Hämtar schemainställningar.
    /// </summary>
    public async Task<DaikinScheduleTimerInfo?> GetScheduleTimerInfoAsync()
    {
        try
        {
            // ret=OK,format=v1,f_detail=total#18;_en#1;_pow#1;_mode#1;_temp#4;_time#4;_vol#1;_dir#1;_humi#3;_spmd#2,scdl_num=3,scdl_per_day=6,en_scdltimer=0,active_no=1,scdl1_name=,scdl2_name=,scdl3_name=
            var response = await httpClient.GetStringAsync("/aircon/get_scdltimer_info");
            var data = ParseResponse(response);

            return new DaikinScheduleTimerInfo
            {
                Format = data.GetValueOrDefault("format"),
                ScheduleNumber = ParseInt(data.GetValueOrDefault("scdl_num")),
                SchedulePerDay = ParseInt(data.GetValueOrDefault("scdl_per_day")),
                Enabled = ParseInt(data.GetValueOrDefault("en_scdltimer")),
                ActiveNumber = ParseInt(data.GetValueOrDefault("active_no"))
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av schema-info");
            return null;
        }
    }

    /// <summary>
    /// Ställer in styrinställningar på Daikin-enheten.
    /// Kräver att alla parametrar skickas; hämta först aktuella värden med <see cref="GetControlInfoAsync"/>
    /// och ändra bara de värden som ska ändras.
    /// </summary>
    public async Task<bool> SetControlInfoAsync(DaikinControlInfo controlInfo)
    {
        try
        {
            var stemp = controlInfo.TargetTemperature?.ToString("F1",
                System.Globalization.CultureInfo.InvariantCulture) ?? "M";

            var url = $"/aircon/set_control_info" +
                      $"?pow={controlInfo.Power}" +
                      $"&mode={controlInfo.Mode}" +
                      $"&stemp={stemp}" +
                      $"&shum={controlInfo.TargetHumidity ?? "0"}" +
                      $"&f_rate={controlInfo.FanRate ?? "A"}" +
                      $"&f_dir={controlInfo.FanDirection}";

            // ret=OK,adv=
            var response = await httpClient.GetStringAsync(url);
            var data = ParseResponse(response);

            var success = data.GetValueOrDefault("ret") == "OK";
            if (!success)
            {
                logger.LogWarning("Daikin set_control_info returnerade: {Response}", response);
            }

            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid inställning av Daikin styrinformation");
            return false;
        }
    }

    /// <summary>
    /// Ställer in målinställning.
    /// </summary>
    public async Task<bool> SetTargetAsync(int target)
    {
        try
        {
            var url = $"/aircon/set_target?target={target}";
            var response = await httpClient.GetStringAsync(url);
            var data = ParseResponse(response);
            return data.GetValueOrDefault("ret") == "OK";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid inställning av måltemperatur");
            return false;
        }
    }

    // ==================== CONVENIENCE METHODS ====================

    /// <summary>
    /// Slår på Daikin-enheten utan att ändra övriga inställningar.
    /// </summary>
    public async Task<bool> TurnOnAsync()
    {
        var controlInfo = await GetControlInfoAsync();
        if (controlInfo is null) return false;

        controlInfo.Power = 1;
        return await SetControlInfoAsync(controlInfo);
    }

    /// <summary>
    /// Stänger av Daikin-enheten utan att ändra övriga inställningar.
    /// </summary>
    public async Task<bool> TurnOffAsync()
    {
        var controlInfo = await GetControlInfoAsync();
        if (controlInfo is null) return false;

        controlInfo.Power = 0;
        return await SetControlInfoAsync(controlInfo);
    }

    /// <summary>
    /// Ställer in börvärdestemperaturen och värme/kyla
    /// </summary>
    public async Task<bool> SetTargetTemperatureAsync(double temperature, bool heat)
    {
        var controlInfo = await GetControlInfoAsync();
        if (controlInfo is null) return false;

        controlInfo.TargetTemperature = temperature;
        controlInfo.Mode = heat ? 4 : 3;
        return await SetControlInfoAsync(controlInfo);
    }

    /// <summary>
    /// Ställer in fläkthastighet utan att ändra övriga inställningar.
    /// </summary>
    public async Task<bool> SetFanRateAsync(string fanRate)
    {
        var controlInfo = await GetControlInfoAsync();
        if (controlInfo is null) return false;

        controlInfo.FanRate = fanRate;
        return await SetControlInfoAsync(controlInfo);
    }

    /// <summary>
    /// Ställer in fläktriktning utan att ändra övriga inställningar.
    /// </summary>
    public async Task<bool> SetFanDirectionAsync(int fanDirection)
    {
        var controlInfo = await GetControlInfoAsync();
        if (controlInfo is null) return false;

        controlInfo.FanDirection = fanDirection;
        return await SetControlInfoAsync(controlInfo);
    }

    /// <summary>
    /// Ställer in läge utan att ändra övriga inställningar.
    /// </summary>
    public async Task<bool> SetModeAsync(int mode)
    {
        var controlInfo = await GetControlInfoAsync();
        if (controlInfo is null) return false;

        controlInfo.Mode = mode;
        return await SetControlInfoAsync(controlInfo);
    }

    // ==================== HELPER METHODS ====================

    /// <summary>
    /// Parsar Daikins nyckel=värde-responsformat till en dictionary.
    /// </summary>
    private static Dictionary<string, string> ParseResponse(string response)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in response.Split(','))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                result[parts[0].Trim()] = parts[1].Trim();
            }
        }

        return result;
    }

    /// <summary>
    /// Parsar en sträng till double, hanterar Daikins specialvärden.
    /// </summary>
    private static double? ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "-" || value == "M")
            return null;

        return double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    /// <summary>
    /// Parsar en sträng till int, hanterar saknade värden.
    /// </summary>
    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "-")
            return null;

        return int.TryParse(value, out var result) ? result : null;
    }

    /// <summary>
    /// Parsar en '/'-separerad sträng till en int-array.
    /// Daikin returnerar t.ex. "59/128/171/147" där varje värde är i 100W-enheter.
    /// </summary>
    private static int[] ParseIntArray(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        return value.Split('/')
            .Select(s => int.TryParse(s.Trim(), out var v) ? v : 0)
            .ToArray();
    }

    /// <summary>
    /// Parsar Daikins datetime-format (t.ex. "2026/3/2 9:1:13") till DateTime.
    /// </summary>
    private static DateTime? ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTime.TryParseExact(value, "yyyy/M/d H:m:ss",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var result))
        {
            return result;
        }

        return null;
    }
}

using System.Net.Http.Json;

using ChargeMaster.Models;

using Serilog;

namespace ChargeMaster.Services;

/// <summary>
/// Access to Garo wallbox through http interface
/// </summary>
/// <param name="httpClient"></param>
public class WallboxService(HttpClient httpClient) : IWallboxService
{
    public long accessCounter
    {
        get => ++field;
        set => field = value;
    } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public async Task<WallboxStatus?> GetStatusAsync()
    {
        try
        {
            var url = $"servlet/rest/chargebox/status?_={accessCounter}";
            return await httpClient.GetFromJsonAsync<WallboxStatus>(url);
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "Error fetching wallbox status");
            return null;
        }
    }

    public async Task<DateTime?> GetTimeAsync()
    {
        var status = await GetStatusAsync();
        if (status?.ChargeboxTime != null && TimeOnly.TryParse(status.ChargeboxTime, out var time))
        {
            return DateTime.Today.Add(time.ToTimeSpan());
        }
        return null;
    }

    public async Task<bool> SetTimeAsync(DateTime dateTime)
    {
        try
        {
            var payload = new
            {
                offset = -60,
                tzName = "Europe/Stockholm",
                datetimeUTC = dateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            };

            var response = await httpClient.PostAsJsonAsync("/servlet/rest/chargebox/time", payload);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<bool> SetModeAsync(WallboxMode mode)
    {
        try
        {
            string modeString = mode switch
            {
                WallboxMode.Available => "ALWAYS_ON",
                WallboxMode.NotAvailable => "ALWAYS_OFF",
                WallboxMode.TimerControlled => "SCHEMA",
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };

            var payload = new { mode = modeString };
            // The wallbox usually expects the payload at /servlet/rest/chargebox/mode
            var response = await httpClient.PostAsync($"/servlet/rest/chargebox/mode/{modeString}",null);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<WallboxMeterInfo?> GetMeterInfoAsync()
    {
        try
        {
            var url = $"servlet/rest/chargebox/meterinfo/EXTERNAL?_={accessCounter}";
            return await httpClient.GetFromJsonAsync<WallboxMeterInfo>(url);
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "Error fetching wallbox meter info");
            return null;
        }
    }
}
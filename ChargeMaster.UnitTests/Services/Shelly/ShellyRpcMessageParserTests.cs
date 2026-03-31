using ChargeMaster.Services.Shelly;

namespace ChargeMaster.UnitTests.Services.Shelly;

public class ShellyRpcMessageParserTests
{
    private const string SampleJson = @"{""src"":""shellyhtg3-d885ac155640"",""dst"":""shelly-arbetsrum/events"",""method"":""NotifyFullStatus"",""params"":{""ts"":1772473140.89,""ble"":{},""cloud"":{""connected"":false},""devicepower:0"":{""id"":0,""battery"":{""V"":6.33,""percent"":100},""external"":{""present"":false}},""ht_ui"":{},""humidity:0"":{""id"":0,""rh"":34.5},""mqtt"":{""connected"":true},""sys"":{""mac"":""D885AC155640"",""restart_required"":false,""time"":null,""unixtime"":null,""last_sync_ts"":null,""uptime"":0,""ram_size"":262124,""ram_free"":161428,""ram_min_free"":158356,""fs_size"":1048576,""fs_free"":712704,""cfg_rev"":18,""kvs_rev"":0,""webhook_rev"":0,""available_updates"":{},""wakeup_reason"":{""boot"":""deepsleep_wake"",""cause"":""status_update""},""wakeup_period"":7200,""reset_reason"":8,""utc_offset"":3600},""temperature:0"":{""id"":0,""tC"":20.7,""tF"":69.3},""wifi"":{""sta_ip"":""192.168.1.245"",""status"":""got ip"",""ssid"":""Cartoon"",""bssid"":""d8:50:e6:44:ca:00"",""rssi"":-75,""sta_ip6"":null},""ws"":{""connected"":false}}}";

    [Fact]
    public void Parse_ValidJson_ReturnsShellyRpcMessage()
    {
        var result = ShellyRpcMessageParser.Parse(SampleJson);

        Assert.NotNull(result);
        Assert.Equal("shellyhtg3-d885ac155640", result.src);
        Assert.Equal("shelly-arbetsrum/events", result.dst);
        Assert.Equal("NotifyFullStatus", result.method);
    }

    [Fact]
    public void Parse_ValidJson_ParsesParamsCorrectly()
    {
        string SampleJson
            = "{\"src\":\"shellyhtg3-d885ac14d3ec\",\"dst\":\"shelly-sovrum/events\",\"method\":\"NotifyFullStatus\",\"params\":{\"ts\":1774791480.92,\"ble\":{},\"cloud\":{\"connected\":true},\"devicepower:0\":{\"id\": 0,\"battery\":{\"V\":0.00, \"percent\":0},\"external\":{\"present\":true}},\"ht_ui\":{},\"humidity:0\":{\"id\": 0,\"rh\":35.2},\"mqtt\":{\"connected\":true},\"sys\":{\"mac\":\"D885AC14D3EC\",\"restart_required\":false,\"time\":\"15:38\",\"unixtime\":1774791480,\"last_sync_ts\":1774791480,\"uptime\":1,\"ram_size\":262136,\"ram_free\":159264,\"ram_min_free\":152536,\"fs_size\":1048576,\"fs_free\":712704,\"cfg_rev\":18,\"kvs_rev\":0,\"webhook_rev\":0,\"available_updates\":{},\"wakeup_reason\":{\"boot\":\"deepsleep_wake\",\"cause\":\"status_update\"},\"wakeup_period\":600,\"reset_reason\":8,\"utc_offset\":7200},\"temperature:0\":{\"id\": 0,\"tC\":23.5, \"tF\":74.2},\"wifi\":{\"sta_ip\":\"192.168.1.128\",\"status\":\"got ip\",\"ssid\":\"Cartoon\",\"bssid\":\"d8:50:e6:44:ca:00\",\"rssi\":-67,\"sta_ip6\":null},\"ws\":{\"connected\":false}}}\r\n";

        var result = ShellyRpcMessageParser.Parse(SampleJson);

        Assert.NotNull(result);
        var @params = result.@params;
        Assert.Equal(1774791480.92, @params.ts);
        Assert.NotNull(@params.cloud);
        Assert.True(@params.cloud.connected);
    }


    [Fact]
    public void Parse_ValidJson_ParsesParamsCorrectly2()
    {
        string SampleJson
            = " {\"src\":\"shellyhtg3-d885ac14d3ec\",\"dst\":\"shelly-sovrum/events\",\"method\":\"NotifyFullStatus\",\"params\":{\"ts\":1774792561.71,\"ble\":{},\"cloud\":{\"connected\":true},\"devicepower:0\":{\"id\": 0,\"battery\":{\"V\":0.00, \"percent\":0},\"external\":{\"present\":true}},\"ht_ui\":{},\"humidity:0\":{\"id\": 0,\"rh\":35.1},\"mqtt\":{\"connected\":true},\"sys\":{\"mac\":\"D885AC14D3EC\",\"restart_required\":false,\"time\":\"15:56\",\"unixtime\":1774792561,\"last_sync_ts\":1774792561,\"uptime\":1,\"ram_size\":262136,\"ram_free\":159364,\"ram_min_free\":150900,\"fs_size\":1048576,\"fs_free\":712704,\"cfg_rev\":18,\"kvs_rev\":0,\"webhook_rev\":0,\"available_updates\":{},\"wakeup_reason\":{\"boot\":\"deepsleep_wake\",\"cause\":\"status_update\"},\"wakeup_period\":600,\"reset_reason\":8,\"utc_offset\":7200},\"temperature:0\":{\"id\": 0,\"tC\":23.5, \"tF\":74.3},\"wifi\":{\"sta_ip\":\"192.168.1.128\",\"status\":\"got ip\",\"ssid\":\"Cartoon\",\"bssid\":\"d8:50:e6:44:ca:00\",\"rssi\":-68,\"sta_ip6\":null},\"ws\":{\"connected\":false}}}";

        var result = ShellyRpcMessageParser.Parse(SampleJson);

        Assert.NotNull(result);
        var @params = result.@params;
        Assert.Equal(1774792561.71, @params.ts);
        Assert.NotNull(@params.cloud);
        Assert.True(@params.cloud.connected);
    }

    [Fact]
    public void Parse_ValidJson_ParsesTemperatureCorrectly()
    {
        var result = ShellyRpcMessageParser.Parse(SampleJson);

        Assert.NotNull(result);
        var temperature = result.@params.Temperature0;
        Assert.NotNull(temperature);
        Assert.Equal(0, temperature.id);
        Assert.Equal(20.7, temperature.TemperatureCelsius);
        Assert.Equal(69.3, temperature.TemperatureFahrenheit);
    }

    [Fact]
    public void Parse_ValidJson_ParsesHumidityCorrectly()
    {
        var result = ShellyRpcMessageParser.Parse(SampleJson);

        Assert.NotNull(result);
        var humidity = result.@params.Humidity0;
        Assert.NotNull(humidity);
        Assert.Equal(0, humidity.id);
        Assert.Equal(34.5, humidity.RelativeHumidity);
    }

    [Fact]
    public void Parse_ValidJson_ParsesDevicePowerCorrectly()
    {
        var result = ShellyRpcMessageParser.Parse(SampleJson);

        Assert.NotNull(result);
        var devicePower = result.@params.DevicePower0;
        Assert.NotNull(devicePower);
        Assert.Equal(0, devicePower.id);
        Assert.Equal(6.33, devicePower.battery.V);
        Assert.Equal(100, devicePower.battery.percent);
    }

    [Fact]
    public void Parse_ValidJson_ParsesWifiCorrectly()
    {
        var result = ShellyRpcMessageParser.Parse(SampleJson);

        Assert.NotNull(result);
        var wifi = result.@params.wifi;
        Assert.Equal("192.168.1.245", wifi.StationIp);
        Assert.Equal("got ip", wifi.status);
        Assert.Equal("Cartoon", wifi.ssid);
        Assert.Equal(-75, wifi.rssi);
    }

    [Fact]
    public void Parse_ValidJson_ParsesMqttCorrectly()
    {
        var result = ShellyRpcMessageParser.Parse(SampleJson);

        Assert.NotNull(result);
        var mqtt = result.@params.mqtt;
        Assert.True(mqtt.connected);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsNull()
    {
        var result = ShellyRpcMessageParser.Parse("not valid json");

        Assert.Null(result);
    }

    [Fact]
    public void TryParse_ValidJson_ReturnsTrue()
    {
        var success = ShellyRpcMessageParser.TryParse(SampleJson, out var message);

        Assert.True(success);
        Assert.NotNull(message);
    }

    [Fact]
    public void TryParse_InvalidJson_ReturnsFalse()
    {
        var success = ShellyRpcMessageParser.TryParse("invalid json", out var message);

        Assert.False(success);
        Assert.Null(message);
    }
}

using System.Text;

using ChargeMaster.Services.Shelly;

using Microsoft.Extensions.Logging.Abstractions;

using MQTTnet;
using MQTTnet.Packets;

using Xunit;

namespace ChargeMaster.xUnit.Services.Shelly;

public class ShellyMqttServiceTests
{
    // Äkta JSON-payload från en Shelly HT G3-enhet placerad i "arbetsrum"
    private const string HallPayload
        = "{\"id\": 0,\"tC\":22.4, \"tF\":72.3}";

    private static ShellyMqttService CreateService() =>
        new(null!, NullLogger<ShellyMqttService>.Instance);

    private static MqttApplicationMessageReceivedEventArgs CreateEventArgs(string topic, string jsonPayload)
    {
        var applicationMessage = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(Encoding.UTF8.GetBytes(jsonPayload))
            .Build();
        return new MqttApplicationMessageReceivedEventArgs(
            "test-client",
            applicationMessage,
            new MqttPublishPacket { Topic = topic },
            null);
    }

    [Fact]
    public async Task OnApplicationMessageReceivedAsync_ValidPayload_UpdatesTemperature()
    {
        var service = CreateService();

        await service.OnApplicationMessageReceivedAsync(CreateEventArgs("shelly-hall/status/temperature:0", HallPayload));

        Assert.Equal(22.4, service.Temperatures["hall"]);
    }

    [Fact(Skip="Exploratory test using logged MQTT messages")]
    public async Task OnApplicationMessageReceivedAsync_logg()
    {
        var loggFilePath = "G:\\dev\\ChargeMaster\\ChargeMaster.xUnit\\Services\\Shelly\\logg";
        if (!File.Exists(loggFilePath))
            throw new FileNotFoundException($"Test data file not found: {loggFilePath}");

        var service = CreateService();
        var lines = File.ReadAllLines(loggFilePath);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Format: "topic payload"
            var parts = line.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                continue;

            var topic = parts[0];
            var payload = parts[1];

            await service.OnApplicationMessageReceivedAsync(CreateEventArgs(topic, payload));
        }
    }
}

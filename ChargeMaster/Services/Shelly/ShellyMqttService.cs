using System.Buffers;
using MQTTnet;

namespace ChargeMaster.Services.Shelly;

/// <summary>
/// Tjänst för att läsa information från Shelly-enheter via MQTT.
/// Använder MQTTnet v5 för kommunikation.
/// </summary>
public class ShellyMqttService(ILogger<ShellyMqttService> logger) : IAsyncDisposable
{
    /// <summary>
    /// Event som triggas när ett meddelande mottas från MQTT-servern.
    /// </summary>
    public event EventHandler<ShellyMqttMessageEventArgs>? MessageReceived;

    /// <summary>
    /// Event som triggas när anslutningen ändras.
    /// </summary>
    public event EventHandler<ShellyMqttConnectionStateChangedEventArgs>? ConnectionStateChanged;

    private IMqttClient? _mqttClient;

    /// <summary>
    /// Returnerar sant om tjänsten är ansluten till MQTT-servern.
    /// </summary>
    public bool IsConnected => _mqttClient?.IsConnected ?? false;

    /// <summary>
    /// Ansluter till en MQTT-server utan autentisering eller TLS.
    /// </summary>
    /// <param name="brokerAddress">IP-adress eller hostname för MQTT-brokern</param>
    /// <param name="brokerPort">Port för MQTT-brokern (standard: 1883)</param>
    /// <param name="clientId">Klient-ID för MQTT-anslutningen</param>
    public async Task ConnectAsync(string brokerAddress, int brokerPort = 1883, string? clientId = null)
    {
        clientId ??= $"chargemaster-{Guid.NewGuid().ToString("N")[..8]}";

        var factory = new MqttClientFactory();
        _mqttClient = factory.CreateMqttClient();

        _mqttClient.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;
        _mqttClient.ConnectedAsync += OnConnectedAsync;
        _mqttClient.DisconnectedAsync += OnDisconnectedAsync;

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(brokerAddress, brokerPort)
            .WithClientId(clientId)
            .WithCleanSession(false)
            .Build();

        await _mqttClient.ConnectAsync(options, CancellationToken.None);
        logger.LogInformation("Ansluten till MQTT-server på {Address}:{Port} med klient-ID {ClientId}",
            brokerAddress, brokerPort, clientId);
    }

    /// <summary>
    /// Kopplar ifrån MQTT-servern.
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_mqttClient is null)
            return;

        if (_mqttClient.IsConnected)
        {
            await _mqttClient.DisconnectAsync(MqttClientDisconnectOptionsReason.NormalDisconnection);
            logger.LogInformation("Kopplad ifrån MQTT-server");
        }
    }

    /// <summary>
    /// Prenumererar på ett MQTT-ämne.
    /// </summary>
    /// <param name="topic">MQTT-ämnet att prenumerera på (t.ex. "shellies/#")</param>
    public async Task SubscribeAsync(string topic)
    {
        if (_mqttClient is null || !_mqttClient.IsConnected)
            throw new InvalidOperationException("Inte ansluten till MQTT-server");

        await _mqttClient.SubscribeAsync(topic, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce);
        logger.LogInformation("Prenumererad på MQTT-ämne: {Topic}", topic);
    }

    /// <summary>
    /// Prenumererar på flera MQTT-ämnen.
    /// </summary>
    /// <param name="topics">Lista av MQTT-ämnen</param>
    public async Task SubscribeAsync(params string[] topics)
    {
        foreach (var topic in topics)
        {
            await SubscribeAsync(topic);
        }
    }

    private Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = System.Text.Encoding.UTF8.GetString(e.ApplicationMessage.Payload.ToArray());

        logger.LogDebug("MQTT-meddelande mottaget från {Topic}: {Payload}", topic, payload);

        var mess = ShellyRpcMessageParser.Parse(payload);

        MessageReceived?.Invoke(this, new ShellyMqttMessageEventArgs
        {
            Topic = topic,
            Payload = payload,
            Timestamp = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }

    private Task OnConnectedAsync(MqttClientConnectedEventArgs e)
    {
        logger.LogInformation("MQTT-anslutning etablerad");

        ConnectionStateChanged?.Invoke(this, new ShellyMqttConnectionStateChangedEventArgs
        {
            IsConnected = true,
            Timestamp = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        var reason = e.Reason.ToString();
        logger.LogWarning("MQTT-anslutning förlorad. Anledning: {Reason}", reason);

        ConnectionStateChanged?.Invoke(this, new ShellyMqttConnectionStateChangedEventArgs
        {
            IsConnected = false,
            Timestamp = DateTime.UtcNow,
            Reason = reason
        });

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_mqttClient is not null)
        {
            _mqttClient.ApplicationMessageReceivedAsync -= OnApplicationMessageReceivedAsync;
            _mqttClient.ConnectedAsync -= OnConnectedAsync;
            _mqttClient.DisconnectedAsync -= OnDisconnectedAsync;

            if (_mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync(MqttClientDisconnectOptionsReason.NormalDisconnection);
            }

            _mqttClient.Dispose();
            _mqttClient = null;
        }
    }
}

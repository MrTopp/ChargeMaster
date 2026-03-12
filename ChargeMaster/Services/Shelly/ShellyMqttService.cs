using System.Buffers;

using Microsoft.EntityFrameworkCore;

using MQTTnet;

namespace ChargeMaster.Services.Shelly;

/// <summary>
/// Tjänst för att läsa information från Shelly-enheter via MQTT.
/// Använder MQTTnet v5 för kommunikation.
/// </summary>
public class ShellyMqttService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ShellyMqttService> logger) : IAsyncDisposable
{
    /// <summary>
    /// Aktuella uppdaterade temperaturer
    /// </summary>
    public readonly Dictionary<string, double> Temperatures = new()
    {
        // Defaultvärden, kommer att uppdateras vid start från databasen
        {"arbetsrum", 21.5},
        {"hall", 21.5},
        {"sovrum", 21.5}
    };

    /// <summary>
    /// Medelvärde av temperaturerna i arbetsrum och sovrum.
    /// </summary>
    public double GetAverage()
    {
        var temp1 = GetArbetsrumTemperature();
        var temp3 = GetSovrumTemperature();
        return (temp1 + temp3) / 2.0;
    }

    public double GetHallTemperature()
    {
        return Temperatures.TryGetValue("hall", out var temp) ? temp : 21.5;
    }

    public double GetArbetsrumTemperature()
    {
        return Temperatures.TryGetValue("arbetsrum", out var temp) ? temp : 21.5;
    }

    public double GetSovrumTemperature()
    {
        return Temperatures.TryGetValue("sovrum", out var temp) ? temp : 21.5;
    }

    /// <summary>
    /// Event som skickas när en temperaturmätning uppdateras från en Shelly-enhet.
    /// </summary>
    public event EventHandler<ShellyTemperatureChangedEventArgs>? TemperatureChanged
    {
        add
        {
            var hadSubscribers = _temperatureChangedHandlers != null;
            _temperatureChangedHandlers += value;

            // Trigga SubscriberConnected när första prenumeranten ansluter
            if (!hadSubscribers && value != null)
            {
                SubscriberConnected?.Invoke(this, EventArgs.Empty);
            }
            _temperatureChangedHandlers?.Invoke(this, new ShellyTemperatureChangedEventArgs("arbetsrum", Temperatures["arbetsrum"]));
            _temperatureChangedHandlers?.Invoke(this, new ShellyTemperatureChangedEventArgs("hall", Temperatures["hall"]));
            _temperatureChangedHandlers?.Invoke(this, new ShellyTemperatureChangedEventArgs("sovrum", Temperatures["sovrum"]));

        }
        remove
        {
            _temperatureChangedHandlers -= value;

            // Trigga SubscriberDisconnected när sista prenumeranten kopplas från
            if (_temperatureChangedHandlers == null)
            {
                SubscriberDisconnected?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    // ----- private parts -----


    private IMqttClient? _mqttClient;

    const string BrokerAddress = "192.168.1.10";
    const int BrokerPort = 1883;
    const string ClientId = "chargemaster-shelly-mqtt";

    private readonly string[] _shellysTopics =
    [
        "shelly-arbetsrum/#",
        "shelly-hall/#",
        "shelly-sovrum/#"
    ];

    /// <summary>
    /// Returnerar sant om tjänsten är ansluten till MQTT-servern.
    /// </summary>
    internal bool IsConnected => _mqttClient?.IsConnected ?? false;

    

    public ShellyMqttService()
        : this(null, null)
    {

    }

    private EventHandler<ShellyTemperatureChangedEventArgs>? _temperatureChangedHandlers;

    /// <summary>
    /// Event som skickas när en klient prenumererar på TemperatureChanged-eventet.
    /// </summary>
    public event EventHandler? SubscriberConnected;

    /// <summary>
    /// Event som skickas när den sista klienten avprenumererar från TemperatureChanged-eventet.
    /// </summary>
    public event EventHandler? SubscriberDisconnected;

    /// <summary>
    /// Event som skickas när MQTT-anslutningen etableras eller försvinner.
    /// </summary>
    public event EventHandler<ShellyConnectionChangedEventArgs>? ConnectionChanged;

    public async Task SetupAsync()
    {
        await InitiateTemperatures();

        await ConnectAsync(BrokerAddress, BrokerPort, ClientId);

        await SubscribeAsync(_shellysTopics);

        // Här kan du lägga till eventuell setup-logik om det behövs
        await Task.CompletedTask;
    }

    /// <summary>
    /// Hämtar aktuella värden för temeratur från databasen.
    /// </summary>
    /// <returns></returns>
    private async Task InitiateTemperatures()
    {
        try
        {
            // Använd IServiceScopeFactory för att skapa ett scope för databasanrop
            // Detta är nödvändigt eftersom ShellyMqttService är Singleton men DbContext är Scoped
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ChargeMaster.Data.ApplicationDbContext>();

            // Hämta senaste temperaturvärde för varje enhet från databasen
            var latestTemperatures = await dbContext.ShellyTemperatures
                .GroupBy(t => t.DeviceId)
                .Select(g => g.OrderByDescending(t => t.Timestamp).FirstOrDefault())
                .ToListAsync();

            // Lägg till värdena i Temperatures-dictionary
            foreach (var temp in latestTemperatures)
            {
                if (temp != null)
                {
                    Temperatures[temp.DeviceId] = temp.TemperatureCelsius;
                    logger.LogDebug("Laddade senaste temperatur för {DeviceId}: {Temperature} °C från databasen",
                        temp.DeviceId, temp.TemperatureCelsius);
                }
            }

            // Sätt defaultvärden för enheter som inte finns i databasen
            var enhetIds = new[] { "arbetsrum", "hall", "sovrum" };
            foreach (var enhetId in enhetIds)
            {
                Temperatures.TryAdd(enhetId, 20.0);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fel vid hämtning av temperaturer från databasen, använder defaultvärden");
        }
    }

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
            logger.LogDebug("Kopplad ifrån MQTT-server");
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
        logger.LogDebug("Prenumererad på MQTT-ämne: {Topic}", topic);
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
        if (mess == null)
            return Task.CompletedTask;

        HandleMessage(mess);

        return Task.CompletedTask;
    }

    private void HandleMessage(ShellyRpcMessage message)
    {
        double? temp = message?.@params?.Temperature0?.TemperatureCelsius;
        string? src = message?.dst?.Split('/')?.FirstOrDefault()?.Replace("shelly-", "");
        if (temp.HasValue && src != null)
        {
            if (!Temperatures.ContainsKey(src) || Math.Abs(Temperatures[src] - temp.Value) > 0.1)
            {
                logger.LogInformation("Temperatur från {Src}: {Temp} °C", src, temp.Value);
                Temperatures[src] = temp.Value;
                _temperatureChangedHandlers?.Invoke(this, new ShellyTemperatureChangedEventArgs(src, temp.Value));
            }
        }
    }


    private Task OnConnectedAsync(MqttClientConnectedEventArgs e)
    {
        logger.LogDebug("MQTT-anslutning etablerad");

        // Trigga ConnectionChanged-eventet
        ConnectionChanged?.Invoke(this, new ShellyConnectionChangedEventArgs(true));

        return Task.CompletedTask;
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        var reason = e.Reason.ToString();
        logger.LogWarning("MQTT-anslutning förlorad. Anledning: {Reason}", reason);

        // Trigga ConnectionChanged-eventet
        ConnectionChanged?.Invoke(this, new ShellyConnectionChangedEventArgs(false));

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

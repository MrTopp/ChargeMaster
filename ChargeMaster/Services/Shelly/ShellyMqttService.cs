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
    ILogger<ShellyMqttService> logger,
    IMqttClient? mqttClient = null) : IAsyncDisposable
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
        lock (_temperaturesLock)
        {
            var temp1 = GetArbetsrumTemperatureUnsafe();
            var temp3 = GetSovrumTemperatureUnsafe();
            return (temp1 + temp3) / 2.0;
        }
    }

    /// <summary>
    /// Returnerar aktuell temperatur i hallen.
    /// </summary>
    public double GetHallTemperature()
    {
        lock (_temperaturesLock)
            return GetHallTemperatureUnsafe();
    }

    /// <summary>
    /// Returnerar aktuell temperatur i arbetsrummet.
    /// </summary>
    public double GetArbetsrumTemperature()
    {
        lock (_temperaturesLock)
            return GetArbetsrumTemperatureUnsafe();
    }

    /// <summary>
    /// Returnerar aktuell temperatur i sovrummet.
    /// </summary>
    public double GetSovrumTemperature()
    {
        lock (_temperaturesLock)
            return GetSovrumTemperatureUnsafe();
    }

    private double GetHallTemperatureUnsafe() =>
        Temperatures.TryGetValue("hall", out var temp) ? temp : 21.5;

    private double GetArbetsrumTemperatureUnsafe() =>
        Temperatures.TryGetValue("arbetsrum", out var temp) ? temp : 21.5;

    private double GetSovrumTemperatureUnsafe() =>
        Temperatures.TryGetValue("sovrum", out var temp) ? temp : 21.5;

    /// <summary>
    /// Event som skickas när en temperaturmätning uppdateras från en Shelly-enhet.
    /// </summary>
    public event EventHandler<ShellyTemperatureChangedEventArgs>? TemperatureChanged
    {
        add
        {
            bool isFirstSubscriber;
            double arbetsrum, hall, sovrum;

            lock (_eventLock)
            {
                isFirstSubscriber = _temperatureChangedHandlers == null;
                _temperatureChangedHandlers += value;
            }

            // Trigga SubscriberConnected när första prenumeranten ansluter
            if (isFirstSubscriber && value != null)
            {
                SubscriberConnected?.Invoke(this, EventArgs.Empty);
            }

            lock (_temperaturesLock)
            {
                arbetsrum = Temperatures.TryGetValue("arbetsrum", out var a) ? a : 21.5;
                hall = Temperatures.TryGetValue("hall", out var h) ? h : 21.5;
                sovrum = Temperatures.TryGetValue("sovrum", out var s) ? s : 21.5;
            }

            // Only invoke the newly added subscriber with initial temperature values
            value?.Invoke(this, new ShellyTemperatureChangedEventArgs("arbetsrum", arbetsrum));
            value?.Invoke(this, new ShellyTemperatureChangedEventArgs("hall", hall));
            value?.Invoke(this, new ShellyTemperatureChangedEventArgs("sovrum", sovrum));
        }
        remove
        {
            bool isLastRemoved;

            lock (_eventLock)
            {
                _temperatureChangedHandlers -= value;
                isLastRemoved = _temperatureChangedHandlers == null;
            }

            // Trigga SubscriberDisconnected när sista prenumeranten kopplas från
            if (isLastRemoved)
            {
                SubscriberDisconnected?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    // ----- private parts -----

    private readonly object _temperaturesLock = new();
    private readonly object _eventLock = new();

    private IMqttClient? _mqttClient;
    private readonly IMqttClient? _injectedMqttClient = mqttClient;
    private MqttClientOptions? _mqttOptions;
    private volatile bool _isIntentionalDisconnect;
    private CancellationTokenSource? _disposeCts = new();

    const string BrokerAddress = "192.168.1.10";
    const int BrokerPort = 1883;
    const string ClientId = "chargemaster-shelly-mqtt";
    const int InitialReconnectDelaySeconds = 5;
    const int MaxReconnectDelaySeconds = 300;

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
        : this(null!, null!)
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

    /// <summary>
    /// Initierar tjänsten genom att hämta temperaturer från databasen, ansluta till MQTT-brokern och prenumerera på Shelly-ämnen.
    /// </summary>
    public async Task SetupAsync()
    {
        await InitiateTemperatures();

        await ConnectAsync(BrokerAddress, BrokerPort, ClientId);

        await SubscribeAsync(_shellysTopics);
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
            var dbContext = scope.ServiceProvider.GetRequiredService<Data.ApplicationDbContext>();

            // Hämta senaste temperaturvärde för varje enhet från databasen
            var latestTemperatures = await dbContext.ShellyTemperatures
                .GroupBy(t => t.DeviceId)
                .Select(g => g.OrderByDescending(t => t.Timestamp).FirstOrDefault())
                .ToListAsync();

            // Lägg till värdena i Temperatures-dictionary
            lock (_temperaturesLock)
            {
                foreach (var temp in latestTemperatures)
                {
                    if (temp != null)
                    {
                        Temperatures[temp.DeviceId] = temp.TemperatureCelsius;
                        logger?.LogDebug("Laddade senaste temperatur för {DeviceId}: {Temperature} °C från databasen",
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
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Fel vid hämtning av temperaturer från databasen, använder defaultvärden");
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

        _isIntentionalDisconnect = false;

        // Skapa bara en ny klient om det inte redan finns en (undvik dubblerade eventhanterare)
        if (_mqttClient is null)
        {
            _mqttClient = _injectedMqttClient ?? new MqttClientFactory().CreateMqttClient();
            _mqttClient.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;
            _mqttClient.ConnectedAsync += OnConnectedAsync;
            _mqttClient.DisconnectedAsync += OnDisconnectedAsync;
        }

        _mqttOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(brokerAddress, brokerPort)
            .WithClientId(clientId)
            .WithCleanSession(false)
            .Build();

        await _mqttClient.ConnectAsync(_mqttOptions, _disposeCts?.Token ?? CancellationToken.None);
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

        _isIntentionalDisconnect = true;

        if (_mqttClient.IsConnected)
        {
            await _mqttClient.DisconnectAsync();
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

        await _mqttClient.SubscribeAsync(topic);
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

    internal Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = System.Text.Encoding.UTF8.GetString(e.ApplicationMessage.Payload.ToArray());

        logger.LogDebug("MQTT-meddelande mottaget från {Topic}: {Payload}", topic, payload);

        var tempMessage = ShellyTemperatureMessageParser.Parse(topic, payload);
        if (tempMessage != null)
        {
            HandleMessageTemperature(tempMessage);
        }

        return Task.CompletedTask;
    }

    private void HandleMessageTemperature(ShellyStatusTemperatureMessage message)
    {
        double temperature = message.TemperatureCelsius;
        string src = message.DeviceId;

        logger.LogInformation("Temperatur från {Src}: {Temp} °C", src, temperature);

        lock (_temperaturesLock)
            Temperatures[src] = temperature;

        EventHandler<ShellyTemperatureChangedEventArgs>? handler;
        lock (_eventLock)
            handler = _temperatureChangedHandlers;

        handler?.Invoke(this, new ShellyTemperatureChangedEventArgs(src, temperature));
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

        ConnectionChanged?.Invoke(this, new ShellyConnectionChangedEventArgs(false));

        if (!_isIntentionalDisconnect)
        {
            _ = ReconnectAsync();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Försöker återansluta till MQTT-servern med exponentiell backoff.
    /// </summary>
    private async Task ReconnectAsync()
    {
        var delaySeconds = InitialReconnectDelaySeconds;
        var attempt = 0;

        while (_disposeCts is { IsCancellationRequested: false })
        {
            attempt++;
            logger.LogInformation("Försöker återansluta till MQTT (försök #{Attempt}) om {Delay}s...",
                attempt, delaySeconds);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), _disposeCts.Token);
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug("Återanslutning avbruten");
                return;
            }

            try
            {
                if (_mqttClient is null || _mqttOptions is null || _disposeCts is null)
                    return;

                await _mqttClient.ConnectAsync(_mqttOptions, _disposeCts.Token);
                logger.LogInformation("Återansluten till MQTT efter {Attempt} försök", attempt);

                await SubscribeAsync(_shellysTopics);
                return;
            }
            catch (OperationCanceledException)
            {
                logger.LogDebug("Återanslutning avbruten");
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Återanslutningsförsök #{Attempt} misslyckades", attempt);
                delaySeconds = Math.Min(delaySeconds * 2, MaxReconnectDelaySeconds);
            }
        }
    }

    /// <summary>
    /// Avbryter pågående återanslutning, kopplar ifrån MQTT och frigör resurser.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _isIntentionalDisconnect = true;

        if (_disposeCts is not null)
        {
            await _disposeCts.CancelAsync();
            _disposeCts.Dispose();
            _disposeCts = null;
        }

        if (_mqttClient is not null)
        {
            _mqttClient.ApplicationMessageReceivedAsync -= OnApplicationMessageReceivedAsync;
            _mqttClient.ConnectedAsync -= OnConnectedAsync;
            _mqttClient.DisconnectedAsync -= OnDisconnectedAsync;

            if (_mqttClient.IsConnected)
            {
                await _mqttClient.DisconnectAsync();
            }

            _mqttClient.Dispose();
            _mqttClient = null;
        }
    }
}

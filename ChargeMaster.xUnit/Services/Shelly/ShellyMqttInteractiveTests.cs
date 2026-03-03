using ChargeMaster.Services.Shelly;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ChargeMaster.xUnit.Services.Shelly;

/// <summary>
/// Interaktiv testklass för att jobba med ShellyMqttService.
/// Denna klass är designad för manuell testning och utforskning av MQTT-funktionalitet.
/// </summary>
public class ShellyMqttInteractiveTests : IAsyncLifetime
{
    private readonly ShellyMqttService _shellyService;
    private readonly ILogger<ShellyMqttService> _logger;
    private readonly ITestOutputHelper _output;

    public ShellyMqttInteractiveTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = new LoggerFactory();
        _logger = new Logger<ShellyMqttService>(loggerFactory);
        _shellyService = new ShellyMqttService(_logger);
    }

    /// <summary>
    /// Initialiseras före varje test.
    /// </summary>
    public ValueTask InitializeAsync()
    {
        // Här kan du sätta upp initiering om det behövs
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Städas upp efter varje test.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_shellyService.IsConnected)
        {
            await _shellyService.DisconnectAsync();
        }
    }

    /// <summary>
    /// Grundläggande test för att ansluta till MQTT-server.
    /// Modifiera brokerAddress och brokerPort enligt din miljö.
    /// </summary>
    [Fact]
    public async Task ConnectToMqttBroker()
    {
        // Konfiguration
        const string brokerAddress = "192.168.1.10";
        const int brokerPort = 1883;

        // Prenumerera på events
        _shellyService.MessageReceived += OnMessageReceived;
        _shellyService.ConnectionStateChanged += OnConnectionStateChanged;

        // Anslut
        await _shellyService.ConnectAsync(brokerAddress, brokerPort);

        // Prenumerera på ämnen
        await _shellyService.SubscribeAsync(
            //"test/topic",
            "shelly-arbetsrum/events/rpc"
        );

        // Vänta en stund för att ta emot meddelanden
        await Task.Delay(TimeSpan.FromSeconds(10));

        // Koppla ifrån
        await _shellyService.DisconnectAsync();
    }
    
    /// <summary>
    /// Test för att prenumerera på specifika Shelly-ämnen.
    /// </summary>
    [Fact(Skip = "Aktivera när du är redo att testa prenumeration")]
    public async Task SubscribeToShellyDevices()
    {
        const string brokerAddress = "192.168.1.100";
        const int brokerPort = 1883;

        _shellyService.MessageReceived += OnMessageReceived;
        _shellyService.ConnectionStateChanged += OnConnectionStateChanged;

        await _shellyService.ConnectAsync(brokerAddress, brokerPort);

        // Prenumerera på alla Shelly-enheter
        await _shellyService.SubscribeAsync(
            "shellies/shellyswitch-1/relay/0",
            "shellies/shellyswitch-1/relay/0/power",
            "shellies/shellyswitch-1/info",
            "shellies/shellyswitch-1/status"
        );

        // Vänta för att ta emot meddelanden
        await Task.Delay(TimeSpan.FromSeconds(15));

        await _shellyService.DisconnectAsync();
    }

    /// <summary>
    /// Test för att lyssna på alla meddelanden från alla Shelly-enheter.
    /// </summary>
    [Fact]
    public async Task ListenToAllShellyMessages()
    {
        const string brokerAddress = "192.168.1.10";
        const int brokerPort = 1883;

        _shellyService.MessageReceived += OnMessageReceived;
        _shellyService.ConnectionStateChanged += OnConnectionStateChanged;

        await _shellyService.ConnectAsync(brokerAddress, brokerPort);

        // Prenumerera på alla ämnen med wildcard
        await _shellyService.SubscribeAsync("#");

        // Vänta på meddelanden
        await Task.Delay(TimeSpan.FromSeconds(30));

        await _shellyService.DisconnectAsync();
    }

    // ===== EVENT HANDLERS =====

    private void OnMessageReceived(object? sender, ShellyMqttMessageEventArgs e)
    {
        _output.WriteLine($"[MESSAGE] Topic: {e.Topic}");
        _output.WriteLine($"[MESSAGE] Payload: {e.Payload}");
        _output.WriteLine($"[MESSAGE] Time: {e.Timestamp:HH:mm:ss.fff}");
        _output.WriteLine("---");
    }

    private void OnConnectionStateChanged(object? sender, ShellyMqttConnectionStateChangedEventArgs e)
    {
        if (e.IsConnected)
        {
            _output.WriteLine($"[CONNECTION] Ansluten vid {e.Timestamp:HH:mm:ss}");
        }
        else
        {
            _output.WriteLine($"[CONNECTION] Frånkopplad vid {e.Timestamp:HH:mm:ss}");
            if (!string.IsNullOrEmpty(e.Reason))
            {
                _output.WriteLine($"[CONNECTION] Anledning: {e.Reason}");
            }
        }
    }
}

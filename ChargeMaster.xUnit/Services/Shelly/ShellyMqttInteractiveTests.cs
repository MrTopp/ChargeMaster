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
        await _shellyService.SetupAsync();

        // Vänta en stund för att ta emot meddelanden
        await Task.Delay(TimeSpan.FromSeconds(30));

        // Koppla ifrån
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

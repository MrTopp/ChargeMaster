using ChargeMaster.Data;
using ChargeMaster.Services.Shelly;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

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

        // Skapa en mock av IServiceScopeFactory för testning
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        var mockScope = new Mock<IServiceScope>();
        var mockServiceProvider = new Mock<IServiceProvider>();

        // Skapa en mock av ApplicationDbContext - returnera tom lista för ShellyTemperatures
        var mockDbContext = new Mock<ApplicationDbContext>(
            new DbContextOptionsBuilder<ApplicationDbContext>().Options);

        var mockDbSet = new Mock<DbSet<Data.ShellyTemperature>>();
        mockDbSet.As<IAsyncEnumerable<Data.ShellyTemperature>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(new AsyncEnumerator(new List<Data.ShellyTemperature>().GetEnumerator()));

        mockDbContext.Setup(db => db.ShellyTemperatures).Returns(mockDbSet.Object);

        // Konfigurera scope factory att returnera vår mockade DbContext
        mockScopeFactory
            .Setup(f => f.CreateScope())
            .Returns(mockScope.Object);

        mockScope
            .Setup(s => s.ServiceProvider)
            .Returns(mockServiceProvider.Object);

        mockServiceProvider
            .Setup(p => p.GetService(typeof(ApplicationDbContext)))
            .Returns(mockDbContext.Object);

        _shellyService = new ShellyMqttService(mockScopeFactory.Object, _logger);
    }

    private class AsyncEnumerator : IAsyncEnumerator<Data.ShellyTemperature>
    {
        private readonly IEnumerator<Data.ShellyTemperature> _enumerator;

        public AsyncEnumerator(IEnumerator<Data.ShellyTemperature> enumerator) => _enumerator = enumerator;

        public Data.ShellyTemperature Current => _enumerator.Current;

        public async ValueTask<bool> MoveNextAsync() => _enumerator.MoveNext();

        public async ValueTask DisposeAsync() { }
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
    [Fact(Skip="Only for interactive testing")]
    public async Task ConnectToMqttBroker()
    {
        // Konfiguration

        // Prenumerera på events
        await _shellyService.SetupAsync();

        // Vänta en stund för att ta emot meddelanden
        await Task.Delay(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

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

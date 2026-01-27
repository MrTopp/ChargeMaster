using ChargeMaster.Models;
using ChargeMaster.Services;
using ChargeMaster.Workers;
using ChargeMaster.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Text;

namespace ChargeMaster.xUnit.Workers;

[CollectionDefinition(nameof(WallboxHttpClientCollection))]
public sealed class WallboxHttpClientCollection : ICollectionFixture<WallboxHttpClientFixture>
{
}

public sealed class WallboxHttpClientFixture : IDisposable
{
    public HttpClient HttpClient { get; } = new()
    {
        BaseAddress = new Uri("http://192.168.1.205:8080/")
    };

    public void Dispose()
    {
        HttpClient.Dispose();
    }
}

[Collection(nameof(WallboxHttpClientCollection))]
public class WallboxWorkerTests(WallboxHttpClientFixture fixture)
{
    private readonly HttpClient httpClient = fixture.HttpClient;

    [Fact]
    public async Task InitializeWallboxStatus_OK()
    {
        // Arrange
        var wallbox = new WallboxService(httpClient);
        var services = new ServiceCollection().BuildServiceProvider();
        var logger = new LoggerFactory().CreateLogger<WallboxWorker>();
        var worker = new WallboxWorker(services, wallbox, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        var result = await worker.InitializeWallboxStatus(cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Serial > 0);
    }

    [Fact]
    public async Task CheckWallboxTime_OK()
    {
        // Arrange
        var wallbox = new WallboxService(httpClient);
        var services = new ServiceCollection().BuildServiceProvider();
        var logger = new LoggerFactory().CreateLogger<WallboxWorker>();

        var worker = new WallboxWorker(services, wallbox, logger);

        var now = DateTime.Now;

        var status = new WallboxStatus(
            Serial: 1,
            OcppState: null,
            ConnectedToInternet: true,
            FreeCharging: false,
            OcppConnectionState: null,
            Connector: "CONNECTED",
            Mode: "ALWAYS_ON",
            CurrentLimit: 0,
            FactoryCurrentLimit: 0,
            SwitchCurrentLimit: 0,
            PowerMode: "",
            CurrentChargingCurrent: 0,
            CurrentChargingPower: 0,
            AccSessionEnergy: 0,
            SessionStartTime: null,
            ChargeboxTime: now.ToString("HH:mm"),
            AccSessionMillis: 0,
            LatestReading: 0,
            ChargeStatus: 0,
            UpdateStatus: null,
            CurrentTemperature: 0,
            SessionStartValue: 0,
            NrOfPhases: 0,
            SlaveControlWarning: false,
            SupportConnectionEnabled: false,
            DatetimeConfigured: false,
            PilotLevel: 0,
            MainCharger: null,
            TwinCharger: null);

        // Act
        await worker.CheckWallboxTime(status);
    }

    [Fact]
    public async Task CheckWallboxSchedule_OK()
    {
        // Arrange
        var wallbox = new WallboxService(httpClient);
        var services = new ServiceCollection().BuildServiceProvider();
        var logger = new LoggerFactory().CreateLogger<WallboxWorker>();
        var worker = new WallboxWorker(services, wallbox, logger);

        // Act
        await worker.CheckWallboxSchedule();

        // Assert (success-path only): ensure we can still read schema after applying rules.
        var schema = await wallbox.GetSchemaAsync();
        Assert.NotNull(schema);
    }

    [Fact(Skip = "Endast för manuell körning av långa serier med mätdata")]
    public async Task ReadAndStoreAsync_DebugOnly()
    {
        var services = new ServiceCollection();

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddSingleton(new WallboxService(httpClient));

        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        }

        var logger = new LoggerFactory().CreateLogger<WallboxWorker>();
        var worker = new WallboxWorker(provider, provider.GetRequiredService<WallboxService>(), logger);

        for (int i = 0; i < 1000; i++)
        {
            await worker.ReadAndStoreAsync(CancellationToken.None);
            // wait 10 seconds between reads
            await Task.Delay(TimeSpan.FromSeconds(10), CancellationToken.None);
        }
    }

    [Fact(Skip = "Use for debugging ExecuteAsync loop")]
    //[Fact]
    public async Task ExecuteAsync_Debug()
    {
        var services = new ServiceCollection();

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddSingleton(new WallboxService(httpClient));
        services.AddLogging();

        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Ensure DB is created
            await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        }

        var logger = provider.GetRequiredService<ILogger<WallboxWorker>>();
        var worker = new TestableWallboxWorker(provider, provider.GetRequiredService<WallboxService>(), logger);

        await worker.ExecuteAsyncPublic(TestContext.Current.CancellationToken);
    }
}

public class TestableWallboxWorker(IServiceProvider serviceProvider,
    WallboxService wallboxService, ILogger<WallboxWorker> logger) : WallboxWorker(serviceProvider, wallboxService, logger)
{
    public Task ExecuteAsyncPublic(CancellationToken stoppingToken)
    {
        return ExecuteAsync(stoppingToken);
    }
}

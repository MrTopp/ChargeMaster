using ChargeMaster.Workers;
using ChargeMaster.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ChargeMaster.Services.Wallbox;

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
    private readonly HttpClient _httpClient = fixture.HttpClient;

    [Fact]
    public async Task InitializeWallboxStatus_OK()
    {
        // Arrange
        var wallbox = new WallboxService(_httpClient, new Logger<WallboxService>(new LoggerFactory()));
        var logger = new LoggerFactory().CreateLogger<WallboxWorker>();
        var worker = new WallboxWorker(null!, wallbox, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        var result = await worker.InitializeWallboxStatusAsync(cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Serial > 0);
    }

    [Fact]
    public async Task CheckWallboxTime_OK()
    {
        // Arrange
        var wallbox = new WallboxService(_httpClient, new Logger<WallboxService>(new LoggerFactory()));
        var logger = new LoggerFactory().CreateLogger<WallboxWorker>();

        var worker = new WallboxWorker(null!, wallbox, logger);

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
        await worker.CheckWallboxTimeAsync(status);
    }

    [Fact]
    public async Task CheckWallboxSchedule_OK()
    {
        // Arrange
        var wallbox = new WallboxService(_httpClient, new Logger<WallboxService>(new LoggerFactory()));
        var logger = new LoggerFactory().CreateLogger<WallboxWorker>();
        var worker = new WallboxWorker(null!, wallbox, logger);

        // Act
        await worker.CheckWallboxScheduleAsync();

        // Assert (success-path only): ensure we can still read schema after applying rules.
        var schema = await wallbox.GetSchemaAsync();
        Assert.NotNull(schema);
    }

    //[Fact(Skip = "Endast för manuell körning av långa serier med mätdata")]
    [Fact]
    public async Task ReadAndStoreAsync_DebugOnly()
    {
        var services = CreateServiceCollection();

        await using var provider = services.BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);


        var logger = new LoggerFactory().CreateLogger<WallboxWorker>();
        //var db = provider.GetRequiredService<ApplicationDbContext>();
        var wallboxService = provider.GetRequiredService<WallboxService>();
        var worker = new WallboxWorker(provider, wallboxService, logger);

        // for (int i = 0; i < 1000; i++)
        {
            var effekt = await worker.ReadEnergyAsync(CancellationToken.None);
            // wait 10 seconds between reads
            await Task.Delay(TimeSpan.FromSeconds(10), CancellationToken.None);
        }
    }

    private ServiceCollection CreateServiceCollection()
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
        services.AddSingleton(new WallboxService(_httpClient, new Logger<WallboxService>(new LoggerFactory())));
        return services;
    }

    [Fact]
    public async Task Phase1CurrentEnergy_OK()
    {
        // Arrange
        var services = CreateServiceCollection();
        await using var provider = services.BuildServiceProvider();

        var wallbox = new WallboxService(_httpClient, new Logger<WallboxService>(new LoggerFactory()));
        var logger = new LoggerFactory().CreateLogger<WallboxWorker>();
        var worker = new WallboxWorker(provider, wallbox, logger);
        // Act
        var result = await worker.ReadEnergyAsync(CancellationToken.None);
        // Assert
        Assert.NotNull(result);
        Assert.True(result.Phase1CurrentEnergy > 0);
        Assert.True(result.Phase2CurrentEnergy > 0);
        Assert.True(result.Phase3CurrentEnergy > 0);
        Assert.True(result.CurrentEnergy > 0);
        int totalEnergy = (int)(result.Phase1CurrentEnergy + result.Phase2CurrentEnergy + result.Phase3CurrentEnergy);
        Assert.Equal(totalEnergy, result.CurrentEnergy);
        return;
    }

    [Fact(Skip = "Use for debugging ExecuteAsync loop")]
    //[Fact]
    public async Task ExecuteAsync_Debug()
    {
        // Arrange
        var services = CreateServiceCollection();
        await using var provider = services.BuildServiceProvider();
        var db = provider.GetService<ApplicationDbContext>();
        await db!.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);


        var logger = provider.GetRequiredService<ILogger<WallboxWorker>>();
        var wallboxService = provider.GetRequiredService<WallboxService>();
        var worker = new WallboxWorker(provider, wallboxService, logger);

        await worker.WallboxLoop(TestContext.Current.CancellationToken);
    }
}

using ChargeMaster.Workers;
using ChargeMaster.Data;
using ChargeMaster.Services.ElectricityPrice;
using ChargeMaster.Services.InfluxDB;
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

    [Fact(Skip="Only for interactive testing")]
    public async Task InitializeWallboxStatus_OK()
    {
        // Arrange
        var wallbox = new WallboxService(_httpClient, new Logger<WallboxService>(new LoggerFactory()));
        var logger = new LoggerFactory().CreateLogger<WallboxWorker>();
        var influxLogger = new LoggerFactory().CreateLogger<InfluxDbService>();
        var influxDbService = new InfluxDbService(Microsoft.Extensions.Options.Options.Create(new InfluxDBOptions { Url = "http://localhost:8086", Token = "test", Org = "test", Bucket = "test" }), null!,
            influxLogger);
        var worker = new WallboxWorker(null!, wallbox, influxDbService, logger);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        var result = await worker.InitializeWallboxStatusAsync(cts.Token);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Serial > 0);
    }

    [Fact(Skip="Only for interactive testing")]
    public async Task CheckWallboxTime_OK()
    {
        // Arrange
        var wallbox = new WallboxService(_httpClient, new Logger<WallboxService>(new LoggerFactory()));
        var logger = new LoggerFactory().CreateLogger<WallboxWorker>();
        var influxLogger = new LoggerFactory().CreateLogger<InfluxDbService>();
        var influxDbService = new InfluxDbService(Microsoft.Extensions.Options.Options.Create(new InfluxDBOptions { Url = "http://localhost:8086", Token = "test", Org = "test", Bucket = "test" }), 
                null!, influxLogger);

        var worker = new WallboxWorker(null!, wallbox, influxDbService, logger);

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

    [Fact(Skip="Only for interactive testing")]
    public async Task ReadEnergyAsync_Debug()
    {
        // Arrange - Set up services with database and wallbox
        var services = CreateServiceCollection();

        await using var provider = services.BuildServiceProvider();

        var db = provider.GetRequiredService<ApplicationDbContext>();
        //await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var wallboxService = provider.GetRequiredService<WallboxService>();
        var logger = provider.GetRequiredService<ILogger<WallboxWorker>>();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var influxDbService = provider.GetRequiredService<InfluxDbService>();

        var worker = new WallboxWorker(scopeFactory, wallboxService, influxDbService, logger);

        // Act - Run ReadEnergyAsync for debugging
        var result = await worker.ReadEnergyAsync(TestContext.Current.CancellationToken, DateTime.Now);

        // Log results for debugging
        if (result != null)
        {
            // Write to InfluxDB
            try
            {
                await influxDbService.WriteWallboxMeterInfoAsync(result);
                logger.LogInformation("Successfully wrote WallboxMeterInfo to InfluxDB");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write WallboxMeterInfo to InfluxDB");
            }
        }
        else
        {
            logger.LogInformation("ReadEnergyAsync returned null");
        }
    }

    [Fact(Skip="Only for interactive testing")]
    public async Task KalkyleraGrans_NoAssertion()
    {
        // Arrange - Set up services with database and wallbox
        var services = CreateServiceCollection();

        await using var provider = services.BuildServiceProvider();

        var wallboxService = provider.GetRequiredService<WallboxService>();
        var logger = provider.GetRequiredService<ILogger<WallboxWorker>>();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var influxDbService = provider.GetRequiredService<InfluxDbService>();

        var worker = new WallboxWorker(scopeFactory, wallboxService, influxDbService, logger);

        // Act
        await worker.KalkyleraGrans(DateTime.Now, CancellationToken.None);
    }

    private ServiceCollection CreateServiceCollection()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(FindAppSettings(), optional: false)
            .Build();
        var connectionString = config.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddLogging(builder => builder.AddConsole());
        services.AddHttpClient();
        services.Configure<InfluxDBOptions>(config.GetSection("InfluxDB"));
        services.AddSingleton(new WallboxService(_httpClient, new Logger<WallboxService>(new LoggerFactory())));
        services.AddSingleton<ElectricityPriceService>();
        services.AddSingleton<InfluxDbService>();
        return services;
    }

    /// <summary>
    /// Söker upp huvudprojektets appsettings.json relativt testbinärens katalog.
    /// </summary>
    private static string FindAppSettings()
    {
        var path = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "../../../../ChargeMaster/appsettings.Development.json"));

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Kunde inte hitta appsettings.json. Sökte på: {path}");
        }

        return path;
    }
}
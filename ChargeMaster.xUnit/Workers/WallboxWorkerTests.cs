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
        var influxDbService = new InfluxDbService(Microsoft.Extensions.Options.Options.Create(new InfluxDBOptions { Url = "http://localhost:8086", Token = "test", Org = "test", Bucket = "test" }), null,
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
                null, influxLogger);

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
        await worker.KalkyleraGrans(DateTime.Now);
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

    [Fact(Skip="Only for interactive testing")]
    public void GenerateHourBoundaryReadings_SameHour_ReturnsSingleEntry()
    {
        // Arrange
        var worker = CreateMinimalWorker();
        var previousTime = new DateTime(2024, 1, 15, 14, 30, 0);
        var currentTime = new DateTime(2024, 1, 15, 14, 45, 0);
        long previousEnergy = 1000;
        long currentEnergy = 1100;

        // Act
        var result = worker.GenerateHourBoundaryReadings(previousTime, previousEnergy, currentTime, currentEnergy);

        // Assert
        Assert.Single(result);
        Assert.Equal(currentTime, result[0].ReadAt);
        Assert.Equal(currentEnergy, result[0].AccEnergy);
    }

    [Fact(Skip="Only for interactive testing")]
    public void GenerateHourBoundaryReadings_CrossesOneHour_ReturnsTwoEntries()
    {
        // Arrange
        var worker = CreateMinimalWorker();
        var previousTime = new DateTime(2024, 1, 15, 14, 45, 0);
        var currentTime = new DateTime(2024, 1, 15, 15, 15, 0);
        long previousEnergy = 1000;
        long currentEnergy = 1100;

        // Total tid: 25 min, energi: 100 Wh
        // Tid till 15:00 = 15 min -> 100 * 15/25 = 60 Wh
        // Förväntad energi vid 15:00 = 1000 + 60 = 1060

        // Act
        var result = worker.GenerateHourBoundaryReadings(previousTime, previousEnergy, currentTime, currentEnergy);

        // Assert
        Assert.Equal(2, result.Count);

        // Första posten vid timgränsen
        Assert.Equal(new DateTime(2024, 1, 15, 15, 0, 0), result[0].ReadAt);
        Assert.Equal(1050, result[0].AccEnergy);

        // Andra posten med slutvärdet
        Assert.Equal(currentTime, result[1].ReadAt);
        Assert.Equal(currentEnergy, result[1].AccEnergy);
    }

    [Fact(Skip="Only for interactive testing")]
    public void GenerateHourBoundaryReadings_CrossesMultipleHours_ReturnsEntryPerHour()
    {
        // Arrange
        var worker = CreateMinimalWorker();
        var previousTime = new DateTime(2024, 1, 15, 14, 30, 0);
        var currentTime = new DateTime(2024, 1, 15, 17, 15, 0);
        long previousEnergy = 1000;
        long currentEnergy = 2000;

        // Total tid: 2h 45min = 165 min, energi: 1000 Wh
        // Energi per minut: 1000/165 ≈ 6.06 Wh/min

        // Tid till 15:00 = 30 min -> 1000 * 30/165 ≈ 182 Wh -> AccEnergy = 1182
        // Tid till 16:00 = 90 min -> 1000 * 90/165 ≈ 545 Wh -> AccEnergy = 1545
        // Tid till 17:00 = 150 min -> 1000 * 150/165 ≈ 909 Wh -> AccEnergy = 1909

        // Act
        var result = worker.GenerateHourBoundaryReadings(previousTime, previousEnergy, currentTime, currentEnergy);

        // Assert
        Assert.Equal(4, result.Count); // 3 timgränser + slutmätning

        Assert.Equal(new DateTime(2024, 1, 15, 15, 0, 0), result[0].ReadAt);
        Assert.Equal(new DateTime(2024, 1, 15, 16, 0, 0), result[1].ReadAt);
        Assert.Equal(new DateTime(2024, 1, 15, 17, 0, 0), result[2].ReadAt);
        Assert.Equal(currentTime, result[3].ReadAt);
        Assert.Equal(currentEnergy, result[3].AccEnergy);

        // Verifiera att energin ökar monotont
        Assert.True(result[0].AccEnergy > previousEnergy);
        Assert.True(result[1].AccEnergy > result[0].AccEnergy);
        Assert.True(result[2].AccEnergy > result[1].AccEnergy);
        Assert.True(result[3].AccEnergy > result[2].AccEnergy);
    }

    [Fact(Skip="Only for interactive testing")]
    public void GenerateHourBoundaryReadings_CrossesMidnight_HandlesDateChange()
    {
        // Arrange
        var worker = CreateMinimalWorker();
        var previousTime = new DateTime(2024, 1, 15, 23, 45, 0);
        var currentTime = new DateTime(2024, 1, 16, 0, 15, 0);
        long previousEnergy = 1000;
        long currentEnergy = 1100;

        // Act
        var result = worker.GenerateHourBoundaryReadings(previousTime, previousEnergy, currentTime, currentEnergy);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(new DateTime(2024, 1, 16, 0, 0, 0), result[0].ReadAt);
        Assert.Equal(currentTime, result[1].ReadAt);
    }

    private static WallboxWorker CreateMinimalWorker()
    {
        var logger = new LoggerFactory().CreateLogger<WallboxWorker>();
        var influxLogger = new LoggerFactory().CreateLogger<InfluxDbService>();
        var influxDbService = new InfluxDbService(
            Microsoft.Extensions.Options.Options.Create(new InfluxDBOptions 
            { 
                Url = "http://localhost:8086", 
                Token = "test", 
                Org = "test", 
                Bucket = "test" 
            }), 
            null,
            influxLogger);

        return new WallboxWorker(null!, null!, influxDbService, logger);
    }
}
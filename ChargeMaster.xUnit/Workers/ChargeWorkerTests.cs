using ChargeMaster.Data;
using ChargeMaster.Services.InfluxDB;
using ChargeMaster.Services.TibberVehicle;
using ChargeMaster.Services.Wallbox;
using ChargeMaster.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ChargeMaster.xUnit.Workers;

public class ChargeWorkerTests
{
    private readonly ChargeWorker _worker = SetUpChargeWorker().Result;

    [Fact(Skip = "Only for interactive testing")]
    public async Task ChargeLoop_OK()
    {
        await _worker.ChargeLoop(CancellationToken.None);
    }

    [Fact(Skip = "Only for interactive testing")]
    public async Task LaddBehovAsync_OK()
    {
        // Act
        var result = await _worker.LaddBehovAsync();

        // Assert
        Assert.Equal(0, result);
    }

    [Fact(Skip = "Only for interactive testing")]
    public void SkapaKvartlista_OK()
    {
        // Act
        var kvartlista = _worker.GetKvartlista();
    }

    [Fact(Skip = "Only for interactive testing")]
    public async Task GetHourlyEnergyUsage_OK()
    {
        // Arrange
        var wallboxWorker = await SetUpWallboxWorker();
        var dateInMonth = DateTime.Now;

        // Act
        var hourlyUsage
            = await wallboxWorker.GetHourlyEnergyUsageAsync(dateInMonth,
                TestContext.Current.CancellationToken);

        // Assert
        var idag = hourlyUsage.Where(x => x.Hour >= DateTime.Today).ToList();
    }

    private static Task<ChargeWorker> SetUpChargeWorker()
    {
        var services = new ServiceCollection();

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException(
                                   "Connection string 'DefaultConnection' not found.");

        connectionString
            = "Host=192.168.1.10;Port=5432;Database=chargemaster_db;Username=chargemaster;Password=3fR%qnNAkjW9_h";

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        HttpClient wallboxClient = new HttpClient
            { BaseAddress = new Uri("http://192.168.1.205:8080/") };
        var wallboxService
            = new WallboxService(wallboxClient, new Logger<WallboxService>(new LoggerFactory()));

        services.AddSingleton(wallboxService);
        services.AddLogging();

        // Mock TibberVehicleService
        var mockTibberService = new Mock<TibberVehicleService>(
            new Mock<TibberOAuthService>(new Mock<IOptions<TibberOAuthOptions>>().Object,
                new Mock<HttpClient>().Object, new Mock<TibberTokenStorage>().Object,
                new Mock<ILogger<TibberOAuthService>>().Object).Object,
            new HttpClient(),
            new Mock<ILogger<TibberVehicleService>>().Object);

        services.AddSingleton(mockTibberService.Object);

        var provider = services.BuildServiceProvider();

        //await using (var scope = provider.CreateAsyncScope())
        //{
        //    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        //    // Ensure DB is created
        //    await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        //}
        var mockLogger = new Mock<ILogger<ChargeWorker>>();

        var worker = new ChargeWorker(null!, null!, null!, null!, null!, mockLogger.Object);
        return Task.FromResult(worker);
    }

    private static Task<WallboxWorker> SetUpWallboxWorker()
    {
        var services = new ServiceCollection();

        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException(
                                   "Connection string 'DefaultConnection' not found.");


        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        HttpClient wallboxClient = new HttpClient
            { BaseAddress = new Uri("http://192.168.1.205:8080/") };
        var wallboxService
            = new WallboxService(wallboxClient, new Logger<WallboxService>(new LoggerFactory()));

        services.AddSingleton(wallboxService);
        services.AddLogging();

        var provider = services.BuildServiceProvider();

        var mockLogger = new Mock<ILogger<WallboxWorker>>();
        var influxLogger = new LoggerFactory().CreateLogger<InfluxDbService>();
        var influxDbService = new InfluxDbService(
            Microsoft.Extensions.Options.Options.Create(new InfluxDBOptions
                { Url = "http://localhost:8086", Token = "test", Org = "test", Bucket = "test" }),
            null!,
            influxLogger);
        var worker = new WallboxWorker(null!, null!, influxDbService, mockLogger.Object);

        return Task.FromResult(worker);
    }
}

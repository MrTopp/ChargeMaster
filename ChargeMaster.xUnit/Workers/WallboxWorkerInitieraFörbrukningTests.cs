using ChargeMaster.Data;
using ChargeMaster.Services.InfluxDB;
using ChargeMaster.Workers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChargeMaster.xUnit.Workers;

public class WallboxWorkerInitieraFörbrukningTests
{
    [Fact]
    public async Task InitieraFörbrukningAsync_WithReadings_SetsPropertiesCorrectly()
    {
        // Arrange
        var now = new DateTime(2024, 6, 15, 14, 30, 0);
        var startOfHour = new DateTime(2024, 6, 15, 14, 0, 0);

        var readings = new List<WallboxMeterReading>
        {
            // 2024-06-15 12:45
            new() { Id = 1, ReadAt = new DateTime(2024, 6, 15, 12, 45, 0), AccEnergy = 1000, RawJson = "" },
            // 2024-06-15 13:30
            new() { Id = 2, ReadAt = new DateTime(2024, 6, 15, 13, 30, 0), AccEnergy = 1500, RawJson = "" },
            // 2024-06-15 14:15
            new() { Id = 3, ReadAt = new DateTime(2024, 6, 15, 14, 15, 0), AccEnergy = 2000, RawJson = "" },
            // 2024-06-15 14:25
            new() { Id = 4, ReadAt = new DateTime(2024, 6, 15, 14, 25, 0), AccEnergy = 2200, RawJson = "" }
        };

        await using var context = CreateInMemoryContext(readings);
        var worker = CreateWorkerWithContext(context);
        DateTime testNu = new DateTime(2024, 6, 15, 14, 30, 0);

        // Act
        await worker.InitieraFörbrukningAsync(CancellationToken.None, testNu);

        // Assert
        Assert.Equal(readings[0].ReadAt, worker.NästSistaMeterInfoFöregåendeTimme?.ReadDateTime);
        Assert.Equal(readings[1].ReadAt, worker.SistaMeterInfoFöregåendeTimme?.ReadDateTime);
        Assert.Equal(readings[2].ReadAt, worker.FöregåendeMeterInfo?.ReadDateTime);
        Assert.Equal(readings[3].ReadAt, worker.NuvarandeMeterInfo?.ReadDateTime);
        Assert.True(worker.WallboxInitierad);
    }

    [Fact]
    public async Task InitieraFörbrukningAsync_WithNoReadings_SetsDefaultValues()
    {
        // Arrange
        await using var context = CreateInMemoryContext([]);
        var worker = CreateWorkerWithContext(context);
        DateTime testNu = new DateTime(2024, 6, 15, 14, 30, 0);

        // Act
        await worker.InitieraFörbrukningAsync(CancellationToken.None, testNu);

        // Assert
        Assert.True(worker.WallboxInitierad);
        Assert.Equal(0, worker.FörbrukningFöregåendeTimme);
    }

    [Fact]
    public async Task InitieraFörbrukningAsync_CalculatesFörbrukningFöregåendeTimme()
    {
        // Arrange - Sätt upp mätningar där vi kan verifiera förbrukning föregående timme
        var readings = new List<WallboxMeterReading>
        {
            // 2024-06-15 12:00 (slutet på timme 12)
            new() { Id = 1, ReadAt = new DateTime(2024, 6, 15, 13, 0, 0), AccEnergy = 1000, RawJson = "" },
            // 2024-06-15 13:00 (slutet på föregående timme)
            new() { Id = 2, ReadAt = new DateTime(2024, 6, 15, 14, 0, 0), AccEnergy = 1500, RawJson = "" },
            // 2024-06-15 14:20 (innevarande timme)
            new() { Id = 3, ReadAt = new DateTime(2024, 6, 15, 14, 20, 0), AccEnergy = 1800, RawJson = "" },
            // 2024-06-15 14:30 (innevarande timme, senare än föregående)
            new() { Id = 4, ReadAt = new DateTime(2024, 6, 15, 14, 30, 0), AccEnergy = 2000, RawJson = "" }
        };

        await using var context = CreateInMemoryContext(readings);
        var worker = CreateWorkerWithContext(context);
        DateTime testNu = new DateTime(2024, 6, 15, 14, 30, 0);

        // Act
        await worker.InitieraFörbrukningAsync(CancellationToken.None, testNu);

        // Assert
        Assert.Equal(readings[0].ReadAt, worker.NästSistaMeterInfoFöregåendeTimme?.ReadDateTime);
        Assert.Equal(readings[1].ReadAt, worker.SistaMeterInfoFöregåendeTimme?.ReadDateTime);
        Assert.Equal(readings[2].ReadAt, worker.FöregåendeMeterInfo?.ReadDateTime);
        Assert.Equal(readings[3].ReadAt, worker.NuvarandeMeterInfo?.ReadDateTime);

        // Förbrukning föregående timme = 1500 - 1000 = 500 Wh
        Assert.Equal(500, worker.FörbrukningFöregåendeTimme);
    }

    [Fact]
    public async Task InitieraFörbrukningAsync_WithSingleReading_HandlesGracefully()
    {
        // Arrange
        var readings = new List<WallboxMeterReading>
        {
            new() { Id = 1, ReadAt = new DateTime(2024, 6, 15, 14, 30, 0), AccEnergy = 1000, RawJson = "" }
        };

        await using var context = CreateInMemoryContext(readings);
        var worker = CreateWorkerWithContext(context);

        // Act
        await worker.InitieraFörbrukningAsync(CancellationToken.None);

        // Assert
        Assert.True(worker.WallboxInitierad);
    }

    [Fact]
    public async Task InitieraFörbrukningAsync_WithReadingsSpanningMultipleDays_WorksCorrectly()
    {
        // Arrange
        var readings = new List<WallboxMeterReading>
        {
            new() { Id = 1, ReadAt = new DateTime(2024, 6, 14, 23, 0, 0), AccEnergy = 5000, RawJson = "" },
            new() { Id = 2, ReadAt = new DateTime(2024, 6, 15, 0, 0, 0), AccEnergy = 5200, RawJson = "" },
            new() { Id = 3, ReadAt = new DateTime(2024, 6, 15, 1, 0, 0), AccEnergy = 5400, RawJson = "" },
            new() { Id = 4, ReadAt = new DateTime(2024, 6, 15, 2, 30, 0), AccEnergy = 5600, RawJson = "" }
        };

        await using var context = CreateInMemoryContext(readings);
        var worker = CreateWorkerWithContext(context);

        // Act
        await worker.InitieraFörbrukningAsync(CancellationToken.None);

        // Assert
        Assert.True(worker.WallboxInitierad);
        // Förbrukning föregående timme = 5400 - 5200 = 200 Wh
        Assert.Equal(200, worker.FörbrukningFöregåendeTimme);
    }

    [Fact]
    public async Task InitieraFörbrukningAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var readings = new List<WallboxMeterReading>
        {
            new() { Id = 1, ReadAt = DateTime.Now, AccEnergy = 1000, RawJson = "" }
        };

        await using var context = CreateInMemoryContext(readings);
        var worker = CreateWorkerWithContext(context);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => worker.InitieraFörbrukningAsync(cts.Token));
    }

    private static ApplicationDbContext CreateInMemoryContext(List<WallboxMeterReading> readings)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new ApplicationDbContext(options);
        context.WallboxMeterReadings.AddRange(readings);
        context.SaveChanges();

        return context;
    }

    private static WallboxWorker CreateWorkerWithContext(ApplicationDbContext context)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => context);
        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

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

        return new WallboxWorker(scopeFactory, null!, influxDbService, logger);
    }
}

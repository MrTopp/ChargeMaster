using ChargeMaster.Data;
using ChargeMaster.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ChargeMaster.xUnit.Services;

public class ElectricityPriceServiceTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _context;
    private readonly Mock<ILogger<ElectricityPriceService>> _loggerMock;
    private readonly ElectricityPriceService _service;

    public ElectricityPriceServiceTests()
    {
        // Use real HttpClient
        _httpClient = new HttpClient();

        var connectionString = "Host=127.0.0.1;Port=5432;Database=chargemaster_db;Username=postgres;Password=bulle";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        
        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
        _context.Database.BeginTransaction();
        
        _loggerMock = new Mock<ILogger<ElectricityPriceService>>();
        
        _service = new ElectricityPriceService(_httpClient, _context, _loggerMock.Object);
    }

    public void Dispose()
    {
        // Rollback transaction to clean up changes
        // Since we never committed, simply disposing the transaction (if we held a reference) or context handles it,
        // but explicit Rollback is clearer.
        if (_context.Database.CurrentTransaction != null)
        { 
            _context.Database.RollbackTransaction();
        }
        _context.Dispose();
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task HasPricesForDateAsync_ReturnsFalse_WhenNoPricesExist()
    {
        // Arrange
        var date = new DateOnly(2023, 10, 27);

        // Act
        var result = await _service.HasPricesForDateAsync(date);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HasPricesForDateAsync_ReturnsTrue_WhenPricesExist()
    {
        // Arrange
        var date = new DateOnly(2025, 10, 27);
        _context.ElectricityPrices.Add(new ElectricityPrice
        {
            TimeStart = date.ToDateTime(new TimeOnly(13, 0)),
            TimeEnd = date.ToDateTime(new TimeOnly(13, 15)),
            SekPerKwh = 1.5m,
            EurPerKwh = 0.15m,
            ExchangeRate = 10m
        });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.HasPricesForDateAsync(date);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetPricesForDateAsync_ReturnsOnlyPricesForSpecificDate()
    {
        // Arrange
        var targetDate = new DateOnly(2025, 11, 27);
        var otherDate = new DateOnly(2025, 11, 28);

        _context.ElectricityPrices.AddRange(
            new ElectricityPrice // Should match
            {
                TimeStart = targetDate.ToDateTime(new TimeOnly(10, 0)),
                TimeEnd = targetDate.ToDateTime(new TimeOnly(11, 0)),
                SekPerKwh = 1,
                EurPerKwh = 0.1m,
                ExchangeRate = 10
            },
            new ElectricityPrice // Should not match
            {
                TimeStart = otherDate.ToDateTime(new TimeOnly(10, 0)),
                TimeEnd = otherDate.ToDateTime(new TimeOnly(11, 0)),
                SekPerKwh = 2,
                EurPerKwh = 0.2m,
                ExchangeRate = 10
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetPricesForDateAsync(targetDate);

        // Assert
        Assert.Single(result);
        Assert.Equal(1, result[0].SekPerKwh);
    }

    /// <summary>
    /// Test running data fetching, deleting existing prices to force a fetch.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task FetchAndStorePricesForDateAsync_DeletesExistingPrices_AndFetchesNewData()
    {
        // Arrange
        var date = new DateTime(2025, 12, 27);
        // remove any existing prices for that date
        var existingPrices = await _context.ElectricityPrices
            .Where(p => p.TimeStart >= date && p.TimeStart < date.AddDays(1))
            .ToListAsync(TestContext.Current.CancellationToken);
        _context.ElectricityPrices.RemoveRange(existingPrices);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _service.FetchAndStorePricesForDateAsync(DateOnly.FromDateTime(date));

        // Assert
        var prices = await _context.ElectricityPrices
            .Where(p => p.TimeStart >= date && p.TimeStart < date.AddDays(1))
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.InRange(prices.Count, 96, 96); // Expecting around 96 entries

    }

    [Fact]
    public async Task FetchAndStorePricesForDateAsync_DoesNothing_IfPricesExist()
    {
        // Arrange
        var date = new DateOnly(2023, 10, 27);
        _context.ElectricityPrices.Add(new ElectricityPrice
        {
            TimeStart = date.ToDateTime(new TimeOnly(12, 0)),
            TimeEnd = date.ToDateTime(new TimeOnly(13, 0)),
            SekPerKwh = 100,
            EurPerKwh = 10,
            ExchangeRate = 10
        });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _service.FetchAndStorePricesForDateAsync(date);

        // Assert - Verify logs indicate existing prices
         _loggerMock.Verify(
             x => x.Log(
                 LogLevel.Information,
                 It.IsAny<EventId>(),
                 It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("exist")),
                 It.IsAny<Exception>(),
                 It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
             Times.Once);
    }

    [Fact]
    public async Task FetchAndStorePricesForDateAsync_FetchesAndStores_WhenNoPricesExist()
    {
        // Arrange
        // Use a real valid date for the API. 
        var date = new DateTime(2026, 1, 1);
        
        // Act
        await _service.FetchAndStorePricesForDateAsync(DateOnly.FromDateTime(date));

        // Assert -
        // Verify database content, prices from day 'date' should be 24*4 = 96 entries
        var prices = await _context.ElectricityPrices
            .Where(x => x.TimeStart >= date 
                        && x.TimeStart < date.AddDays(1)).ToListAsync(TestContext.Current.CancellationToken);
        Assert.InRange(prices.Count, 96, 96);

    }

}

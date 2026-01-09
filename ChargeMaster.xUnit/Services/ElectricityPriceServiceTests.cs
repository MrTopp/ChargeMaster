using System.Net;
using ChargeMaster.Data;
using ChargeMaster.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

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

        var connectionString = "Server=THOMASPC\\SQL2022;Database=ChargeMasterTest;Trusted_Connection=True;TrustServerCertificate=True;";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
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
        var date = new DateOnly(2023, 10, 27);
        _context.ElectricityPrices.Add(new ElectricityPrice
        {
            TimeStart = date.ToDateTime(new TimeOnly(1, 0)),
            TimeEnd = date.ToDateTime(new TimeOnly(2, 0)),
            SekPerKwh = 1.5m,
            EurPerKwh = 0.15m,
            ExchangeRate = 10m
        });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.HasPricesForDateAsync(date);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetPricesForDateAsync_ReturnsOnlyPricesForSpecificDate()
    {
        // Arrange
        var targetDate = new DateOnly(2025, 12, 27);
        var otherDate = new DateOnly(2025, 12, 28);

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
        await _context.SaveChangesAsync();

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
        var date = new DateOnly(2025, 12, 27);
        // remove any existing prices for that date
        var existingPrices = await _context.ElectricityPrices
            .Where(p => p.TimeStart >= date.ToDateTime(TimeOnly.MinValue) && p.TimeStart <= date.ToDateTime(TimeOnly.MaxValue))
            .ToListAsync();
        _context.ElectricityPrices.RemoveRange(existingPrices);
        await _context.SaveChangesAsync();

        // Act
        await _service.FetchAndStorePricesForDateAsync(date);

        // Assert
        var prices = await _context.ElectricityPrices
            .Where(p => p.TimeStart >= date.ToDateTime(TimeOnly.MinValue) && p.TimeStart <= date.ToDateTime(TimeOnly.MaxValue))
            .ToListAsync();
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
        await _context.SaveChangesAsync();

        // Act
        await _service.FetchAndStorePricesForDateAsync(date);

        // Assert - Verify logs indicate existing prices
         _loggerMock.Verify(
             x => x.Log(
                 LogLevel.Information,
                 It.IsAny<EventId>(),
                 It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("exist")),
                 It.IsAny<Exception>(),
                 It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
             Times.Once);
    }

    [Fact]
    public async Task FetchAndStorePricesForDateAsync_FetchesAndStores_WhenNoPricesExist()
    {
        // Arrange
        // Use a real valid date for the API. 
        var date = new DateOnly(2023, 1, 1);
        
        // Act
        await _service.FetchAndStorePricesForDateAsync(date);

        // Assert
        var prices = await _context.ElectricityPrices.ToListAsync();
        // The API returns prices for the whole day (24 hours typically)
        Assert.InRange(prices.Count, 23, 25);
        Assert.Contains(prices, p => p.SekPerKwh > 0);
    }

    [Fact]
    public async Task FetchAndStorePricesForDateAsync_LogsError_WhenApiFails()
    {
        // Arrange
        // Use a date that the API is likely not to support (e.g. very old date)
        // This is expected to cause a 404 or similar error from the API
        var date = new DateOnly(1970, 1, 1);
        
        // Act & Assert
        // The service is designed to re-throw exceptions
        await Assert.ThrowsAsync<HttpRequestException>(async () => 
            await _service.FetchAndStorePricesForDateAsync(date));
        
        // Assert Logger
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}

using ChargeMaster.Data;
using ChargeMaster.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace ChargeMaster.xUnit.DebugTests;

public class ElectricityPriceServiceDebug : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _context;
    private readonly ElectricityPriceService _service;

    public ElectricityPriceServiceDebug()
    {
        // Use real HttpClient
        _httpClient = new HttpClient();

        var connectionString = "Host=127.0.0.1;Port=5432;Database=chargemaster_db;Username=postgres;Password=bulle";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        
        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();
        
        var loggerMock = new Mock<ILogger<ElectricityPriceService>>();
        
        _service = new ElectricityPriceService(_httpClient, _context, loggerMock.Object);
    }

    public void Dispose()
    {
        if (_context.Database.CurrentTransaction != null)
        { 
            _context.Database.CommitTransaction();
        }
        _context.Dispose();
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task HasPricesForDateAsync_ReturnsFalse_WhenNoPricesExist()
    {
        // Arrange
        var date = DateOnly.FromDateTime(DateTime.Today);

        // Act
        var result = await _service.HasPricesForDateAsync(date);

        // Assert
        Assert.False(result);
    }
    
    /// <summary>
    /// Test running data fetching, deleting existing prices to force a fetch.
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task FetchAndStorePricesForDateAsync_Rensa_OK()
    {
        // Arrange
        var date = DateTime.Today;

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
    public async Task FetchAndStorePricesForDateAsync_utan_rensning_OK()
    {
        // Arrange
        // Use a real valid date for the API. 
        var date = DateTime.Today;
        
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

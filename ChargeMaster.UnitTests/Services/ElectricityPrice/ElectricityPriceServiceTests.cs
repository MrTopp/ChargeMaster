using Microsoft.Extensions.Logging;
using Moq;

namespace ChargeMaster.Services.ElectricityPrice.UnitTests;


/// <summary>
/// Unit tests for the ElectricityPriceService class.
/// </summary>
public partial class ElectricityPriceServiceTests
{
    /// <summary>
    /// Tests that GetPricesForDateAsync returns a non-empty list when the repository contains prices for the specified date.
    /// </summary>
    [Fact]
    public async Task GetPricesForDateAsync_WithPricesInRepository_ReturnsListOfPrices()
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var testDate = new DateOnly(2024, 1, 15);
        var expectedPrices = new List<Data.ElectricityPrice>
        {
            new Data.ElectricityPrice { Id = 1, SekPerKwh = 1.5m, TimeStart = testDate.ToDateTime(new TimeOnly(0, 0)) },
            new Data.ElectricityPrice { Id = 2, SekPerKwh = 1.8m, TimeStart = testDate.ToDateTime(new TimeOnly(1, 0)) }
        };

        mockRepository.Setup(r => r.GetPricesForDateAsync(testDate))
            .ReturnsAsync(expectedPrices);

        // Act
        var result = await service.GetPricesForDateAsync(testDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedPrices, result);
        mockRepository.Verify(r => r.GetPricesForDateAsync(testDate), Times.Once);
    }

    /// <summary>
    /// Tests that GetPricesForDateAsync returns an empty list when the repository has no prices for the specified date.
    /// </summary>
    [Fact]
    public async Task GetPricesForDateAsync_WithNoPricesInRepository_ReturnsEmptyList()
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var testDate = new DateOnly(2024, 6, 20);
        var emptyList = new List<Data.ElectricityPrice>();

        mockRepository.Setup(r => r.GetPricesForDateAsync(testDate))
            .ReturnsAsync(emptyList);

        // Act
        var result = await service.GetPricesForDateAsync(testDate);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
        mockRepository.Verify(r => r.GetPricesForDateAsync(testDate), Times.Once);
    }

    /// <summary>
    /// Tests that GetPricesForDateAsync correctly handles DateOnly.MinValue as input parameter.
    /// </summary>
    [Fact]
    public async Task GetPricesForDateAsync_WithMinValueDate_CallsRepositoryAndReturnsResult()
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var minDate = DateOnly.MinValue;
        var expectedPrices = new List<Data.ElectricityPrice>
        {
            new Data.ElectricityPrice { Id = 1, SekPerKwh = 0.5m, TimeStart = minDate.ToDateTime(new TimeOnly(0, 0)) }
        };

        mockRepository.Setup(r => r.GetPricesForDateAsync(minDate))
            .ReturnsAsync(expectedPrices);

        // Act
        var result = await service.GetPricesForDateAsync(minDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedPrices, result);
        mockRepository.Verify(r => r.GetPricesForDateAsync(minDate), Times.Once);
    }

    /// <summary>
    /// Tests that GetPricesForDateAsync correctly handles DateOnly.MaxValue as input parameter.
    /// </summary>
    [Fact]
    public async Task GetPricesForDateAsync_WithMaxValueDate_CallsRepositoryAndReturnsResult()
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var maxDate = DateOnly.MaxValue;
        var expectedPrices = new List<Data.ElectricityPrice>
        {
            new Data.ElectricityPrice { Id = 999, SekPerKwh = 99.9m, TimeStart = new DateTime(9999, 12, 31, 23, 0, 0) }
        };

        mockRepository.Setup(r => r.GetPricesForDateAsync(maxDate))
            .ReturnsAsync(expectedPrices);

        // Act
        var result = await service.GetPricesForDateAsync(maxDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedPrices, result);
        mockRepository.Verify(r => r.GetPricesForDateAsync(maxDate), Times.Once);
    }

    /// <summary>
    /// Tests that GetPricesForDateAsync correctly passes various dates to the repository.
    /// Verifies that the method acts as a proper pass-through to the repository with different date values.
    /// </summary>
    /// <param name="year">The year component of the date.</param>
    /// <param name="month">The month component of the date.</param>
    /// <param name="day">The day component of the date.</param>
    [Theory]
    [InlineData(2023, 1, 1)]
    [InlineData(2024, 12, 31)]
    [InlineData(2025, 6, 15)]
    [InlineData(2020, 2, 29)]
    [InlineData(1900, 1, 1)]
    public async Task GetPricesForDateAsync_WithVariousDates_PassesCorrectDateToRepository(int year, int month, int day)
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var testDate = new DateOnly(year, month, day);
        var expectedPrices = new List<Data.ElectricityPrice>
        {
            new Data.ElectricityPrice { Id = 1, SekPerKwh = 2.5m, TimeStart = testDate.ToDateTime(new TimeOnly(0, 0)) }
        };

        mockRepository.Setup(r => r.GetPricesForDateAsync(testDate))
            .ReturnsAsync(expectedPrices);

        // Act
        var result = await service.GetPricesForDateAsync(testDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedPrices, result);
        mockRepository.Verify(r => r.GetPricesForDateAsync(testDate), Times.Once);
    }

    /// <summary>
    /// Tests that GetPricesForDateAsync propagates exceptions thrown by the repository.
    /// Verifies that any exception from the repository layer is not caught or swallowed.
    /// </summary>
    [Fact]
    public async Task GetPricesForDateAsync_WhenRepositoryThrowsException_PropagatesException()
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var testDate = new DateOnly(2024, 3, 10);
        var expectedException = new InvalidOperationException("Database connection failed");

        mockRepository.Setup(r => r.GetPricesForDateAsync(testDate))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetPricesForDateAsync(testDate));
        Assert.Equal(expectedException.Message, exception.Message);
        mockRepository.Verify(r => r.GetPricesForDateAsync(testDate), Times.Once);
    }

    /// <summary>
    /// Tests that GetPricesForDateAsync returns a list with a single price when repository returns one item.
    /// Verifies correct handling of single-item collections.
    /// </summary>
    [Fact]
    public async Task GetPricesForDateAsync_WithSinglePriceInRepository_ReturnsSingleItemList()
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var testDate = new DateOnly(2024, 5, 1);
        var singlePrice = new Data.ElectricityPrice
        {
            Id = 42,
            SekPerKwh = 3.75m,
            EurPerKwh = 0.35m,
            ExchangeRate = 10.71m,
            TimeStart = testDate.ToDateTime(new TimeOnly(12, 0)),
            TimeEnd = testDate.ToDateTime(new TimeOnly(13, 0))
        };
        var expectedPrices = new List<Data.ElectricityPrice> { singlePrice };

        mockRepository.Setup(r => r.GetPricesForDateAsync(testDate))
            .ReturnsAsync(expectedPrices);

        // Act
        var result = await service.GetPricesForDateAsync(testDate);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(singlePrice, result[0]);
        mockRepository.Verify(r => r.GetPricesForDateAsync(testDate), Times.Once);
    }

    /// <summary>
    /// Tests that GetPricesForDateAsync returns a large list when repository returns many prices.
    /// Verifies correct handling of larger collections (96 items representing 15-minute intervals for a full day).
    /// </summary>
    [Fact]
    public async Task GetPricesForDateAsync_WithManyPricesInRepository_ReturnsFullList()
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var testDate = new DateOnly(2024, 7, 15);
        var manyPrices = new List<Data.ElectricityPrice>();
        for (int i = 0; i < 96; i++)
        {
            manyPrices.Add(new Data.ElectricityPrice
            {
                Id = i,
                SekPerKwh = 1.0m + (i * 0.01m),
                TimeStart = testDate.ToDateTime(new TimeOnly(i / 4, (i % 4) * 15))
            });
        }

        mockRepository.Setup(r => r.GetPricesForDateAsync(testDate))
            .ReturnsAsync(manyPrices);

        // Act
        var result = await service.GetPricesForDateAsync(testDate);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(96, result.Count);
        Assert.Equal(manyPrices, result);
        mockRepository.Verify(r => r.GetPricesForDateAsync(testDate), Times.Once);
    }

    /// <summary>
    /// Tests that HasPricesForDateAsync returns true when the repository indicates prices exist for the given date.
    /// </summary>
    [Fact]
    public async Task HasPricesForDateAsync_WhenRepositoryReturnsTrue_ReturnsTrue()
    {
        // Arrange
        var date = new DateOnly(2024, 1, 15);
        var mockRepository = new Mock<IElectricityPriceRepository>();
        mockRepository.Setup(r => r.HasPricesForDateAsync(date))
            .ReturnsAsync(true);
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        // Act
        var result = await service.HasPricesForDateAsync(date);

        // Assert
        Assert.True(result);
        mockRepository.Verify(r => r.HasPricesForDateAsync(date), Times.Once);
    }

    /// <summary>
    /// Tests that HasPricesForDateAsync returns false when the repository indicates no prices exist for the given date.
    /// </summary>
    [Fact]
    public async Task HasPricesForDateAsync_WhenRepositoryReturnsFalse_ReturnsFalse()
    {
        // Arrange
        var date = new DateOnly(2024, 3, 20);
        var mockRepository = new Mock<IElectricityPriceRepository>();
        mockRepository.Setup(r => r.HasPricesForDateAsync(date))
            .ReturnsAsync(false);
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        // Act
        var result = await service.HasPricesForDateAsync(date);

        // Assert
        Assert.False(result);
        mockRepository.Verify(r => r.HasPricesForDateAsync(date), Times.Once);
    }

    /// <summary>
    /// Tests that HasPricesForDateAsync correctly handles DateOnly.MinValue and delegates to the repository.
    /// </summary>
    [Fact]
    public async Task HasPricesForDateAsync_WithMinDate_CallsRepositoryWithMinDate()
    {
        // Arrange
        var date = DateOnly.MinValue;
        var mockRepository = new Mock<IElectricityPriceRepository>();
        mockRepository.Setup(r => r.HasPricesForDateAsync(date))
            .ReturnsAsync(false);
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        // Act
        var result = await service.HasPricesForDateAsync(date);

        // Assert
        Assert.False(result);
        mockRepository.Verify(r => r.HasPricesForDateAsync(date), Times.Once);
    }

    /// <summary>
    /// Tests that HasPricesForDateAsync correctly handles DateOnly.MaxValue and delegates to the repository.
    /// </summary>
    [Fact]
    public async Task HasPricesForDateAsync_WithMaxDate_CallsRepositoryWithMaxDate()
    {
        // Arrange
        var date = DateOnly.MaxValue;
        var mockRepository = new Mock<IElectricityPriceRepository>();
        mockRepository.Setup(r => r.HasPricesForDateAsync(date))
            .ReturnsAsync(true);
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        // Act
        var result = await service.HasPricesForDateAsync(date);

        // Assert
        Assert.True(result);
        mockRepository.Verify(r => r.HasPricesForDateAsync(date), Times.Once);
    }

    /// <summary>
    /// Tests that HasPricesForDateAsync with various dates correctly delegates to the repository with the expected date parameter.
    /// </summary>
    /// <param name="year">The year of the test date.</param>
    /// <param name="month">The month of the test date.</param>
    /// <param name="day">The day of the test date.</param>
    /// <param name="expectedResult">The expected boolean result from the repository.</param>
    [Theory]
    [InlineData(2024, 1, 1, true)]
    [InlineData(2024, 12, 31, false)]
    [InlineData(2023, 6, 15, true)]
    [InlineData(2025, 2, 28, false)]
    public async Task HasPricesForDateAsync_WithVariousDates_ReturnsExpectedResult(int year, int month, int day, bool expectedResult)
    {
        // Arrange
        var date = new DateOnly(year, month, day);
        var mockRepository = new Mock<IElectricityPriceRepository>();
        mockRepository.Setup(r => r.HasPricesForDateAsync(date))
            .ReturnsAsync(expectedResult);
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        // Act
        var result = await service.HasPricesForDateAsync(date);

        // Assert
        Assert.Equal(expectedResult, result);
        mockRepository.Verify(r => r.HasPricesForDateAsync(date), Times.Once);
    }

    /// <summary>
    /// Tests that DeletePricesForDateAsync calls repository with the provided date and logs information when no prices are deleted.
    /// </summary>
    [Fact]
    public async Task DeletePricesForDateAsync_WithNoPricesDeleted_CallsRepositoryAndLogsZeroCount()
    {
        // Arrange
        var date = new DateOnly(2024, 1, 15);
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new System.Net.Http.HttpClient();

        mockRepository
            .Setup(r => r.DeletePricesForDateAsync(date))
            .ReturnsAsync(0);

        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        // Act
        await service.DeletePricesForDateAsync(date);

        // Assert
        mockRepository.Verify(r => r.DeletePricesForDateAsync(date), Times.Once);
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Deleted 0 prices")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that DeletePricesForDateAsync calls repository with the provided date and logs information when one price is deleted.
    /// </summary>
    [Fact]
    public async Task DeletePricesForDateAsync_WithOnePriceDeleted_CallsRepositoryAndLogsOneCount()
    {
        // Arrange
        var date = new DateOnly(2024, 6, 10);
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new System.Net.Http.HttpClient();

        mockRepository
            .Setup(r => r.DeletePricesForDateAsync(date))
            .ReturnsAsync(1);

        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        // Act
        await service.DeletePricesForDateAsync(date);

        // Assert
        mockRepository.Verify(r => r.DeletePricesForDateAsync(date), Times.Once);
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Deleted 1 prices")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that DeletePricesForDateAsync calls repository with the provided date and logs information when multiple prices are deleted.
    /// </summary>
    [Theory]
    [InlineData(96)]
    [InlineData(10)]
    [InlineData(1000)]
    public async Task DeletePricesForDateAsync_WithMultiplePricesDeleted_CallsRepositoryAndLogsCorrectCount(int count)
    {
        // Arrange
        var date = new DateOnly(2024, 3, 20);
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new System.Net.Http.HttpClient();

        mockRepository
            .Setup(r => r.DeletePricesForDateAsync(date))
            .ReturnsAsync(count);

        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        // Act
        await service.DeletePricesForDateAsync(date);

        // Assert
        mockRepository.Verify(r => r.DeletePricesForDateAsync(date), Times.Once);
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Deleted {count} prices")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that DeletePricesForDateAsync correctly handles DateOnly.MinValue as input parameter.
    /// </summary>
    [Fact]
    public async Task DeletePricesForDateAsync_WithMinValueDate_CallsRepositoryAndLogs()
    {
        // Arrange
        var date = DateOnly.MinValue;
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new System.Net.Http.HttpClient();

        mockRepository
            .Setup(r => r.DeletePricesForDateAsync(date))
            .ReturnsAsync(5);

        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        // Act
        await service.DeletePricesForDateAsync(date);

        // Assert
        mockRepository.Verify(r => r.DeletePricesForDateAsync(date), Times.Once);
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Deleted 5 prices")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that DeletePricesForDateAsync correctly handles DateOnly.MaxValue as input parameter.
    /// </summary>
    [Fact]
    public async Task DeletePricesForDateAsync_WithMaxValueDate_CallsRepositoryAndLogs()
    {
        // Arrange
        var date = DateOnly.MaxValue;
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new System.Net.Http.HttpClient();

        mockRepository
            .Setup(r => r.DeletePricesForDateAsync(date))
            .ReturnsAsync(3);

        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        // Act
        await service.DeletePricesForDateAsync(date);

        // Assert
        mockRepository.Verify(r => r.DeletePricesForDateAsync(date), Times.Once);
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Deleted 3 prices")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that DeletePricesForDateAsync logs information with the correct date format when deleting prices.
    /// </summary>
    [Fact]
    [Trait("Category", "ProductionBugSuspected")]
    public async Task DeletePricesForDateAsync_LogsWithCorrectDateParameter()
    {
        // Arrange
        var date = new DateOnly(2024, 12, 25);
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new System.Net.Http.HttpClient();

        mockRepository
            .Setup(r => r.DeletePricesForDateAsync(date))
            .ReturnsAsync(42);

        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        // Act
        await service.DeletePricesForDateAsync(date);

        // Assert
        mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("2024-12-25")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Tests that GetPriceForDateTimeAsync returns matching price from repository when cache is empty.
    /// </summary>
    [Fact]
    public async Task GetPriceForDateTimeAsync_CacheMiss_ReturnsMatchingPriceFromRepository()
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var testDateTime = new DateTime(2024, 1, 15, 10, 30, 0);
        var testDate = DateOnly.FromDateTime(testDateTime);
        var prices = new List<ChargeMaster.Data.ElectricityPrice>
        {
            new() { TimeStart = new DateTime(2024, 1, 15, 10, 0, 0), TimeEnd = new DateTime(2024, 1, 15, 10, 15, 0) },
            new() { TimeStart = new DateTime(2024, 1, 15, 10, 15, 0), TimeEnd = new DateTime(2024, 1, 15, 10, 30, 0) },
            new() { TimeStart = new DateTime(2024, 1, 15, 10, 30, 0), TimeEnd = new DateTime(2024, 1, 15, 10, 45, 0) },
            new() { TimeStart = new DateTime(2024, 1, 15, 10, 45, 0), TimeEnd = new DateTime(2024, 1, 15, 11, 0, 0) }
        };

        mockRepository.Setup(r => r.GetPricesForDateAsync(testDate)).ReturnsAsync(prices);

        // Act
        var result = await service.GetPriceForDateTimeAsync(testDateTime);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new DateTime(2024, 1, 15, 10, 30, 0), result.TimeStart);
        Assert.Equal(new DateTime(2024, 1, 15, 10, 45, 0), result.TimeEnd);
        mockRepository.Verify(r => r.GetPricesForDateAsync(testDate), Times.Once);
    }

    /// <summary>
    /// Tests that GetPriceForDateTimeAsync returns null when no matching price exists in repository.
    /// </summary>
    [Fact]
    public async Task GetPriceForDateTimeAsync_NoMatchingPrice_ReturnsNull()
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var testDateTime = new DateTime(2024, 1, 15, 23, 59, 0);
        var testDate = DateOnly.FromDateTime(testDateTime);
        var prices = new List<ChargeMaster.Data.ElectricityPrice>
        {
            new() { TimeStart = new DateTime(2024, 1, 15, 10, 0, 0), TimeEnd = new DateTime(2024, 1, 15, 10, 15, 0) },
            new() { TimeStart = new DateTime(2024, 1, 15, 10, 15, 0), TimeEnd = new DateTime(2024, 1, 15, 10, 30, 0) }
        };

        mockRepository.Setup(r => r.GetPricesForDateAsync(testDate)).ReturnsAsync(prices);

        // Act
        var result = await service.GetPriceForDateTimeAsync(testDateTime);

        // Assert
        Assert.Null(result);
        mockRepository.Verify(r => r.GetPricesForDateAsync(testDate), Times.Once);
    }

    /// <summary>
    /// Tests that GetPriceForDateTimeAsync returns null when repository returns empty list.
    /// </summary>
    [Fact]
    public async Task GetPriceForDateTimeAsync_EmptyPriceList_ReturnsNull()
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var testDateTime = new DateTime(2024, 1, 15, 10, 30, 0);
        var testDate = DateOnly.FromDateTime(testDateTime);
        var prices = new List<ChargeMaster.Data.ElectricityPrice>();

        mockRepository.Setup(r => r.GetPricesForDateAsync(testDate)).ReturnsAsync(prices);

        // Act
        var result = await service.GetPriceForDateTimeAsync(testDateTime);

        // Assert
        Assert.Null(result);
        mockRepository.Verify(r => r.GetPricesForDateAsync(testDate), Times.Once);
    }

    /// <summary>
    /// Tests that GetPriceForDateTimeAsync uses cached prices on second call with same date.
    /// </summary>
    [Fact]
    public async Task GetPriceForDateTimeAsync_SecondCallSameDate_UsesCachedPrices()
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var testDateTime1 = new DateTime(2024, 1, 15, 10, 30, 0);
        var testDateTime2 = new DateTime(2024, 1, 15, 14, 45, 0);
        var testDate = DateOnly.FromDateTime(testDateTime1);
        var prices = new List<ChargeMaster.Data.ElectricityPrice>
        {
            new() { TimeStart = new DateTime(2024, 1, 15, 10, 0, 0), TimeEnd = new DateTime(2024, 1, 15, 11, 0, 0) },
            new() { TimeStart = new DateTime(2024, 1, 15, 14, 0, 0), TimeEnd = new DateTime(2024, 1, 15, 15, 0, 0) }
        };

        mockRepository.Setup(r => r.GetPricesForDateAsync(testDate)).ReturnsAsync(prices);

        // Act
        var result1 = await service.GetPriceForDateTimeAsync(testDateTime1);
        var result2 = await service.GetPriceForDateTimeAsync(testDateTime2);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(new DateTime(2024, 1, 15, 10, 0, 0), result1.TimeStart);
        Assert.Equal(new DateTime(2024, 1, 15, 14, 0, 0), result2.TimeStart);
        mockRepository.Verify(r => r.GetPricesForDateAsync(testDate), Times.Once);
    }

    /// <summary>
    /// Tests that GetPriceForDateTimeAsync fetches new data when date changes.
    /// </summary>
    [Fact]
    public async Task GetPriceForDateTimeAsync_DifferentDates_FetchesNewData()
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var testDateTime1 = new DateTime(2024, 1, 15, 10, 30, 0);
        var testDateTime2 = new DateTime(2024, 1, 16, 10, 30, 0);
        var testDate1 = DateOnly.FromDateTime(testDateTime1);
        var testDate2 = DateOnly.FromDateTime(testDateTime2);

        var prices1 = new List<ChargeMaster.Data.ElectricityPrice>
        {
            new() { TimeStart = new DateTime(2024, 1, 15, 10, 0, 0), TimeEnd = new DateTime(2024, 1, 15, 11, 0, 0) }
        };
        var prices2 = new List<ChargeMaster.Data.ElectricityPrice>
        {
            new() { TimeStart = new DateTime(2024, 1, 16, 10, 0, 0), TimeEnd = new DateTime(2024, 1, 16, 11, 0, 0) }
        };

        mockRepository.Setup(r => r.GetPricesForDateAsync(testDate1)).ReturnsAsync(prices1);
        mockRepository.Setup(r => r.GetPricesForDateAsync(testDate2)).ReturnsAsync(prices2);

        // Act
        var result1 = await service.GetPriceForDateTimeAsync(testDateTime1);
        var result2 = await service.GetPriceForDateTimeAsync(testDateTime2);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(new DateTime(2024, 1, 15, 10, 0, 0), result1.TimeStart);
        Assert.Equal(new DateTime(2024, 1, 16, 10, 0, 0), result2.TimeStart);
        mockRepository.Verify(r => r.GetPricesForDateAsync(testDate1), Times.Once);
        mockRepository.Verify(r => r.GetPricesForDateAsync(testDate2), Times.Once);
    }

    /// <summary>
    /// Tests that GetPriceForDateTimeAsync returns null from cache when no matching price exists.
    /// </summary>
    [Fact]
    public async Task GetPriceForDateTimeAsync_CacheHitNoMatch_ReturnsNull()
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var testDateTime1 = new DateTime(2024, 1, 15, 10, 30, 0);
        var testDateTime2 = new DateTime(2024, 1, 15, 23, 59, 0);
        var testDate = DateOnly.FromDateTime(testDateTime1);
        var prices = new List<ChargeMaster.Data.ElectricityPrice>
        {
            new() { TimeStart = new DateTime(2024, 1, 15, 10, 0, 0), TimeEnd = new DateTime(2024, 1, 15, 11, 0, 0) }
        };

        mockRepository.Setup(r => r.GetPricesForDateAsync(testDate)).ReturnsAsync(prices);

        // Act
        var result1 = await service.GetPriceForDateTimeAsync(testDateTime1);
        var result2 = await service.GetPriceForDateTimeAsync(testDateTime2);

        // Assert
        Assert.NotNull(result1);
        Assert.Null(result2);
        mockRepository.Verify(r => r.GetPricesForDateAsync(testDate), Times.Once);
    }

    /// <summary>
    /// Tests that GetPriceForDateTimeAsync correctly matches price at exact TimeStart boundary.
    /// </summary>
    [Fact]
    public async Task GetPriceForDateTimeAsync_ExactTimeStart_ReturnsMatchingPrice()
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var testDateTime = new DateTime(2024, 1, 15, 10, 0, 0);
        var testDate = DateOnly.FromDateTime(testDateTime);
        var prices = new List<ChargeMaster.Data.ElectricityPrice>
        {
            new() { TimeStart = new DateTime(2024, 1, 15, 10, 0, 0), TimeEnd = new DateTime(2024, 1, 15, 10, 15, 0) }
        };

        mockRepository.Setup(r => r.GetPricesForDateAsync(testDate)).ReturnsAsync(prices);

        // Act
        var result = await service.GetPriceForDateTimeAsync(testDateTime);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new DateTime(2024, 1, 15, 10, 0, 0), result.TimeStart);
    }

    /// <summary>
    /// Tests that GetPriceForDateTimeAsync returns null at exact TimeEnd boundary.
    /// </summary>
    [Fact]
    public async Task GetPriceForDateTimeAsync_ExactTimeEnd_ReturnsNull()
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var testDateTime = new DateTime(2024, 1, 15, 10, 15, 0);
        var testDate = DateOnly.FromDateTime(testDateTime);
        var prices = new List<ChargeMaster.Data.ElectricityPrice>
        {
            new() { TimeStart = new DateTime(2024, 1, 15, 10, 0, 0), TimeEnd = new DateTime(2024, 1, 15, 10, 15, 0) }
        };

        mockRepository.Setup(r => r.GetPricesForDateAsync(testDate)).ReturnsAsync(prices);

        // Act
        var result = await service.GetPriceForDateTimeAsync(testDateTime);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Tests that GetPriceForDateTimeAsync handles DateTime.MinValue correctly.
    /// </summary>
    [Fact]
    public async Task GetPriceForDateTimeAsync_DateTimeMinValue_ReturnsNullWhenNoMatch()
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var testDateTime = DateTime.MinValue;
        var testDate = DateOnly.FromDateTime(testDateTime);
        var prices = new List<ChargeMaster.Data.ElectricityPrice>
        {
            new() { TimeStart = new DateTime(2024, 1, 15, 10, 0, 0), TimeEnd = new DateTime(2024, 1, 15, 11, 0, 0) }
        };

        mockRepository.Setup(r => r.GetPricesForDateAsync(testDate)).ReturnsAsync(prices);

        // Act
        var result = await service.GetPriceForDateTimeAsync(testDateTime);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Tests that GetPriceForDateTimeAsync handles DateTime.MaxValue correctly.
    /// </summary>
    [Fact]
    public async Task GetPriceForDateTimeAsync_DateTimeMaxValue_ReturnsNullWhenNoMatch()
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var testDateTime = DateTime.MaxValue;
        var testDate = DateOnly.FromDateTime(testDateTime);
        var prices = new List<ChargeMaster.Data.ElectricityPrice>
        {
            new() { TimeStart = new DateTime(2024, 1, 15, 10, 0, 0), TimeEnd = new DateTime(2024, 1, 15, 11, 0, 0) }
        };

        mockRepository.Setup(r => r.GetPricesForDateAsync(testDate)).ReturnsAsync(prices);

        // Act
        var result = await service.GetPriceForDateTimeAsync(testDateTime);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Tests that GetPriceForDateTimeAsync returns matching price when datetime is within time window.
    /// </summary>
    [Fact]
    public async Task GetPriceForDateTimeAsync_DateTimeWithinWindow_ReturnsMatchingPrice()
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var testDateTime = new DateTime(2024, 1, 15, 10, 7, 30);
        var testDate = DateOnly.FromDateTime(testDateTime);
        var prices = new List<ChargeMaster.Data.ElectricityPrice>
        {
            new() { TimeStart = new DateTime(2024, 1, 15, 10, 0, 0), TimeEnd = new DateTime(2024, 1, 15, 10, 15, 0) }
        };

        mockRepository.Setup(r => r.GetPricesForDateAsync(testDate)).ReturnsAsync(prices);

        // Act
        var result = await service.GetPriceForDateTimeAsync(testDateTime);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.TimeStart <= testDateTime);
        Assert.True(result.TimeEnd > testDateTime);
    }

    /// <summary>
    /// Tests that GetPriceForDateTimeAsync selects correct price from multiple options.
    /// </summary>
    [Fact]
    public async Task GetPriceForDateTimeAsync_MultiplePrices_SelectsCorrectOne()
    {
        // Arrange
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        var httpClient = new HttpClient();
        var service = new ElectricityPriceService(httpClient, mockRepository.Object, mockLogger.Object);

        var testDateTime = new DateTime(2024, 1, 15, 10, 20, 0);
        var testDate = DateOnly.FromDateTime(testDateTime);
        var prices = new List<ChargeMaster.Data.ElectricityPrice>
        {
            new() { Id = 1, TimeStart = new DateTime(2024, 1, 15, 10, 0, 0), TimeEnd = new DateTime(2024, 1, 15, 10, 15, 0) },
            new() { Id = 2, TimeStart = new DateTime(2024, 1, 15, 10, 15, 0), TimeEnd = new DateTime(2024, 1, 15, 10, 30, 0) },
            new() { Id = 3, TimeStart = new DateTime(2024, 1, 15, 10, 30, 0), TimeEnd = new DateTime(2024, 1, 15, 10, 45, 0) }
        };

        mockRepository.Setup(r => r.GetPricesForDateAsync(testDate)).ReturnsAsync(prices);

        // Act
        var result = await service.GetPriceForDateTimeAsync(testDateTime);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Id);
        Assert.Equal(new DateTime(2024, 1, 15, 10, 15, 0), result.TimeStart);
        Assert.Equal(new DateTime(2024, 1, 15, 10, 30, 0), result.TimeEnd);
    }
}
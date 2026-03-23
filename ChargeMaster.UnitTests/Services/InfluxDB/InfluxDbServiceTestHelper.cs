using ChargeMaster.Services.ElectricityPrice;
using ChargeMaster.Services.InfluxDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net.Http;

namespace ChargeMaster.Services.InfluxDB.UnitTests;

/// <summary>
/// Helper class for creating properly configured test dependencies for InfluxDbService tests.
/// Centralizes mock creation to reduce duplication and ensure consistency across tests.
/// </summary>
public static class InfluxDbServiceTestHelper
{
    /// <summary>
    /// Creates a mock ElectricityPriceService suitable for unit testing.
    /// The mock returns null for all price queries by default.
    /// </summary>
    /// <returns>A mock ElectricityPriceService instance</returns>
    public static Mock<ElectricityPriceService> CreateMockPriceService()
    {
        var mockHttpClient = new Mock<HttpClient>();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();
        
        var mockPriceService = new Mock<ElectricityPriceService>(
            mockHttpClient.Object,
            mockServiceScopeFactory.Object,
            mockLogger.Object);
        
        // Configure default behavior: returns null for price queries
        mockPriceService
            .Setup(x => x.GetPriceForDateTimeAsync(It.IsAny<DateTime>()))
            .ReturnsAsync((Data.ElectricityPrice?)null);
        
        return mockPriceService;
    }

    /// <summary>
    /// Creates a mock ILogger suitable for verification in tests.
    /// </summary>
    /// <returns>A mock ILogger for InfluxDbService</returns>
    public static Mock<ILogger<InfluxDbService>> CreateMockLogger()
    {
        return new Mock<ILogger<InfluxDbService>>();
    }

    /// <summary>
    /// Creates a mock IInfluxDBClientFactory suitable for unit testing.
    /// The factory returns null by default, which is acceptable since the client
    /// is only used in try-catch blocks of write methods.
    /// </summary>
    /// <returns>A mock IInfluxDBClientFactory instance</returns>
    public static Mock<IInfluxDBClientFactory> CreateMockClientFactory()
    {
        return new Mock<IInfluxDBClientFactory>();
    }

    /// <summary>
    /// Creates properly configured InfluxDB options for testing.
    /// These are test values and won't connect to a real InfluxDB instance.
    /// </summary>
    /// <returns>IOptions configured with valid test InfluxDB settings</returns>
    public static IOptions<InfluxDBOptions> CreateValidOptions()
    {
        var options = new InfluxDBOptions
        {
            Url = "http://localhost:8086",
            Token = "test-token",
            Org = "test-org",
            Bucket = "test-bucket"
        };
        return Options.Create(options);
    }

    /// <summary>
    /// Creates properly configured InfluxDB options with custom values for testing.
    /// </summary>
    /// <param name="url">The InfluxDB URL</param>
    /// <param name="token">The authentication token</param>
    /// <param name="org">The organization name</param>
    /// <param name="bucket">The bucket name</param>
    /// <returns>IOptions configured with the provided InfluxDB settings</returns>
    public static IOptions<InfluxDBOptions> CreateOptionsWithValues(
        string url,
        string token,
        string org,
        string bucket)
    {
        var options = new InfluxDBOptions
        {
            Url = url,
            Token = token,
            Org = org,
            Bucket = bucket
        };
        return Options.Create(options);
    }

    /// <summary>
    /// Creates a fully configured InfluxDbService for testing with all dependencies mocked.
    /// This is the recommended way to create a service instance for unit tests.
    /// </summary>
    /// <param name="options">Optional custom InfluxDB options. If null, valid defaults are used.</param>
    /// <param name="priceService">Optional custom price service. If null, a mock with default behavior is used.</param>
    /// <param name="logger">Optional custom logger. If null, a mock is used.</param>
    /// <param name="clientFactory">Optional custom client factory. If null, a mock is used.</param>
    /// <returns>A configured InfluxDbService instance with all dependencies properly injected</returns>
    public static InfluxDbService CreateServiceWithMocks(
        IOptions<InfluxDBOptions>? options = null,
        Mock<ElectricityPriceService>? priceService = null,
        Mock<ILogger<InfluxDbService>>? logger = null,
        Mock<IInfluxDBClientFactory>? clientFactory = null)
    {
        var optionsToUse = options ?? CreateValidOptions();
        var priceServiceToUse = priceService ?? CreateMockPriceService();
        var loggerToUse = logger ?? CreateMockLogger();
        var clientFactoryToUse = clientFactory ?? CreateMockClientFactory();

        return new InfluxDbService(
            optionsToUse,
            priceServiceToUse.Object,
            loggerToUse.Object,
            clientFactoryToUse.Object);
    }

    /// <summary>
    /// Creates a RealTimeMeasurement for testing with typical values.
    /// </summary>
    /// <returns>A RealTimeMeasurement instance with default test values</returns>
    public static Tibber.Sdk.RealTimeMeasurement CreateMeasurement(
        long power = 1000,
        decimal accumulatedConsumption = 0.5m,
        decimal? powerFactor = null,
        decimal? voltagePhase2 = null,
        decimal? currentPhase2 = null)
    {
        var measurement = new Tibber.Sdk.RealTimeMeasurement
        {
            Timestamp = DateTimeOffset.UtcNow,
            Power = power,
            AccumulatedConsumptionLastHour = accumulatedConsumption
        };

        if (powerFactor.HasValue)
            measurement.PowerFactor = powerFactor.Value;
        
        if (voltagePhase2.HasValue)
            measurement.VoltagePhase2 = voltagePhase2.Value;
        
        if (currentPhase2.HasValue)
            measurement.CurrentPhase2 = currentPhase2.Value;

        return measurement;
    }

    /// <summary>
    /// Creates a WallboxMeterInfo for testing with typical values.
    /// </summary>
    /// <returns>A WallboxMeterInfo instance with default test values</returns>
    public static ChargeMaster.Services.Wallbox.WallboxMeterInfo CreateMeterInfo(
        long accEnergy = 5000,
        double phase1Current = 100.0,
        double phase2Current = 100.0,
        double phase3Current = 100.0,
        string meterSerial = "TEST123")
    {
        return new ChargeMaster.Services.Wallbox.WallboxMeterInfo
        {
            AccEnergy = accEnergy,
            Phase1Current = phase1Current,
            Phase2Current = phase2Current,
            Phase3Current = phase3Current,
            MeterSerial = meterSerial
        };
    }
}

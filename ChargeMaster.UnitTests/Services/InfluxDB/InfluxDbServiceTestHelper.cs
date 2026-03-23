using ChargeMaster.Services.ElectricityPrice;
using ChargeMaster.Services.InfluxDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ChargeMaster.UnitTests.Services.InfluxDB;

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
    /// <exception cref="InvalidOperationException">Thrown if the mock cannot be properly configured</exception>
    public static Mock<ElectricityPriceService> CreateMockPriceService()
    {
        var mockHttpClient = new Mock<HttpClient>();
        var mockRepository = new Mock<IElectricityPriceRepository>();
        var mockLogger = new Mock<ILogger<ElectricityPriceService>>();

        var mockPriceService = new Mock<ElectricityPriceService>(
            mockHttpClient.Object,
            mockRepository.Object,
            mockLogger.Object);

        // Configure default behavior: returns null for price queries
        mockPriceService
            .Setup(x => x.GetPriceForDateTimeAsync(It.IsAny<DateTime>()))
            .ReturnsAsync((Data.ElectricityPrice?)null);

        // Verification: Ensure the mock was created successfully
        if (mockPriceService == null)
        {
            throw new InvalidOperationException("Failed to create mock ElectricityPriceService instance");
        }

        return mockPriceService;
    }

    /// <summary>
    /// Creates a mock ILogger suitable for verification in tests.
    /// </summary>
    /// <returns>A mock ILogger for InfluxDbService</returns>
    /// <exception cref="InvalidOperationException">Thrown if the mock cannot be properly configured</exception>
    public static Mock<ILogger<InfluxDbService>> CreateMockLogger()
    {
        var mockLogger = new Mock<ILogger<InfluxDbService>>();

        // Verification: Ensure the mock was created successfully
        if (mockLogger == null)
        {
            throw new InvalidOperationException("Failed to create mock ILogger<InfluxDbService> instance");
        }

        return mockLogger;
    }

    /// <summary>
    /// Creates a mock IInfluxDBClientFactory suitable for unit testing.
    /// The factory returns null by default, which is acceptable since the client
    /// is only used in try-catch blocks of write methods.
    /// </summary>
    /// <returns>A mock IInfluxDBClientFactory instance</returns>
    /// <exception cref="InvalidOperationException">Thrown if the mock cannot be properly configured</exception>
    public static Mock<IInfluxDBClientFactory> CreateMockClientFactory()
    {
        var mockClientFactory = new Mock<IInfluxDBClientFactory>();

        // Verification: Ensure the mock was created successfully
        if (mockClientFactory == null)
        {
            throw new InvalidOperationException("Failed to create mock IInfluxDBClientFactory instance");
        }

        return mockClientFactory;
    }

    /// <summary>
    /// Creates properly configured InfluxDB options for testing.
    /// These are test values and won't connect to a real InfluxDB instance.
    /// </summary>
    /// <returns>IOptions configured with valid test InfluxDB settings</returns>
    /// <exception cref="InvalidOperationException">Thrown if options cannot be properly created</exception>
    public static IOptions<InfluxDBOptions> CreateValidOptions()
    {
        var options = new InfluxDBOptions
        {
            Url = "http://localhost:8086",
            Token = "test-token",
            Org = "test-org",
            Bucket = "test-bucket"
        };
        var result = Options.Create(options);

        // Verification: Ensure options were created successfully and are accessible
        if (result == null)
        {
            throw new InvalidOperationException("Failed to create IOptions<InfluxDBOptions>");
        }

        if (result.Value == null)
        {
            throw new InvalidOperationException("Options.Value is null; IOptions<InfluxDBOptions> creation failed");
        }

        return result;
    }

    /// <summary>
    /// Creates properly configured InfluxDB options with custom values for testing.
    /// </summary>
    /// <param name="url">The InfluxDB URL - must not be null or empty</param>
    /// <param name="token">The authentication token - must not be null</param>
    /// <param name="org">The organization name - must not be null</param>
    /// <param name="bucket">The bucket name - must not be null</param>
    /// <returns>IOptions configured with the provided InfluxDB settings</returns>
    /// <exception cref="ArgumentNullException">Thrown if any required parameter is null</exception>
    /// <exception cref="ArgumentException">Thrown if any parameter has an invalid format or is empty</exception>
    /// <exception cref="InvalidOperationException">Thrown if options cannot be properly created</exception>
    public static IOptions<InfluxDBOptions> CreateOptionsWithValues(
        string url,
        string token,
        string org,
        string bucket)
    {
        // Guard clauses: Verify parameters
        if (url == null)
            throw new ArgumentNullException(nameof(url), "URL cannot be null");
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be empty or whitespace", nameof(url));

        // Validate URL format (basic check - must be absolute URI)
        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
            throw new ArgumentException("URL must be a valid absolute URI", nameof(url));

        if (token == null)
            throw new ArgumentNullException(nameof(token), "Token cannot be null");

        if (org == null)
            throw new ArgumentNullException(nameof(org), "Organization name cannot be null");

        if (bucket == null)
            throw new ArgumentNullException(nameof(bucket), "Bucket name cannot be null");

        var options = new InfluxDBOptions
        {
            Url = url,
            Token = token,
            Org = org,
            Bucket = bucket
        };
        var result = Options.Create(options);

        // Verification: Ensure options were created successfully
        if (result == null)
        {
            throw new InvalidOperationException("Failed to create IOptions<InfluxDBOptions> with provided values");
        }

        if (result.Value == null)
        {
            throw new InvalidOperationException("Options.Value is null; creation with custom values failed");
        }

        // Verification: Ensure values were set correctly
        if (!result.Value.Url.Equals(url, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"URL mismatch: expected '{url}' but got '{result.Value.Url}'");
        }

        return result;
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
    /// <exception cref="InvalidOperationException">Thrown if the service cannot be created or any dependency is invalid</exception>
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

        // Verification: Ensure all dependencies are properly created before service instantiation
        if (optionsToUse == null)
            throw new InvalidOperationException("InfluxDB options cannot be null");
        if (optionsToUse.Value == null)
            throw new InvalidOperationException("InfluxDB options.Value cannot be null");
        if (priceServiceToUse == null)
            throw new InvalidOperationException("Price service mock cannot be null");
        if (priceServiceToUse.Object == null)
            throw new InvalidOperationException("Price service mock object cannot be null");
        if (loggerToUse == null)
            throw new InvalidOperationException("Logger mock cannot be null");
        if (loggerToUse.Object == null)
            throw new InvalidOperationException("Logger mock object cannot be null");
        if (clientFactoryToUse == null)
            throw new InvalidOperationException("Client factory mock cannot be null");
        if (clientFactoryToUse.Object == null)
            throw new InvalidOperationException("Client factory mock object cannot be null");

        InfluxDbService service;
        try
        {
            service = new InfluxDbService(
                optionsToUse,
                priceServiceToUse.Object,
                loggerToUse.Object,
                clientFactoryToUse.Object);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to create InfluxDbService instance with provided mocks. " +
                "Ensure all options and dependencies are properly configured.",
                ex);
        }

        // Verification: Ensure service was created successfully
        if (service == null)
        {
            throw new InvalidOperationException("InfluxDbService instance creation resulted in null");
        }

        return service;
    }

    /// <summary>
    /// Creates a RealTimeMeasurement for testing with typical values.
    /// </summary>
    /// <param name="power">Power in watts (default: 1000W)</param>
    /// <param name="accumulatedConsumption">Accumulated consumption in kWh (default: 0.5 kWh)</param>
    /// <param name="powerFactor">Optional power factor (0.0-1.0, or null for omission)</param>
    /// <param name="voltagePhase2">Optional phase 2 voltage in volts</param>
    /// <param name="currentPhase2">Optional phase 2 current in amperes</param>
    /// <returns>A RealTimeMeasurement instance with specified test values</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if powerFactor is outside valid range [0.0, 1.0]</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if voltage or current values are negative</exception>
    /// <exception cref="InvalidOperationException">Thrown if measurement object cannot be created</exception>
    public static Tibber.Sdk.RealTimeMeasurement CreateMeasurement(
        long power = 1000,
        decimal accumulatedConsumption = 0.5m,
        decimal? powerFactor = null,
        decimal? voltagePhase2 = null,
        decimal? currentPhase2 = null)
    {
        // Validation: Check power factor range if provided
        if (powerFactor.HasValue && (powerFactor.Value < 0m || powerFactor.Value > 1m))
        {
            throw new ArgumentOutOfRangeException(
                nameof(powerFactor),
                powerFactor.Value,
                "Power factor must be between 0.0 and 1.0");
        }

        // Validation: Check voltage is non-negative if provided
        if (voltagePhase2.HasValue && voltagePhase2.Value < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(voltagePhase2),
                voltagePhase2.Value,
                "Voltage cannot be negative");
        }

        // Validation: Check current is non-negative if provided
        if (currentPhase2.HasValue && currentPhase2.Value < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentPhase2),
                currentPhase2.Value,
                "Current cannot be negative");
        }

        try
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

            // Verification: Ensure measurement was created with expected values
            if (measurement == null)
            {
                throw new InvalidOperationException("RealTimeMeasurement instance creation resulted in null");
            }

            if (measurement.Power != power)
            {
                throw new InvalidOperationException(
                    $"Power mismatch: expected {power}W but measurement has {measurement.Power}W");
            }

            if (measurement.AccumulatedConsumptionLastHour != accumulatedConsumption)
            {
                throw new InvalidOperationException(
                    $"Accumulated consumption mismatch: expected {accumulatedConsumption} kWh but measurement has {measurement.AccumulatedConsumptionLastHour} kWh");
            }

            return measurement;
        }
        catch (Exception ex) when (!(ex is ArgumentOutOfRangeException) && !(ex is InvalidOperationException))
        {
            throw new InvalidOperationException(
                "Failed to create RealTimeMeasurement instance with provided values",
                ex);
        }
    }

    /// <summary>
    /// Creates a WallboxMeterInfo for testing with typical values.
    /// </summary>
    /// <param name="accEnergy">Accumulated energy in Wh (default: 5000 Wh)</param>
    /// <param name="phase1Current">Phase 1 current in amperes (default: 100A, must be non-negative)</param>
    /// <param name="phase2Current">Phase 2 current in amperes (default: 100A, must be non-negative)</param>
    /// <param name="phase3Current">Phase 3 current in amperes (default: 100A, must be non-negative)</param>
    /// <param name="meterSerial">Meter serial number (default: "TEST123")</param>
    /// <returns>A WallboxMeterInfo instance with specified test values</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if any current value is negative</exception>
    /// <exception cref="ArgumentNullException">Thrown if meterSerial is null</exception>
    /// <exception cref="InvalidOperationException">Thrown if meter info object cannot be created</exception>
    public static ChargeMaster.Services.Wallbox.WallboxMeterInfo CreateMeterInfo(
        long accEnergy = 5000,
        double phase1Current = 100.0,
        double phase2Current = 100.0,
        double phase3Current = 100.0,
        string meterSerial = "TEST123")
    {
        // Guard clauses: Validate inputs
        if (meterSerial == null)
        {
            throw new ArgumentNullException(nameof(meterSerial), "Meter serial cannot be null");
        }

        // Validation: Current values must be non-negative
        if (phase1Current < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(phase1Current),
                phase1Current,
                "Phase 1 current cannot be negative");
        }

        if (phase2Current < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(phase2Current),
                phase2Current,
                "Phase 2 current cannot be negative");
        }

        if (phase3Current < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(phase3Current),
                phase3Current,
                "Phase 3 current cannot be negative");
        }

        try
        {
            var meterInfo = new ChargeMaster.Services.Wallbox.WallboxMeterInfo
            {
                AccEnergy = accEnergy,
                Phase1Current = phase1Current,
                Phase2Current = phase2Current,
                Phase3Current = phase3Current,
                MeterSerial = meterSerial
            };

            // Verification: Ensure meter info was created with expected values
            if (meterInfo == null)
            {
                throw new InvalidOperationException("WallboxMeterInfo instance creation resulted in null");
            }

            if (Math.Abs(meterInfo.Phase1Current - phase1Current) > 0.001)
            {
                throw new InvalidOperationException(
                    $"Phase 1 current mismatch: expected {phase1Current}A but meter info has {meterInfo.Phase1Current}A");
            }

            if (!meterInfo.MeterSerial.Equals(meterSerial, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Meter serial mismatch: expected '{meterSerial}' but meter info has '{meterInfo.MeterSerial}'");
            }

            return meterInfo;
        }
        catch (Exception ex) when (!(ex is ArgumentOutOfRangeException) && !(ex is ArgumentNullException) && !(ex is InvalidOperationException))
        {
            throw new InvalidOperationException(
                "Failed to create WallboxMeterInfo instance with provided values",
                ex);
        }
    }
}

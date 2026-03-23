using ChargeMaster.Services.Daikin;
using Microsoft.Extensions.Logging;
using Moq;

namespace ChargeMaster.UnitTests.Services.Daikin;

/// <summary>
/// Helper class for creating properly configured test dependencies for DaikinFacade unit tests.
/// Centralizes mock creation to reduce duplication and ensure consistency across tests.
/// </summary>
public static class DaikinFacadeTestHelper
{
    /// <summary>
    /// Creates a mock IDaikinService with default behavior for basic testing.
    /// </summary>
    /// <returns>A mock IDaikinService instance</returns>
    public static Mock<IDaikinService> CreateMockDaikinService()
    {
        var mockService = new Mock<IDaikinService>();

        // Configure default sensor info response
        mockService
            .Setup(x => x.GetSensorInfoAsync())
            .ReturnsAsync(new DaikinSensorInfo
            {
                IndoorTemperature = 22.5,
                OutdoorTemperature = 5.0,
                IndoorHumidity = 45.0,
                CompressorFrequency = 0,
                ErrorCode = 0
            });

        // Configure default control info response
        mockService
            .Setup(x => x.GetControlInfoAsync())
            .ReturnsAsync(new DaikinControlInfo
            {
                Power = 1,
                Mode = 4, // Heat mode
                TargetTemperature = 21.0,
                TargetHumidity = "0",
                FanRate = "AUTO",
                FanDirection = 0,
                Alert = 255,
                Advanced = ""
            });

        // Configure default behavior for control operations
        mockService
            .Setup(x => x.SetTargetTemperatureAsync(It.IsAny<double>(), It.IsAny<bool>()))
            .ReturnsAsync(true);

        mockService
            .Setup(x => x.TurnOnAsync())
            .ReturnsAsync(true);

        mockService
            .Setup(x => x.TurnOffAsync())
            .ReturnsAsync(true);

        mockService
            .Setup(x => x.SetModeAsync(It.IsAny<int>()))
            .ReturnsAsync(true);

        return mockService;
    }

    /// <summary>
    /// Creates a mock ILogger for DaikinFacade testing.
    /// </summary>
    /// <returns>A mock ILogger for DaikinFacade</returns>
    public static Mock<ILogger<DaikinFacade>> CreateMockLogger()
    {
        return new Mock<ILogger<DaikinFacade>>();
    }

    /// <summary>
    /// Creates a fully configured DaikinFacade for unit testing with all dependencies mocked.
    /// </summary>
    /// <param name="daikinService">Optional custom Daikin service mock. If null, default mock is used.</param>
    /// <param name="logger">Optional custom logger mock. If null, default mock is used.</param>
    /// <returns>A configured DaikinFacade instance with all dependencies properly mocked</returns>
    public static DaikinFacade CreateFacadeWithMocks(
        Mock<IDaikinService>? daikinService = null,
        Mock<ILogger<DaikinFacade>>? logger = null)
    {
        var serviceToUse = daikinService ?? CreateMockDaikinService();
        var loggerToUse = logger ?? CreateMockLogger();

        if (serviceToUse == null)
            throw new InvalidOperationException("Daikin service mock cannot be null");
        if (serviceToUse.Object == null)
            throw new InvalidOperationException("Daikin service mock object cannot be null");
        if (loggerToUse == null)
            throw new InvalidOperationException("Logger mock cannot be null");
        if (loggerToUse.Object == null)
            throw new InvalidOperationException("Logger mock object cannot be null");

        try
        {
            return new DaikinFacade(serviceToUse.Object, loggerToUse.Object);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to create DaikinFacade instance with provided mocks. " +
                "Ensure all dependencies are properly configured.",
                ex);
        }
    }

    /// <summary>
    /// Creates mock DaikinSensorInfo with custom values for testing.
    /// </summary>
    /// <param name="indoorTemp">Indoor temperature in °C (default: 22.5)</param>
    /// <param name="outdoorTemp">Outdoor temperature in °C (default: 5.0)</param>
    /// <param name="compressorFreq">Compressor frequency (default: 0)</param>
    /// <returns>A DaikinSensorInfo instance with specified values</returns>
    public static DaikinSensorInfo CreateSensorInfo(
        double indoorTemp = 22.5,
        double outdoorTemp = 5.0,
        int compressorFreq = 0)
    {
        return new DaikinSensorInfo
        {
            IndoorTemperature = indoorTemp,
            OutdoorTemperature = outdoorTemp,
            IndoorHumidity = 45.0,
            CompressorFrequency = compressorFreq,
            ErrorCode = 0
        };
    }

    /// <summary>
    /// Creates mock DaikinControlInfo with custom values for testing.
    /// </summary>
    /// <param name="power">Power state: 0 = Off, 1 = On (default: 1)</param>
    /// <param name="mode">Mode value: 0=Auto, 2=Dry, 3=Cool, 4=Heat, 6=Fan (default: 4 = Heat)</param>
    /// <param name="targetTemp">Target temperature in °C (default: 21.0)</param>
    /// <returns>A DaikinControlInfo instance with specified values</returns>
    public static DaikinControlInfo CreateControlInfo(
        int power = 1,
        int mode = 4,
        double targetTemp = 21.0)
    {
        return new DaikinControlInfo
        {
            Power = power,
            Mode = mode,
            TargetTemperature = targetTemp,
            TargetHumidity = "0",
            FanRate = "AUTO",
            FanDirection = 0,
            Alert = 255,
            Advanced = ""
        };
    }

    /// <summary>
    /// Configures a mock Daikin service to return specific sensor and control info on subsequent calls.
    /// Useful for testing state changes and event triggering.
    /// </summary>
    /// <param name="mockService">The mock service to configure</param>
    /// <param name="sensorInfo">The sensor info to return</param>
    /// <param name="controlInfo">The control info to return</param>
    public static void ConfigureMockServiceResponses(
        Mock<IDaikinService> mockService,
        DaikinSensorInfo sensorInfo,
        DaikinControlInfo controlInfo)
    {
        mockService
            .Setup(x => x.GetSensorInfoAsync())
            .ReturnsAsync(sensorInfo);

        mockService
            .Setup(x => x.GetControlInfoAsync())
            .ReturnsAsync(controlInfo);
    }

    /// <summary>
    /// Configures a mock Daikin service to throw an exception when called.
    /// Useful for testing error handling.
    /// </summary>
    /// <param name="mockService">The mock service to configure</param>
    /// <param name="exception">The exception to throw</param>
    public static void ConfigureMockServiceToThrow(
        Mock<IDaikinService> mockService,
        Exception exception)
    {
        mockService
            .Setup(x => x.GetSensorInfoAsync())
            .ThrowsAsync(exception);

        mockService
            .Setup(x => x.GetControlInfoAsync())
            .ThrowsAsync(exception);

        mockService
            .Setup(x => x.SetTargetTemperatureAsync(It.IsAny<double>(), It.IsAny<bool>()))
            .ThrowsAsync(exception);

        mockService
            .Setup(x => x.TurnOnAsync())
            .ThrowsAsync(exception);

        mockService
            .Setup(x => x.TurnOffAsync())
            .ThrowsAsync(exception);

        mockService
            .Setup(x => x.SetModeAsync(It.IsAny<int>()))
            .ThrowsAsync(exception);
    }

    /// <summary>
    /// Configures a mock Daikin service to return null responses.
    /// Useful for testing handling of missing or unavailable data.
    /// </summary>
    /// <param name="mockService">The mock service to configure</param>
    public static void ConfigureMockServiceToReturnNull(Mock<IDaikinService> mockService)
    {
        mockService
            .Setup(x => x.GetSensorInfoAsync())
            .ReturnsAsync((DaikinSensorInfo?)null);

        mockService
            .Setup(x => x.GetControlInfoAsync())
            .ReturnsAsync((DaikinControlInfo?)null);
    }
}
